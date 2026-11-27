using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

// the balance on a future date. a projection only: the same accrual as next_month (AccuralService.project_earned_total)
// applied to the months that will be over by then, nothing is written to the db
public class ProjectionService
{
    // how far ahead a projection can be made (the rules and schedules of the far future are only a guess anyway)
    public const int MAX_YEARS_AHEAD = 2;

    private readonly Database db;
    private readonly CurrentDateService currentDateService;

    public ProjectionService(Database db, CurrentDateService currentDateService)
    {
        this.db = db;
        this.currentDateService = currentDateService;
    }

    // throws InvalidOperationException if the date is in the past or too far ahead, KeyNotFoundException if the user
    // has no schedule / accounts
    public async Task<ProjectedBalanceView> get_projected_balance(Guid user_id, DateOnly date)
    {
        DateOnly today = await currentDateService.get_current_date();
        if (date < today)
        {
            throw new InvalidOperationException($"Choose today ({today:yyyy-MM-dd}) or a later date");
        }
        if (date > today.AddYears(MAX_YEARS_AHEAD))
        {
            throw new InvalidOperationException($"A balance can be projected at most {MAX_YEARS_AHEAD} years ahead");
        }

        List<Schedule> schedules = await db.schedules.Where(s => s.user_id == user_id).ToListAsync();
        Schedule? schedule_on_date = Schedule.find_for_date(schedules, date);
        if (schedule_on_date == null)
        {
            throw new KeyNotFoundException("The user has no working schedule");
        }

        // the accrual of a month is added when the month is over (next_month), so on `date` every month before the
        // month of `date` is accrued. a date on the 1st of a month: the month before is just over, also accrued
        DateOnly accrue_until = new DateOnly(date.Year, date.Month, 1).AddDays(-1);
        // an employee who leaves earns nothing after the last working day, and gets no new accounts after it
        DateOnly? end_date = await db.users.Where(u => u.id == user_id).Select(u => u.end_date).FirstOrDefaultAsync();
        if (end_date != null && accrue_until > end_date)
        {
            accrue_until = end_date.Value;
        }

        List<HolidayAccount> accounts = new List<HolidayAccount>();
        accounts.AddRange(await db.law_holidays.Where(l => l.user_id == user_id && l.usable).ToListAsync());
        accounts.AddRange(await db.agreed_holidays.Where(a => a.user_id == user_id && a.usable).ToListAsync());
        if (accounts.Count == 0)
        {
            throw new KeyNotFoundException("The user has no holiday accounts");
        }

        List<Guid> ids = accounts.Select(a => a.id).ToList();
        Dictionary<Guid, List<HolidayHistory>> histories = (await db.law_holidays_history
                .Where(h => ids.Contains(h.account_id)).ToListAsync())
            .Cast<HolidayHistory>()
            .Concat((await db.agreed_holidays_history.Where(h => ids.Contains(h.account_id)).ToListAsync())
                .Cast<HolidayHistory>())
            .GroupBy(h => h.account_id)
            .ToDictionary(g => g.Key, g => g.ToList());

        // pending holiday requests and pending transfers both reserve hours of their account
        Dictionary<Guid, decimal> pending = (await db.HolidayRequest
                .Where(r => r.user_id == user_id && r.RequestStatus == RequestStatus.Pending)
                .Select(r => new { r.account_id, r.Hours })
                .ToListAsync())
            .Concat((await db.transfer_requests
                    .Where(t => t.user_id == user_id && t.status == RequestStatus.Pending)
                    .ToListAsync())
                .Select(t => new { account_id = t.from_account_id, Hours = t.hours }))
            .GroupBy(r => r.account_id)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Hours));

        List<ProjectedAccountView> views = new List<ProjectedAccountView>();
        foreach (HolidayAccount account in accounts)
        {
            List<HolidayHistory> account_histories = histories.GetValueOrDefault(account.id, new List<HolidayHistory>());
            decimal future = account.active
                ? AccuralService.project_earned_total(account, account_histories, accrue_until, schedules) - account.get_earned()
                : 0;
            if (future < 0)
            {
                future = 0;
            }
            decimal pending_hours = pending.GetValueOrDefault(account.id, 0);
            views.Add(make_view(account, account.id, account.get_available(), pending_hours, future, date));
        }

        // accounts that don't exist yet: every earning period that starts on or before the date gets a new account
        // when the one before it ends (AccuralService.start_new_earning_periods), with the rules of that day
        foreach (HolidayType type in new[] { HolidayType.FerieEfterFerieloven, HolidayType.FerieUge })
        {
            HolidayAccount? latest = accounts.Where(a => a.holiday_type == type)
                .OrderByDescending(a => a.earning_period_start)
                .FirstOrDefault();
            if (latest == null)
            {
                continue;
            }

            DateOnly next_start = latest.earning_period_end.AddDays(1);
            while (next_start <= date && (end_date == null || next_start <= end_date))
            {
                var (earning_start, earning_end, usage_start, usage_end) = HolidayParameters.get_account_dates(type, next_start);
                HolidayAccount future_account = type == HolidayType.FerieEfterFerieloven ? new LawHolidays() : new AgreedHolidays();
                future_account.user_id = user_id;
                future_account.holiday_type = type;
                future_account.earning_period_start = earning_start;
                future_account.earning_period_end = earning_end;
                future_account.usage_period_start = usage_start;
                future_account.usage_period_end = usage_end;
                future_account.active = true;

                decimal earned = AccuralService.project_earned_total(future_account, new List<HolidayHistory>(),
                    accrue_until, schedules);
                views.Add(make_view(future_account, null, 0, 0, earned, date));
                next_start = earning_end.AddDays(1);
            }
        }

        decimal day_hours = schedule_on_date.get_average_day_hours();
        return new ProjectedBalanceView
        {
            date = date,
            today = today,
            day_hours = day_hours,
            law_hours = views.Where(v => v.state == "usable" && v.holiday_type == HolidayType.FerieEfterFerieloven)
                .Sum(v => v.projected_hours),
            agreed_hours = views.Where(v => v.state == "usable" && v.holiday_type == HolidayType.FerieUge)
                .Sum(v => v.projected_hours),
            debt_hours = get_debt_hours(views, accounts, histories, schedules, date, accrue_until, day_hours, end_date),
            accounts = views
                .OrderBy(v => v.holiday_type)
                .ThenBy(v => v.usage_period_end)
                .ToList()
        };
    }

    private static ProjectedAccountView make_view(HolidayAccount account, Guid? id, decimal available_now,
        decimal pending_hours, decimal future_accrual, DateOnly date)
    {
        string state = date < account.usage_period_start ? "not_yet"
            : date > account.usage_period_end ? "expired"
            : "usable";
        return new ProjectedAccountView
        {
            id = id,
            holiday_type = account.holiday_type,
            earning_period_start = account.earning_period_start,
            earning_period_end = account.earning_period_end,
            usage_period_start = account.usage_period_start,
            usage_period_end = account.usage_period_end,
            available_now = available_now,
            pending_hours = pending_hours,
            future_accrual = future_accrual,
            projected_hours = available_now - pending_hours + future_accrual,
            state = state
        };
    }

    // holiday debt (RequestValidator.check_balance): weeks 1-5 that are still being earned on the date may go below 0
    // by max_holiday_debt_days, and must be paid back by what is earned until the end of the earning period
    // (31 August). so on the date the employee can borrow at most min(1 week, what is still earned after the date),
    // minus what is already borrowed
    private static decimal get_debt_hours(List<ProjectedAccountView> views, List<HolidayAccount> accounts,
        Dictionary<Guid, List<HolidayHistory>> histories, List<Schedule> schedules, DateOnly date,
        DateOnly accrue_until, decimal day_hours, DateOnly? end_date)
    {
        ProjectedAccountView? earning = views.FirstOrDefault(v => v.holiday_type == HolidayType.FerieEfterFerieloven
                                                                  && v.state == "usable"
                                                                  && v.earning_period_start <= date
                                                                  && date <= v.earning_period_end);
        if (earning == null)
        {
            return 0;
        }

        HolidayAccount account = earning.id != null
            ? accounts.First(a => a.id == earning.id)
            : new LawHolidays
            {
                holiday_type = HolidayType.FerieEfterFerieloven,
                earning_period_start = earning.earning_period_start,
                earning_period_end = earning.earning_period_end,
                active = true
            };
        List<HolidayHistory> account_histories = earning.id != null
            ? histories.GetValueOrDefault(earning.id.Value, new List<HolidayHistory>())
            : new List<HolidayHistory>();

        DateOnly earning_end = end_date != null && end_date < account.earning_period_end ? end_date.Value : account.earning_period_end;
        decimal earned_by_end = AccuralService.project_earned_total(account, account_histories, earning_end, schedules);
        decimal earned_by_date = AccuralService.project_earned_total(account, account_histories, accrue_until, schedules);
        decimal earned_after = earned_by_end - earned_by_date;

        RuleSet rules = HolidayParameters.get_rule_set(date);
        decimal max_debt = Math.Round(rules.max_holiday_debt_days * day_hours, 2, MidpointRounding.AwayFromZero);
        // debt already taken (a negative balance) is part of the limit
        decimal already_borrowed = Math.Max(0, -earning.projected_hours);
        return Math.Max(0, Math.Min(max_debt, earned_after) - already_borrowed);
    }
}
