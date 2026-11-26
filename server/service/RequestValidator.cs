using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

// the result of a valid request: the account it is spent from, its cost in hours (calculated by the server,
// never sent by the client) and whether it is a main holiday (hovedferie)
public record ValidatedRequest(HolidayAccount account, decimal hours, bool is_main_holiday);

// every holiday rule a request must follow, in one place. used when a request is sent, when its dates are changed
// (Edit) and again when the accountant approves it (the balance may have changed since it was sent).
// a broken rule throws InvalidOperationException with a message for the employee / accountant
public class RequestValidator
{
    private readonly Database db;
    private readonly CurrentDateService currentDateService;

    public RequestValidator(Database db, CurrentDateService currentDateService)
    {
        this.db = db;
        this.currentDateService = currentDateService;
    }

    // ignore_request_id: a request that must not be counted against this one (the request itself when it is approved).
    // check_notice: false when the accountant approves: the notice is about the moment the request was SENT,
    // otherwise a slow approval would make a correct request too late
    public async Task<ValidatedRequest> validate(Guid user_id, Guid account_id, DateOnly start, DateOnly end,
        Guid? ignore_request_id, bool check_notice)
    {
        if (end < start)
        {
            throw new InvalidOperationException("The holiday must end on or after the day it starts");
        }

        // the account must belong to the user and must not be expired. "active" (still earning) does not matter here:
        // week 6 can only be used after its earning year, and weeks 1-5 also 4 months after their earning period ended
        HolidayAccount? account =
            (HolidayAccount?)await db.law_holidays.FirstOrDefaultAsync(l =>
                l.id == account_id && l.user_id == user_id && l.usable)
            ?? await db.agreed_holidays.FirstOrDefaultAsync(a =>
                a.id == account_id && a.user_id == user_id && a.usable);
        if (account == null)
        {
            throw new InvalidOperationException("No usable holiday account was found for this user");
        }

        // "today" is the simulated date (time simulator), not DateTime.Now
        DateOnly today = await currentDateService.get_current_date();

        // the holiday itself (not the day the request is sent) must lie inside the usage period of the account
        if (!account.covers(start, end))
        {
            throw new InvalidOperationException(
                $"This balance can only be used from {account.usage_period_start:yyyy-MM-dd} to {account.usage_period_end:yyyy-MM-dd}");
        }

        // every day costs the hours of that weekday in the employee's own schedule that applies on that day
        List<Schedule> schedules = await db.schedules.Where(s => s.user_id == user_id).ToListAsync();
        HashSet<DateOnly> days_off = (await db.days_off
                .Where(d => d.date >= start && d.date <= end)
                .Select(d => d.date)
                .ToListAsync())
            .ToHashSet();

        decimal hours = RequestService.calculate_holiday_hours(start, end, schedules, days_off);
        if (hours == 0)
        {
            throw new InvalidOperationException(
                "The selected days contain no working days (weekends, public holidays and your days off are not holiday)");
        }

        bool is_main_holiday = is_main(account, start, end, schedules);

        check_start(account, start, today, is_main_holiday, check_notice);
        await check_overlap(user_id, start, end, ignore_request_id);
        await check_balance(account, hours, schedules, today, ignore_request_id);

        return new ValidatedRequest(account, hours, is_main_holiday);
    }

