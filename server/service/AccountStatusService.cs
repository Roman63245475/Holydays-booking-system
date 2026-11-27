using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

// whether the hours of an account can be transferred to the next holiday period right now, and how many
public record TransferOption(bool possible, decimal max_hours, DateOnly? deadline, string? reason);

// approved transfers of one account: hours received from the previous holiday period and hours sent to the next one.
// both are needed (transferred_holiday_hours is only the sum of them), they come from holiday_transfer_request
public record TransferTotals(decimal received, decimal sent)
{
    public static readonly TransferTotals None = new TransferTotals(0, 0);

    // the approved transfers of the given accounts, by account id
    public static Dictionary<Guid, TransferTotals> from_accepted(IEnumerable<HolidayTransferRequest> accepted, IEnumerable<Guid> account_ids)
    {
        List<HolidayTransferRequest> list = accepted.Where(t => t.status == RequestStatus.Accepted).ToList();
        return account_ids.Distinct().ToDictionary(id => id, id => new TransferTotals(
            list.Where(t => t.to_account_id == id).Sum(t => t.hours),
            list.Where(t => t.from_account_id == id).Sum(t => t.hours)));
    }
}

// the status of every holiday balance: what is planned, what still has to be taken before it expires, whether it
// can be transferred, what is left when the employee leaves. "Show employees and accounting expiry dates ...
// flag balances that must be taken, transferred or paid out". nothing is saved here, everything is calculated
public class AccountStatusService
{
    // a balance with unplanned hours is flagged this long before its usage period ends
    // (weeks 1-5: from 1 September, when the earning period is over and only the last 4 months are left)
    public const int EXPIRY_WARNING_MONTHS = 4;

    // expired balances that were neither taken nor transferred are shown to the accountant this long after they expired
    public const int EXPIRED_SHOWN_MONTHS = 12;

    // the Holiday Act (Ferieloven § 20): the first 4 weeks (20 holiday days) must be taken as time off, only holiday
    // beyond them (the 5th week) can be transferred. this is the law itself, not a company rule like
    // transfer_max_days, so it is not a rule set parameter
    public const int LAW_DAYS_THAT_MUST_BE_TAKEN = 20;

    private readonly Database db;
    private readonly CurrentDateService currentDateService;

    public AccountStatusService(Database db, CurrentDateService currentDateService)
    {
        this.db = db;
        this.currentDateService = currentDateService;
    }

    // every account of the user that has not expired yet, with the status of its balance (employee's home page,
    // the system owner's user page)
    public async Task<List<HolidayAccountView>> get_account_views(Guid user_id)
    {
        User? user = await db.users.FirstOrDefaultAsync(u => u.id == user_id);
        if (user == null)
        {
            return new List<HolidayAccountView>();
        }

        DateOnly today = await currentDateService.get_current_date();
        return (await build(new List<User> { user }, today, include_expired: false))
            .Select(r => r.account)
            .ToList();
    }

    // every balance of every employee for the accountant: the usable ones and the ones that expired in the last
    // EXPIRED_SHOWN_MONTHS with hours left (payroll has to pay them out / report them to the holiday fund)
    public async Task<List<EmployeeBalanceRow>> get_overview()
    {
        DateOnly today = await currentDateService.get_current_date();
        // only employees have holiday, the accountant and system owner are role accounts
        List<User> users = await db.users.Where(u => u.role == Role.User).ToListAsync();
        return (await build(users, today, include_expired: true))
            .OrderBy(r => r.name)
            .ThenBy(r => r.account.holiday_type)
            .ThenBy(r => r.account.usage_period_end)
            .ToList();
    }

