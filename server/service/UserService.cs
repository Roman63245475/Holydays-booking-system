using api;
using be;
using database;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.AspNetCore.Identity;

namespace service;


public class UserService
{

    private Database db;
    private HolidayAccountsService holidayAccountsService;
    private HolidayHistoryService holidayHistoryService;
    private ScheduleService scheduleService;
    private CurrentDateService currentDateService;
    private AccountStatusService accountStatusService;
    private RequestService requestService;

    public UserService(Database db, HolidayAccountsService holidayAccountsService, HolidayHistoryService holidayHistoryService,
        ScheduleService scheduleService, CurrentDateService currentDateService, AccountStatusService accountStatusService,
        RequestService requestService)
    {
        this.accountStatusService = accountStatusService;
        this.requestService = requestService;
        this.currentDateService = currentDateService;
        this.scheduleService = scheduleService;
        this.db = db;
        this.holidayAccountsService = holidayAccountsService;
        this.holidayHistoryService = holidayHistoryService;
    }

    // an employee (role User) gets their working schedule, holiday accounts and first histories. the accountant and
    // the system owner are role accounts: no schedule, no holiday, and there is exactly one of each (the system owner
    // is created on startup, ux_users_single_role in the db is the safety net).
    // throws InvalidOperationException if the role account already exists / an employee has no schedule
    public async Task create_user(User user, Schedule? schedule)
    {
        if (user.role != Role.User && await db.users.AnyAsync(u => u.role == user.role))
        {
            string existing = await db.users.Where(u => u.role == user.role).Select(u => u.email).FirstAsync();
            throw new InvalidOperationException(
                $"There is already {(user.role == Role.Accountant ? "an" : "a")} {role_name(user.role)} account ({existing}). There can only be one: " +
                "change its name, email or password to hand it over to another person.");
        }
        if (user.role == Role.User && schedule == null)
        {
            throw new InvalidOperationException("An employee needs a working schedule");
        }
        string email = user.email.Trim();
        if (await db.users.AnyAsync(u => u.email == email))
        {
            throw new InvalidOperationException("Another user already has this email");
        }

        PasswordHasher<User> hasher = new PasswordHasher<User>();
        var hashedPassword = hasher.HashPassword(user, user.password);
        user.password = hashedPassword;
        var transaction = db.BeginTransaction();
        try
        {
            User inserted = await db.users.InsertWithOutputAsync(user);
            if (inserted.role == Role.User)
            {
                // in the same transaction: an employee is never saved without a schedule
                await scheduleService.create_first_schedule(inserted, schedule!);
                var (law_holidays, agreed_holidays) = await holidayAccountsService.initialize_users_holidays_accounts(inserted);
                await holidayHistoryService.create_history_records(law_holidays, agreed_holidays, inserted.start_date);
            }

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw new Exception("Failed to create new user", e);
        }
    }

    // changes the user's data and, if it changed, their working schedule (a new schedule period from schedule_date,
    // employees only). an empty password keeps the old one. the role can not be changed: an employee has holiday
    // accounts and a role account has none, and there is exactly one accountant and one system owner. a role account
    // is handed over to another person by changing its name, email and password.
    // throws KeyNotFoundException if the user does not exist and InvalidOperationException if the email belongs to
    // another user, the role was changed or an employee has no schedule
    public async Task update_user(Guid id, string name, string email, string? password, Role role,
        Schedule? schedule, DateOnly schedule_date)
    {
        User? user = await db.users.FirstOrDefaultAsync(u => u.id == id);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }
        if (role != user.role)
        {
            throw new InvalidOperationException(
                "The role of a user can not be changed. To give the accountant or system owner account to another " +
                "person, change its name, email and password.");
        }
        if (user.role == Role.User && schedule == null)
        {
            throw new InvalidOperationException("schedule is required");
        }

        string trimmed_email = email.Trim();
        if (await db.users.AnyAsync(u => u.email == trimmed_email && u.id != id))
        {
            throw new InvalidOperationException("Another user already has this email");
        }

        user.name = name.Trim();
        user.email = trimmed_email;
        if (!string.IsNullOrEmpty(password))
        {
            user.password = new PasswordHasher<User>().HashPassword(user, password);
        }