    // main holiday (hovedferie): weeks 1-5 holiday with at least main_holiday_length_days (15) holiday days inside the
    // main holiday window (1 May - 30 Sep). one holiday day = 1/5 of the employee's working week, so the limit is
    // 3 weeks of the employee's OWN schedule: full time 15 * 7.40 = 111 h = 3 * 37 h, part time 22.5 h/week -> 67.5 h.
    // hours, not working days: a part timer working 3 days a week would need 5 weeks for "15 working days".
    // public holidays inside the block are counted with their normal hours here: they don't cost holiday, but they
    // don't break the block either, so 3 whole weeks with Grundlovsdag inside are still a main holiday.
    // the window and length are the rules of the holiday's own date (the summer it lies in).
    // it does not block anything (taking the main holiday is the employee's right), it decides the notice
    public static bool is_main(HolidayAccount account, DateOnly start, DateOnly end, List<Schedule> schedules)
    {
        if (account.holiday_type != HolidayType.FerieEfterFerieloven)
        {
            return false;
        }

        RuleSet rule_set = HolidayParameters.get_rule_set(start);
        DateOnly window_start = new DateOnly(start.Year, rule_set.main_holiday_start_month, 1);
        DateOnly window_end = HolidayParameters.last_day_of_month(start.Year, rule_set.main_holiday_end_month);

        DateOnly from = start > window_start ? start : window_start;
        DateOnly to = end < window_end ? end : window_end;
        if (from > to)
        {
            return false;
        }

        Schedule? schedule = Schedule.find_for_date(schedules, from);
        if (schedule == null)
        {
            return false;
        }

        decimal hours_in_window = RequestService.calculate_holiday_hours(from, to, schedules, new HashSet<DateOnly>());
        decimal limit = Math.Round(rule_set.main_holiday_length_days * schedule.get_average_day_hours(), 2,
            MidpointRounding.AwayFromZero);
        return limit > 0 && hours_in_window >= limit;
    }

    // notice: the holiday must be requested main_holiday_notice_months (3) ahead for the main holiday and
    // other_holiday_notice_months (1) ahead for other weeks 1-5 holiday, with the rules that apply on the day the
    // request is sent. week 6 is placed at the employee's request, so it has no notice: it just can't be in the past
    private static void check_start(HolidayAccount account, DateOnly start, DateOnly today, bool is_main_holiday, bool check_notice)
    {
        if (start < today)
        {
            throw new InvalidOperationException($"A holiday can not start in the past (today is {today:yyyy-MM-dd})");
        }

        if (!check_notice || account.holiday_type == HolidayType.FerieUge)
        {
            return;
        }

        RuleSet rule_set = HolidayParameters.get_rule_set(today);
        int months = is_main_holiday ? rule_set.main_holiday_notice_months : rule_set.other_holiday_notice_months;
        DateOnly earliest = today.AddMonths(months);
        if (start < earliest)
        {
            string kind = is_main_holiday ? "A main holiday (3 weeks in the summer)" : "This holiday";
            string unit = months == 1 ? "month" : "months";
            throw new InvalidOperationException(
                $"{kind} must be requested {months} {unit} in advance: it can start on {earliest:yyyy-MM-dd} at the earliest");
        }
    }

    // the same days can not be requested twice: no pending or approved request of the user (from any account)
    // may overlap. rejected, cancelled and withdrawn requests don't count
    private async Task check_overlap(Guid user_id, DateOnly start, DateOnly end, Guid? ignore_request_id)
    {
        List<HolidayRequest> requests = await db.HolidayRequest
            .Where(r => r.user_id == user_id
                        && (r.RequestStatus == RequestStatus.Pending || r.RequestStatus == RequestStatus.Accepted)
                        && (ignore_request_id == null || r.id != ignore_request_id))
            .ToListAsync();

        foreach (HolidayRequest other in requests)
        {
            DateOnly other_start = DateOnly.ParseExact(other.StartDate, "yyyy-MM-dd");
            DateOnly other_end = DateOnly.ParseExact(other.EndDate, "yyyy-MM-dd");
            if (other_start <= end && other_end >= start)
            {
                string status = other.RequestStatus == RequestStatus.Accepted ? "an approved" : "a pending";
                throw new InvalidOperationException(
                    $"These dates overlap {status} holiday from {other.StartDate} to {other.EndDate}");
            }
        }
    }