    private async Task<List<EmployeeBalanceRow>> build(List<User> users, DateOnly today, bool include_expired)
    {
        List<Guid> user_ids = users.Select(u => u.id).ToList();

        // all accounts of these users, also expired ones: the next account of a transfer must be found among them
        List<HolidayAccount> accounts = new List<HolidayAccount>();
        accounts.AddRange(await db.law_holidays.Where(l => user_ids.Contains(l.user_id)).ToListAsync());
        accounts.AddRange(await db.agreed_holidays.Where(a => user_ids.Contains(a.user_id)).ToListAsync());

        Dictionary<Guid, decimal> pending_holidays = (await db.HolidayRequest
                .Where(r => user_ids.Contains(r.user_id) && r.RequestStatus == RequestStatus.Pending)
                .Select(r => new { r.account_id, r.Hours })
                .ToListAsync())
            .GroupBy(r => r.account_id)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Hours));

        Dictionary<Guid, HolidayTransferRequest> pending_transfers = (await db.transfer_requests
                .Where(t => user_ids.Contains(t.user_id) && t.status == RequestStatus.Pending)
                .ToListAsync())
            .GroupBy(t => t.from_account_id)
            .ToDictionary(g => g.Key, g => g.First());

        Dictionary<Guid, TransferTotals> transfer_totals = TransferTotals.from_accepted(
            await db.transfer_requests
                .Where(t => user_ids.Contains(t.user_id) && t.status == RequestStatus.Accepted)
                .ToListAsync(),
            accounts.Select(a => a.id));

        Dictionary<Guid, List<Schedule>> schedules = (await db.schedules
                .Where(s => user_ids.Contains(s.user_id))
                .ToListAsync())
            .GroupBy(s => s.user_id)
            .ToDictionary(g => g.Key, g => g.ToList());

        // histories are only needed for the settlement of leaving employees: what they still earn until the last day
        HashSet<Guid> leaving = users.Where(u => u.end_date != null).Select(u => u.id).ToHashSet();
        List<Guid> earning_ids = accounts.Where(a => a.active && leaving.Contains(a.user_id)).Select(a => a.id).ToList();
        Dictionary<Guid, List<HolidayHistory>> histories = earning_ids.Count == 0
            ? new Dictionary<Guid, List<HolidayHistory>>()
            : (await db.law_holidays_history.Where(h => earning_ids.Contains(h.account_id)).ToListAsync())
                .Cast<HolidayHistory>()
                .Concat((await db.agreed_holidays_history.Where(h => earning_ids.Contains(h.account_id)).ToListAsync())
                    .Cast<HolidayHistory>())
                .GroupBy(h => h.account_id)
                .ToDictionary(g => g.Key, g => g.ToList());

        DateOnly expired_shown_from = today.AddMonths(-EXPIRED_SHOWN_MONTHS);
        List<EmployeeBalanceRow> rows = new List<EmployeeBalanceRow>();
        foreach (User user in users)
        {
            List<HolidayAccount> user_accounts = accounts.Where(a => a.user_id == user.id).ToList();
            List<Schedule> user_schedules = schedules.GetValueOrDefault(user.id, new List<Schedule>());
            Schedule? schedule = Schedule.find_for_date(user_schedules, today)
                                 ?? user_schedules.OrderByDescending(s => s.effective_from).FirstOrDefault();
            decimal day_hours = schedule?.get_average_day_hours() ?? 0;

            foreach (HolidayAccount account in user_accounts)
            {
                bool show = account.usable
                            || (include_expired && account.get_available() != 0
                                                && account.usage_period_end >= expired_shown_from);
                if (!show)
                {
                    continue;
                }

                decimal holiday_hours = pending_holidays.GetValueOrDefault(account.id, 0);
                pending_transfers.TryGetValue(account.id, out HolidayTransferRequest? transfer);
                HolidayAccount? next = find_next_account(user_accounts, account);

                HolidayAccountView view = to_view(account);
                view.day_hours = Math.Round(day_hours, 2, MidpointRounding.AwayFromZero);
                view.pending_hours = holiday_hours + (transfer?.hours ?? 0);
                view.unplanned_hours = view.available_holiday_hours - view.pending_hours;
                view.days_until_expiry = account.usage_period_end.DayNumber - today.DayNumber;
                view.pending_transfer_id = transfer?.id;
                view.pending_transfer_hours = transfer?.hours ?? 0;

                TransferOption option = get_transfer_option(account, next, today, day_hours, holiday_hours,
                    transfer_totals.GetValueOrDefault(account.id, TransferTotals.None),
                    user.end_date, has_pending_transfer: transfer != null);
                view.can_transfer = option.possible;
                view.transfer_max_hours = option.max_hours;
                view.transfer_deadline = option.deadline;
                view.transfer_unavailable_reason = option.reason;

                if (user.end_date != null)
                {
                    decimal still_earned = 0;
                    if (account.active)
                    {
                        still_earned = AccuralService.project_earned_total(account,
                            histories.GetValueOrDefault(account.id, new List<HolidayHistory>()),
                            user.end_date.Value, user_schedules) - account.get_earned();
                    }
                    view.flag = "leaving";
                    view.settlement_hours = view.available_holiday_hours + Math.Max(0, still_earned);
                }
                else if (!account.usable)
                {
                    view.flag = "expired_unused";
                }
                else if (view.unplanned_hours > 0 && account.usage_period_end <= today.AddMonths(EXPIRY_WARNING_MONTHS))
                {
                    view.flag = "plan_soon";
                }

                rows.Add(new EmployeeBalanceRow { user_id = user.id, name = user.name, end_date = user.end_date, account = view });
            }
        }

        // weeks 1-5 first, the account that expires first comes first (it should be used first)
        return rows
            .OrderBy(r => r.account.holiday_type)
            .ThenBy(r => r.account.usage_period_end)
            .ToList();
    }

    private static HolidayAccountView to_view(HolidayAccount account)
    {
        return new HolidayAccountView
        {
            id = account.id,
            holiday_type = account.holiday_type,
            active = account.active,
            usable = account.usable,
            earning_period_start = account.earning_period_start,
            earning_period_end = account.earning_period_end,
            usage_period_start = account.usage_period_start,
            usage_period_end = account.usage_period_end,
            earned_holiday_hours = account.get_earned(),
            used_holiday_hours = account.get_used(),
            converted_holiday_hours = account.converted_holiday_hours,
            transferred_holiday_hours = account.transferred_holiday_hours,
            available_holiday_hours = account.get_available()
        };
    }

    // the account of the same type and user whose earning period starts the day after this one's ends:
    // the holiday period a transfer goes to
    public static HolidayAccount? find_next_account(IEnumerable<HolidayAccount> user_accounts, HolidayAccount account)
    {
        DateOnly next_start = account.earning_period_end.AddDays(1);
        return user_accounts.FirstOrDefault(a => a.holiday_type == account.holiday_type
                                                 && a.user_id == account.user_id
                                                 && a.earning_period_start == next_start);
    }

    // the last day a transfer can be agreed, with the rules that apply today: the latest deadline day (weeks 1-5:
    // 31 December, week 6: 30 September) that is not after the end of the usage period.
    // weeks 1-5 used until 31.12.2027 -> 31.12.2027; week 6 used 01.05.2027 - 30.04.2028 -> 30.09.2027
    public static DateOnly get_transfer_deadline(HolidayAccount account, RuleSet rules)
    {
        bool law = account.holiday_type == HolidayType.FerieEfterFerieloven;
        int month = law ? rules.transfer_weeks1to5_deadline_month : rules.transfer_week6_deadline_month;
        int day = law ? rules.transfer_weeks1to5_deadline_day : rules.transfer_week6_deadline_day;

        DateOnly deadline = make_date(account.usage_period_end.Year, month, day);
        if (deadline > account.usage_period_end)
        {
            deadline = make_date(account.usage_period_end.Year - 1, month, day);
        }
        return deadline;
    }

    // every rule a transfer must follow. reserved_hours = hours of pending holiday requests on the account (a
    // transfer can not take hours that are already requested). has_pending_transfer = another transfer of this
    // account waits for the accountant (only one at a time). totals = the approved transfers of the account
    public static TransferOption get_transfer_option(HolidayAccount account, HolidayAccount? next, DateOnly today,
        decimal day_hours, decimal reserved_hours, TransferTotals totals, DateOnly? end_date, bool has_pending_transfer)
    {
        RuleSet rules = HolidayParameters.get_rule_set(today);
        if (!rules.transfer_allowed)
        {
            return new TransferOption(false, 0, null, "Transferring holiday to the next period is not allowed by the company's rules");
        }
        if (!account.usable)
        {
            return new TransferOption(false, 0, null, "The usage period of this balance is over");
        }

        DateOnly deadline = get_transfer_deadline(account, rules);
        if (account.active)
        {
            return new TransferOption(false, 0, deadline,
                "This balance is still being earned, it can be transferred once its earning period has ended");
        }
        if (today > deadline)
        {
            return new TransferOption(false, 0, deadline, $"The last day to agree on a transfer was {deadline:yyyy-MM-dd}");
        }
        if (end_date != null)
        {
            return new TransferOption(false, 0, deadline,
                $"The employment ends on {end_date:yyyy-MM-dd}, the remaining holiday is settled with the final salary");
        }
        if (next == null || !next.usable)
        {
            return new TransferOption(false, 0, deadline, "There is no next holiday period to transfer the hours to");
        }
        if (has_pending_transfer)
        {
            return new TransferOption(false, 0, deadline, "A transfer of this balance is already waiting for the accountant");
        }

        decimal max = get_transfer_max_hours(account, rules, day_hours, reserved_hours, totals);
        if (max <= 0)
        {
            string reason = totals.sent > 0
                ? $"{totals.sent:0.00} h of this balance are already transferred, that is the most that can be transferred"
                : account.holiday_type == HolidayType.FerieEfterFerieloven
                ? "Nothing can be transferred: only unplanned holiday beyond the first 4 weeks (the 5th week) can be transferred"
                : "Nothing can be transferred: every hour of this balance is used or requested";
            return new TransferOption(false, 0, deadline, reason);
        }
        return new TransferOption(true, max, deadline, null);
    }

    // at most transfer_max_days holiday days of the employee's schedule (1 week, full time 5 * 7.40 = 37 h), never
    // more than is left after the pending requests. weeks 1-5: only the part earned beyond the first 4 weeks.
    // example: 184.70 h earned (24.96 days), 4 weeks = 148 h -> at most 36.70 h, even if 60 h are left.
    // the limit is ONE week per account, whatever hours it is made of: hours received from the previous period by an
    // earlier transfer can be part of it, but they never raise the limit above one week. hours already transferred
    // FROM this account count against it. received 3 h, 40 h left -> at most 37 h (1 week), not 40 h
    public static decimal get_transfer_max_hours(HolidayAccount account, RuleSet rules, decimal day_hours, decimal reserved_hours,
        TransferTotals totals)
    {
        decimal already_sent = totals.sent;
        decimal week = Math.Round(rules.transfer_max_days * day_hours, 2, MidpointRounding.AwayFromZero);
        decimal max = Math.Min(week - already_sent, account.get_available() - reserved_hours);
        if (account.holiday_type == HolidayType.FerieEfterFerieloven)
        {
            decimal must_be_taken = Math.Round(LAW_DAYS_THAT_MUST_BE_TAKEN * day_hours, 2, MidpointRounding.AwayFromZero);
            // converted_holiday_hours keeps the earned days in the hours of the current schedule. hours received from the
            // previous period are not part of this year's 4 weeks, so they count as transferable
            // (+ received - sent = transferred_holiday_hours)
            decimal beyond_4_weeks = account.get_earned() + account.converted_holiday_hours + account.transferred_holiday_hours
                                     - must_be_taken;
            max = Math.Min(max, beyond_4_weeks);
        }
        return Math.Max(0, Math.Round(max, 2, MidpointRounding.AwayFromZero));
    }

    private static DateOnly make_date(int year, int month, int day)
    {
        return new DateOnly(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
    }
}