        await using var transaction = await db.BeginTransactionAsync();
        await db.UpdateAsync(user);
        if (user.role == Role.User)
        {
            await scheduleService.change_schedule(id, schedule!, schedule_date);
        }
        await transaction.CommitAsync();
    }

    private static string role_name(Role role)
    {
        return role == Role.Admin ? "system owner" : role == Role.Accountant ? "accountant" : "employee";
    }

    // removes the user with everything that belongs to them: holiday requests, holiday accounts with their
    // histories and schedules (accounts, histories and schedules are removed by ON DELETE CASCADE).
    // returns false if the user does not exist
    public async Task<bool> delete_user(Guid id)
    {
        await using var transaction = await db.BeginTransactionAsync();
        // holiday_request.user_id has no ON DELETE CASCADE, so the requests are removed first
        await db.HolidayRequest.Where(r => r.user_id == id).DeleteAsync();
        int deleted = await db.users.Where(u => u.id == id).DeleteAsync();
        await transaction.CommitAsync();
        return deleted > 0;
    }

    // the employee leaves the company (fratræden) after end_date (the last working day), or null = the leaving is
    // called off (only before the last working day has passed). in one transaction:
    //  - the month that is accrued next (the open history) ends on the last working day, so exactly the days worked
    //    are earned; next_month then stops the accounts (AccuralService)
    //  - approved holidays after the last day are cancelled, pending requests past it declined (RequestService)
    //  - pending transfers are declined: what is left is settled with the final salary
    // the remaining balance on the last day is shown as the settlement (AccountStatusService, flag "leaving").
    // calling it off does not bring back holidays that were cancelled, the employee requests them again.
    // throws KeyNotFoundException, InvalidOperationException (date not allowed / an approved holiday crosses it)
    public async Task<LeavingResult> set_end_date(Guid id, DateOnly? end_date, Guid decided_by)
    {
        User? user = await db.users.FirstOrDefaultAsync(u => u.id == id);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }
        if (user.role != Role.User)
        {
            throw new InvalidOperationException(
                $"The {role_name(user.role)} account is not an employee, it can not leave the company. " +
                "Hand it over to another person by changing its name, email and password.");
        }

        // "today" of the system: the 1st of the month that is accrued next. the months before it are already accrued
        // and can not be changed anymore, so the last working day can not lie in them
        DateOnly today = await currentDateService.get_current_date();
        if (user.end_date != null && user.end_date < today)
        {
            throw new InvalidOperationException(
                $"{user.name} already left on {user.end_date:yyyy-MM-dd}, this can not be changed anymore");
        }
        if (end_date != null && end_date < today)
        {
            throw new InvalidOperationException(
                $"The last working day can not be before today ({today:yyyy-MM-dd}): the months before are already accrued");
        }
        if (end_date != null && end_date < user.start_date)
        {
            throw new InvalidOperationException($"The last working day can not be before the start date ({user.start_date:yyyy-MM-dd})");
        }

        await using var transaction = await db.BeginTransactionAsync();
        await db.users.Where(u => u.id == id).Set(u => u.end_date, end_date).UpdateAsync();

        List<HolidayAccount> earning = (await db.law_holidays.Where(l => l.user_id == id && l.active).ToListAsync())
            .Cast<HolidayAccount>()
            .ToList();
        await set_open_history_end<LawHolidaysHistory>(earning, end_date);
        earning = (await db.agreed_holidays.Where(a => a.user_id == id && a.active).ToListAsync())
            .Cast<HolidayAccount>()
            .ToList();
        await set_open_history_end<AgreedHolidaysHistory>(earning, end_date);

        LeavingResult result = new LeavingResult();
        if (end_date != null)
        {
            (result.cancelled_holidays, result.declined_requests) =
                await requestService.HandleLeaving(id, end_date.Value, decided_by);
            result.declined_transfers = await TransferService.decline_for_leaving(db, id, end_date.Value);
        }

        await transaction.CommitAsync();
        return result;
    }

    // the open (not yet accrued) history of every earning account normally ends with its month (or the earning
    // period). with a last working day inside that month it ends on that day. recalculated from the history's own
    // month, so changing or calling off the leaving date gives the right end again
    private async Task set_open_history_end<THistory>(List<HolidayAccount> accounts, DateOnly? end_date)
        where THistory : HolidayHistory
    {
        foreach (HolidayAccount account in accounts)
        {
            THistory? open = await db.GetTable<THistory>()
                .Where(h => h.account_id == account.id)
                .OrderByDescending(h => h.period_start)
                .FirstOrDefaultAsync();
            if (open == null)
            {
                continue;
            }

            DateOnly natural_end = HolidayParameters.last_day_of_month(open.period_start.Year, open.period_start.Month);
            if (natural_end > account.earning_period_end)
            {
                natural_end = account.earning_period_end;
            }
            DateOnly period_end = end_date != null && end_date < natural_end ? end_date.Value : natural_end;

            await db.GetTable<THistory>()
                .Where(h => h.id == open.id)
                .Set(h => h.period_end, period_end)
                .UpdateAsync();
        }
    }

    public async Task<List<ClientUser>> get_all_users()
    {
        List<ClientUser> users = await db.users.Select(u => new ClientUser
            { id = u.id, name = u.name, email = u.email, role = u.role, start_date = u.start_date, end_date = u.end_date })
            .ToListAsync();
        return users;
    }

    public async Task<UserViewPage?> get_user(Guid userId)
    {
        User? user = await db.users.FirstOrDefaultAsync(u => u.id == userId);
        if (user == null)
        {
            return null;
        }

        // all accounts that have not expired yet (usable), not only the ones that are still earning (active),
        // with the status of each balance (expiry flag, transfer, settlement when leaving)
        List<HolidayAccountView> accounts = await accountStatusService.get_account_views(userId);

        // the summer whose main holiday window is not over yet: this year until 30 September, then the next one
        DateOnly today = await currentDateService.get_current_date();
        RuleSet rules = HolidayParameters.get_rule_set(today);
        int season = today.Year;
        if (today > HolidayParameters.last_day_of_month(season, rules.main_holiday_end_month))
        {
            season++;
        }
        RuleSet season_rules = HolidayParameters.get_rule_set(new DateOnly(season, rules.main_holiday_start_month, 1));
        DateOnly window_start = new DateOnly(season, season_rules.main_holiday_start_month, 1);
        DateOnly window_end = HolidayParameters.last_day_of_month(season, season_rules.main_holiday_end_month);
        string window_start_text = window_start.ToString("yyyy-MM-dd");
        string window_end_text = window_end.ToString("yyyy-MM-dd");

        // dates are "yyyy-MM-dd", so comparing the text compares the dates
        List<RequestStatus> main_holidays = (await db.HolidayRequest
                .Where(r => r.user_id == userId && r.is_main_holiday
                            && (r.RequestStatus == RequestStatus.Accepted || r.RequestStatus == RequestStatus.Pending))
                .Select(r => new { r.StartDate, r.EndDate, r.RequestStatus })
                .ToListAsync())
            .Where(r => string.CompareOrdinal(r.StartDate, window_end_text) <= 0
                        && string.CompareOrdinal(r.EndDate, window_start_text) >= 0)
            .Select(r => r.RequestStatus)
            .ToList();
        string main_holiday_status = main_holidays.Contains(RequestStatus.Accepted) ? "approved"
            : main_holidays.Contains(RequestStatus.Pending) ? "pending"
            : "none";

        return new UserViewPage
        {
            name = user.name,
            email = user.email,
            main_holiday_year = season,
            main_holiday_window_start = window_start,
            main_holiday_window_end = window_end,
            main_holiday_length_days = season_rules.main_holiday_length_days,
            main_holiday_notice_months = rules.main_holiday_notice_months,
            main_holiday_status = main_holiday_status,
            end_date = user.end_date,
            // weeks 1-5 first, the account that expires first comes first (it should be used first)
            accounts = accounts
        };
    }

    public async Task<Guid?> get_accId(Guid userId, string kind)
    {
        if (string.Equals(kind, "law", StringComparison.OrdinalIgnoreCase))
        {
            return await db.law_holidays
                .Where(account => account.user_id == userId && account.active)
                .Select(account => (Guid?)account.id)
                .FirstOrDefaultAsync();
        }

        if (string.Equals(kind, "agreed", StringComparison.OrdinalIgnoreCase))
        {
            return await db.agreed_holidays
                .Where(account => account.user_id == userId && account.active)
                .Select(account => (Guid?)account.id)
                .FirstOrDefaultAsync();
        }

        return null;
    }
}
