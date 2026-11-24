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

    public UserService(Database db, HolidayAccountsService holidayAccountsService, HolidayHistoryService holidayHistoryService, ScheduleService scheduleService)
    {
        this.scheduleService = scheduleService;
        this.db = db;
        this.holidayAccountsService = holidayAccountsService;
        this.holidayHistoryService = holidayHistoryService;
    }

    public async Task create_user(User user, Schedule schedule)
    {
        PasswordHasher<User> hasher = new PasswordHasher<User>();
        var hashedPassword = hasher.HashPassword(user, user.password);
        user.password = hashedPassword;
        var transaction = db.BeginTransaction();
        try
        {
            User inserted = await db.users.InsertWithOutputAsync(user);
            // in the same transaction: a user is never saved without a schedule
            await scheduleService.create_first_schedule(inserted, schedule);
            var (law_holidays, agreed_holidays) = await holidayAccountsService.initialize_users_holidays_accounts(inserted);
            await holidayHistoryService.create_history_records(law_holidays, agreed_holidays, inserted.start_date);

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw new Exception("Failed to create new user", e);
        }
    }

    // changes the user's data and, if it changed, their working schedule (a new schedule period from schedule_date).
    // an empty password keeps the old one. throws KeyNotFoundException if the user does not exist and
    // InvalidOperationException if the email belongs to another user
    public async Task update_user(Guid id, string name, string email, string? password, Role role,
        Schedule schedule, DateOnly schedule_date)
    {
        User? user = await db.users.FirstOrDefaultAsync(u => u.id == id);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        string trimmed_email = email.Trim();
        if (await db.users.AnyAsync(u => u.email == trimmed_email && u.id != id))
        {
            throw new InvalidOperationException("Another user already has this email");
        }

        user.name = name.Trim();
        user.email = trimmed_email;
        user.role = role;
        if (!string.IsNullOrEmpty(password))
        {
            user.password = new PasswordHasher<User>().HashPassword(user, password);
        }

        await using var transaction = await db.BeginTransactionAsync();
        await db.UpdateAsync(user);
        await scheduleService.change_schedule(id, schedule, schedule_date);
        await transaction.CommitAsync();
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

    public async Task<List<ClientUser>> get_all_users()
    {
        List<ClientUser> users = await db.users.Select(u => new ClientUser
            { id = u.id, name = u.name, email = u.email, role = u.role, start_date = u.start_date }).ToListAsync();
        return users;
    }

    public async Task<UserViewPage?> get_user(Guid userId)
    {
        User? user = await db.users.FirstOrDefaultAsync(u => u.id == userId);
        if (user == null)
        {
            return null;
        }

        // all accounts that have not expired yet (usable), not only the ones that are still earning (active)
        List<HolidayAccountView> law_accounts = await db.law_holidays
            .Where(l => l.user_id == userId && l.usable)
            .Select(l => new HolidayAccountView
            {
                id = l.id,
                holiday_type = l.holiday_type,
                active = l.active,
                earning_period_start = l.earning_period_start,
                earning_period_end = l.earning_period_end,
                usage_period_start = l.usage_period_start,
                usage_period_end = l.usage_period_end,
                earned_holiday_hours = l.earned_holiday_hours,
                used_holiday_hours = l.used_holiday_hours,
                converted_holiday_hours = l.converted_holiday_hours,
                available_holiday_hours = l.earned_holiday_hours + l.converted_holiday_hours - l.used_holiday_hours
            })
            .ToListAsync();

        List<HolidayAccountView> agreed_accounts = await db.agreed_holidays
            .Where(a => a.user_id == userId && a.usable)
            .Select(a => new HolidayAccountView
            {
                id = a.id,
                holiday_type = a.holiday_type,
                active = a.active,
                earning_period_start = a.earning_period_start,
                earning_period_end = a.earning_period_end,
                usage_period_start = a.usage_period_start,
                usage_period_end = a.usage_period_end,
                earned_holiday_hours = a.earned_holiday_hours,
                used_holiday_hours = a.used_holiday_hours,
                converted_holiday_hours = a.converted_holiday_hours,
                available_holiday_hours = a.earned_holiday_hours + a.converted_holiday_hours - a.used_holiday_hours
            })
            .ToListAsync();

        return new UserViewPage
        {
            name = user.name,
            email = user.email,
            // weeks 1-5 first, the account that expires first comes first (it should be used first)
            accounts = law_accounts.Concat(agreed_accounts)
                .OrderBy(a => a.holiday_type)
                .ThenBy(a => a.usage_period_end)
                .ToList()
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