    // the balance of the account must cover the request. hours of PENDING requests on the same account are
    // reserved too, otherwise several requests could together spend more than the balance before any is approved.
    // holiday debt: an account that is still being earned (weeks 1-5, active) may go below 0 by at most
    // max_holiday_debt_days (1 week), and must be 0 or positive again on 31 August (see check_balance_on_deadline).
    // week 6 and accounts that finished earning get nothing more, so no debt there
    private async Task check_balance(HolidayAccount account, decimal hours, List<Schedule> schedules, DateOnly today,
        Guid? ignore_request_id)
    {
        decimal reserved = await db.HolidayRequest
            .Where(r => r.account_id == account.id && r.RequestStatus == RequestStatus.Pending
                        && (ignore_request_id == null || r.id != ignore_request_id))
            .SumAsync(r => (decimal?)r.Hours) ?? 0;

        decimal available = account.get_available() - reserved;
        bool debt_possible = account.holiday_type == HolidayType.FerieEfterFerieloven && account.active;

        decimal allowed_debt = 0;
        if (debt_possible)
        {
            Schedule? schedule = Schedule.find_for_date(schedules, today);
            if (schedule != null)
            {
                // one week = max_holiday_debt_days holiday days of the employee's own schedule (full time 5 * 7.40 = 37 h)
                RuleSet rule_set = HolidayParameters.get_rule_set(today);
                allowed_debt = Math.Round(rule_set.max_holiday_debt_days * schedule.get_average_day_hours(), 2,
                    MidpointRounding.AwayFromZero);
            }
        }

        if (available - hours < -allowed_debt)
        {
            string debt_text = allowed_debt > 0 ? $" + {allowed_debt:0.00} h of allowed holiday debt" : "";
            string reserved_text = reserved > 0 ? $" ({reserved:0.00} h are reserved by your pending requests)" : "";
            throw new InvalidOperationException(
                $"Not enough holiday: the request costs {hours:0.00} h, you have {available:0.00} h{debt_text}{reserved_text}");
        }

        if (debt_possible)
        {
            await check_balance_on_deadline(account, available - hours, schedules, today);
        }
    }

    // holiday debt must be paid back by the holiday earned until 31 August (non_negative_balance_month / day):
    // the balance after this request + everything the account will still earn until then must be 0 or positive.
    // the future months are accrued exactly like AccuralService will do it (the schedule and rules of every month,
    // the total rounded once), so the projection matches the real balance on that day
    private async Task check_balance_on_deadline(HolidayAccount account, decimal balance_after, List<Schedule> schedules, DateOnly today)
    {
        RuleSet rule_set = HolidayParameters.get_rule_set(today);
        DateOnly deadline = next_date(today, rule_set.non_negative_balance_month, rule_set.non_negative_balance_day);
        DateOnly accrue_until = deadline < account.earning_period_end ? deadline : account.earning_period_end;

        List<LawHolidaysHistory> histories = await db.law_holidays_history
            .Where(h => h.account_id == account.id)
            .ToListAsync();
        if (histories.Count == 0)
        {
            return;
        }

        // the newest history is the month that is accrued next (accrued = 0), every month before it is in earned
        DateOnly open_start = histories.Max(h => h.period_start);

        decimal exact_total = 0;
        foreach (LawHolidaysHistory h in histories.Where(h => h.period_start < open_start))
        {
            exact_total += AccuralService.get_exact_accrual(account, h.period_start, h.period_end, schedules);
        }

        for (DateOnly month_start = open_start; month_start <= accrue_until;
             month_start = new DateOnly(month_start.Year, month_start.Month, 1).AddMonths(1))
        {
            DateOnly month_end = HolidayParameters.last_day_of_month(month_start.Year, month_start.Month);
            if (month_end > accrue_until)
            {
                month_end = accrue_until;
            }
            exact_total += AccuralService.get_exact_accrual(account, month_start, month_end, schedules);
        }

        decimal still_earned = Math.Round(exact_total, 2, MidpointRounding.AwayFromZero) - account.get_earned();
        decimal projected = balance_after + still_earned;
        if (projected < 0)
        {
            throw new InvalidOperationException(
                $"Not enough holiday: on {deadline:yyyy-MM-dd} your balance would be {projected:0.00} h " +
                $"(you will earn {still_earned:0.00} h more until then). Holiday debt must be paid back, " +
                $"the balance must be 0 or positive on {deadline:yyyy-MM-dd}");
        }
    }

    // the first date on or after today with the given month and day (e.g. 31 August)
    private static DateOnly next_date(DateOnly today, int month, int day)
    {
        DateOnly date = new DateOnly(today.Year, month, Math.Min(day, DateTime.DaysInMonth(today.Year, month)));
        if (date < today)
        {
            date = new DateOnly(today.Year + 1, month, Math.Min(day, DateTime.DaysInMonth(today.Year + 1, month)));
        }
        return date;
    }
}
