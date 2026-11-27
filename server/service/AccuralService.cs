using be;
using database;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;

namespace service;

public class AccuralService
{
    private Database db;
    private CurrentDateService currentDateService;

    public AccuralService(Database db, CurrentDateService currentDateService)
    {
        this.db = db;
        this.currentDateService = currentDateService;
    }

    public async Task accrue_holidays()
    {
        var transaction = await db.BeginTransactionAsync();
        try
        {
            List<LawHolidays> law_holidays_with_history = (await get_holidays_with_history(HolidayType.FerieEfterFerieloven))
                .Cast<LawHolidays>()
                .ToList();
            List<AgreedHolidays> agreed_holidays_with_history = (await get_holidays_with_history(HolidayType.FerieUge))
                .Cast<AgreedHolidays>()
                .ToList();

            // working schedules of all users with an active account, the accrued hours depend on them
            Dictionary<Guid, List<Schedule>> schedules_by_user = await get_schedules_by_user(law_holidays_with_history, agreed_holidays_with_history);

            await accrue_holidays_in_histories<LawHolidaysHistory>(law_holidays_with_history, schedules_by_user);
            await accrue_holidays_in_histories<AgreedHolidaysHistory>(agreed_holidays_with_history, schedules_by_user);
        
            update_accrue_holidays(law_holidays_with_history);
            update_accrue_holidays(agreed_holidays_with_history);

            // last working days of the users who leave the company (fratræden), null while employed
            Dictionary<Guid, DateOnly?> end_dates = await get_end_dates(law_holidays_with_history, agreed_holidays_with_history);

            // accounts of users whose last working day was in the month that was just accrued: they earned everything,
            // they stop earning and get no new account
            List<HolidayAccount> left_accounts = get_left_accounts(law_holidays_with_history, end_dates)
                .Concat(get_left_accounts(agreed_holidays_with_history, end_dates))
                .ToList();
            HashSet<Guid> left_ids = left_accounts.Select(a => a.id).ToHashSet();

            // accounts whose earning period ends with the month that was just accrued
            List<LawHolidays> ended_law_holidays = get_accounts_with_ended_earning_period(law_holidays_with_history)
                .Where(a => !left_ids.Contains(a.id)).ToList();
            List<AgreedHolidays> ended_agreed_holidays = get_accounts_with_ended_earning_period(agreed_holidays_with_history)
                .Where(a => !left_ids.Contains(a.id)).ToList();

            // accounts with ended earning period are skipped here, they get a new account instead
            await create_new_history_records<LawHolidaysHistory>(law_holidays_with_history, end_dates);
            await create_new_history_records<AgreedHolidaysHistory>(agreed_holidays_with_history, end_dates);

            await update_holiday_accounts<LawHolidays>(law_holidays_with_history, nameof(LawHolidays.earned_holiday_hours));
            await update_holiday_accounts<AgreedHolidays>(agreed_holidays_with_history, nameof(AgreedHolidays.earned_holiday_hours));

            await start_new_earning_periods<LawHolidays, LawHolidaysHistory>(ended_law_holidays, HolidayType.FerieEfterFerieloven);
            await start_new_earning_periods<AgreedHolidays, AgreedHolidaysHistory>(ended_agreed_holidays, HolidayType.FerieUge);
            await stop_earning(left_accounts);

            // the new histories are saved, so the current date is now the 1st of the next month.
            // accounts whose usage period ended before it can not be used anymore
            DateOnly today = await currentDateService.get_current_date();
            await expire_accounts<LawHolidays>(today);
            await expire_accounts<AgreedHolidays>(today);
            // a transfer can only be agreed until its deadline
            await TransferService.decline_overdue(db, today);

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            await transaction.RollbackAsync();
            throw;
        }
        

        
        
        

    }

    // new() lets us create THistory (LawHolidaysHistory / AgreedHolidaysHistory) via its parameterless constructor
    // end_dates: a user who leaves gets no history after their last working day, the last one ends on that day
    private async Task create_new_history_records<THistory>(IEnumerable<HolidayAccount> holidays, Dictionary<Guid, DateOnly?> end_dates)
        where THistory : HolidayHistory, new()
    {
        List<THistory> new_histories = new List<THistory>();
        foreach (var holiday_acc in holidays)
        {
            THistory h = (THistory) holiday_acc.get__holiday_history();
            if (h == null)
            {
                continue;
            }

            // earning period of this account is over, the next month belongs to a new account
            if (is_earning_period_ended(holiday_acc))
            {
                continue;
            }

            // first day of the month after the last history period
            DateOnly next_start = new DateOnly(h.period_end.Year, h.period_end.Month, 1).AddMonths(1);
            DateOnly next_end = next_start.AddMonths(1).AddDays(-1);

            // if the earning period ends before the end of the month, the history stops there
            // (only the days inside the earning period are accrued)
            if (next_end > holiday_acc.earning_period_end)
            {
                next_end = holiday_acc.earning_period_end;
            }

            // the user leaves: nothing is earned after the last working day
            DateOnly? end_date = end_dates.GetValueOrDefault(holiday_acc.user_id);
            if (end_date != null)
            {
                if (next_start > end_date)
                {
                    continue;
                }
                if (next_end > end_date)
                {
                    next_end = end_date.Value;
                }
            }

            new_histories.Add(new THistory
            {
                account_id = holiday_acc.id,
                period_start = next_start,
                period_end = next_end,
                accrued = 0
            });
        }

        if (new_histories.Count == 0)
        {
            return;
        }

        // GetTable<THistory>() resolves the table from the mapping:
        // LawHolidaysHistory -> law_holidays_history, AgreedHolidaysHistory -> agreed_holidays_history
        await db.GetTable<THistory>().BulkCopyAsync(new_histories);
    }

    private void update_accrue_holidays<THoliday>(IEnumerable<THoliday> holidays) where THoliday : HolidayAccount
    {
        foreach (var holiday in holidays)
        {
            HolidayHistory hist = holiday.get__holiday_history();
            if (hist == null)
            {
                continue;
            }
            holiday.add_earned(holiday.get__holiday_history().accrued);
        }
    }

    // THoliday defines the table the accounts are updated in (LawHolidays -> law_holidays, AgreedHolidays -> agreed_holidays)
    // earned_column defines the column, because it is different for every holiday type (both are earned_holiday_hours now, but the tables differ)
    private async Task update_holiday_accounts<THoliday>(IEnumerable<HolidayAccount> holidays, string earned_column)
        where THoliday : HolidayAccount
    {
        List<THoliday> holiday_accounts = new List<THoliday>();
        foreach (var holiday in holidays)
        {
            // accounts without history were not changed in update_accrue_holidays
            if (holiday.get__holiday_history() == null)
            {
                continue;
            }

            holiday_accounts.Add((THoliday)holiday);
        }

        // batch update by id: one UPDATE ... FROM (VALUES (id, earned), ...) for all accounts
        List<AccrualRow> rows = holiday_accounts
            .Select(h => new AccrualRow { id = h.id, accrued = h.get_earned() })
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        await (from h in db.GetTable<THoliday>()
               join r in rows on h.id equals r.id
               select new { h, r })
            .Set(x => Sql.Property<decimal>(x.h, earned_column), x => x.r.accrued)
            .UpdateAsync();
    }

    // THistory defines the table the histories are updated in (LawHolidaysHistory -> law_holidays_history, ...)
    //
    // rounding: rounding every month on its own loses up to 0.005 h a month (week 6: 3.083 -> 3.08, 12 x 3.08 = 36.96
    // instead of 37). so the month gets the difference between the ROUNDED EXACT TOTAL of the account up to and
    // including this month and what was already accrued (earned). every stored value still has two decimals, but the
    // total of the account is always the exact total rounded once (12 x 3.083 = 36.996 -> 37.00).
    // the exact total of the earlier months is not stored, it is recalculated from their histories with the schedule
    // and the rules that applied in those months (both are never rewritten, so the result is always the same)
    private async Task accrue_holidays_in_histories<THistory>(IEnumerable<HolidayAccount> holidays, Dictionary<Guid, List<Schedule>> schedules_by_user)
        where THistory : HolidayHistory
    {
        List<HolidayAccount> accounts = holidays.Where(h => h.get__holiday_history() != null).ToList();
        if (accounts.Count == 0)
        {
            return;
        }

        // all histories of these accounts with one query, grouped by account
        List<Guid> account_ids = accounts.Select(a => a.id).ToList();
        Dictionary<Guid, List<THistory>> histories_by_account = (await db.GetTable<THistory>()
                .Where(h => account_ids.Contains(h.account_id))
                .ToListAsync())
            .GroupBy(h => h.account_id)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<THistory> holiday_histories = new List<THistory>();
        foreach (var holiday in accounts)
        {
            THistory history = (THistory)holiday.get__holiday_history();
            List<Schedule> user_schedules = schedules_by_user.GetValueOrDefault(holiday.user_id, new List<Schedule>());

            // exact (not rounded) hours of all earlier months of the account + this month
            decimal exact_total = get_exact_accrual(holiday, history, user_schedules);
            foreach (THistory earlier in histories_by_account.GetValueOrDefault(holiday.id, new List<THistory>()))
            {
                if (earlier.period_start < history.period_start)
                {
                    exact_total += get_exact_accrual(holiday, earlier, user_schedules);
                }
            }

            // get_earned() = what was accrued before this month (it is increased only after this method)
            history.accrued = Math.Round(exact_total, 2, MidpointRounding.AwayFromZero) - holiday.get_earned();
            holiday_histories.Add(history);
        }

        // batch update by id: one UPDATE ... FROM (VALUES (id, accrued), ...) for all histories
        List<AccrualRow> rows = holiday_histories
            .Select(h => new AccrualRow { id = h.id, accrued = h.accrued })
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        await (from h in db.GetTable<THistory>()
               join r in rows on h.id equals r.id
               select new { h, r })
            .Set(x => x.h.accrued, x => x.r.accrued)
            .UpdateAsync();
    }

    // exact (not rounded) hours one history period earns: the month rate with the schedule and the rules that applied
    // in that month, for the days of the month the period covers (a mid-month start earns only the days worked)
    private static decimal get_exact_accrual(HolidayAccount holiday, HolidayHistory history, List<Schedule> user_schedules)
    {
        return get_exact_accrual(holiday, history.period_start, history.period_end, user_schedules);
    }

    // the same for any period inside ONE month (period_start and period_end in the same month, both included).
    // public, because the balance projection (RequestValidator, balance on 31 August) must accrue future months
    // exactly the way the real accrual will do it
    public static decimal get_exact_accrual(HolidayAccount holiday, DateOnly period_start, DateOnly period_end, List<Schedule> user_schedules)
    {
        // the schedule that applied in that month (schedule history: a change later must not affect this month)
        Schedule? schedule = Schedule.find_for_date(user_schedules, period_start);
        if (schedule == null)
        {
            // without a schedule the hours can not be calculated, accruing 0 would silently lose a month
            throw new Exception($"User {holiday.user_id} has no working schedule on {period_start}, holidays can not be accrued");
        }

        // the rules that applied in that month (a later change of the rules must not affect this month)
        RuleSet rule_set = HolidayParameters.get_rule_set(period_start);
        decimal month_rate = holiday.get_month_rate_hours(schedule, rule_set);

        // +1 because both period_start and period_end are included (13..30 = 18 days)
        int days = period_end.DayNumber - period_start.DayNumber + 1;
        return month_rate * days / DateTime.DaysInMonth(period_start.Year, period_start.Month);
    }

    // the earned total (rounded, like earned_holiday_hours) the account will have once every day up to and including
    // accrue_until is accrued: the months already accrued + the open month + the future months, each with the schedule
    // and rules of its own month, the total rounded once - exactly what next_month will store.
    // histories = all histories of the account; empty for an account that does not exist yet (a future earning
    // period), then accrual starts on its earning_period_start. never accrues past earning_period_end.
    // used by the 31 August debt check (RequestValidator) and the balance on a future date (ProjectionService)
    public static decimal project_earned_total(HolidayAccount account, IReadOnlyCollection<HolidayHistory> histories,
        DateOnly accrue_until, List<Schedule> schedules)
    {
        if (accrue_until > account.earning_period_end)
        {
            accrue_until = account.earning_period_end;
        }

        // the newest history is the month that is accrued next (accrued = 0), every month before it is in earned
        DateOnly open_start = histories.Count > 0 ? histories.Max(h => h.period_start) : account.earning_period_start;

        decimal exact_total = 0;
        foreach (HolidayHistory h in histories.Where(h => h.period_start < open_start))
        {
            exact_total += get_exact_accrual(account, h.period_start, h.period_end, schedules);
        }

        for (DateOnly month_start = open_start; month_start <= accrue_until;
             month_start = new DateOnly(month_start.Year, month_start.Month, 1).AddMonths(1))
        {
            DateOnly month_end = HolidayParameters.last_day_of_month(month_start.Year, month_start.Month);
            if (month_end > accrue_until)
            {
                month_end = accrue_until;
            }
            exact_total += get_exact_accrual(account, month_start, month_end, schedules);
        }

        return Math.Round(exact_total, 2, MidpointRounding.AwayFromZero);
    }

    // earning period is over when the last history month reached the end of the earning period
    // (law: 31 August, agreed: 31 December)
    private bool is_earning_period_ended(HolidayAccount holiday)
    {
        HolidayHistory history = holiday.get__holiday_history();
        if (history == null)
        {
            return false;
        }

        return history.period_end >= holiday.earning_period_end;
    }

    private List<THoliday> get_accounts_with_ended_earning_period<THoliday>(List<THoliday> holidays)
        where THoliday : HolidayAccount
    {
        List<THoliday> ended_accounts = new List<THoliday>();
        foreach (var holiday in holidays)
        {
            if (is_earning_period_ended(holiday))
            {
                ended_accounts.Add(holiday);
            }
        }
        return ended_accounts;
    }

    // old accounts stop earning, every user gets a new account of the same type with the next earning period
    // THoliday defines the accounts table (LawHolidays -> law_holidays, AgreedHolidays -> agreed_holidays)
    // THistory defines the histories table (LawHolidaysHistory -> law_holidays_history, ...)
    // h_type defines the usage period of the new account, because it is different for every holiday type
    private async Task start_new_earning_periods<THoliday, THistory>(List<THoliday> ended_accounts, HolidayType h_type)
        where THoliday : HolidayAccount, new()
        where THistory : HolidayHistory, new()
    {
        if (ended_accounts.Count == 0)
        {
            return;
        }

        // old accounts are not deleted, the employee can still use them until usage_period_end
        List<Guid> ended_ids = new List<Guid>();
        foreach (var old_account in ended_accounts)
        {
            ended_ids.Add(old_account.id);
        }

        await db.GetTable<THoliday>()
            .Where(h => ended_ids.Contains(h.id))
            .Set(h => h.active, false)
            .UpdateAsync();

        List<THoliday> new_accounts = new List<THoliday>();
        List<Guid> user_ids = new List<Guid>();
        foreach (var old_account in ended_accounts)
        {
            // the new earning period starts the day after the old one ended,
            // its dates come from the rules that apply on that day
            var (earning_start, earning_end, usage_start, usage_end) =
                HolidayParameters.get_account_dates(h_type, old_account.earning_period_end.AddDays(1));

            // new() lets us create THoliday via its parameterless constructor,
            // earned and used holidays stay 0 by default
            new_accounts.Add(new THoliday
            {
                user_id = old_account.user_id,
                earning_period_start = earning_start,
                earning_period_end = earning_end,
                usage_period_start = usage_start,
                usage_period_end = usage_end,
                holiday_type = h_type,
                active = true
            });
            user_ids.Add(old_account.user_id);
        }

        // one batch insert for all new accounts (id is generated by the db)
        await db.GetTable<THoliday>().BulkCopyAsync(new_accounts);

        // BulkCopy does not return the generated ids, so we read the new accounts back.
        // old accounts are already inactive, so the only active account of these users is the new one
        List<THoliday> saved_accounts = await db.GetTable<THoliday>()
            .Where(h => h.active && user_ids.Contains(h.user_id))
            .ToListAsync();

        List<THistory> first_histories = new List<THistory>();
        foreach (var saved_account in saved_accounts)
        {
            DateOnly history_start = saved_account.earning_period_start;
            DateOnly history_end = new DateOnly(history_start.Year, history_start.Month,
                DateTime.DaysInMonth(history_start.Year, history_start.Month));
            if (history_end > saved_account.earning_period_end)
            {
                history_end = saved_account.earning_period_end;
            }

            first_histories.Add(new THistory
            {
                account_id = saved_account.id,
                period_start = history_start,
                period_end = history_end,
                accrued = 0
            });
        }

        // one batch insert for all first histories
        await db.GetTable<THistory>().BulkCopyAsync(first_histories);
    }

    // usable = false for every account whose usage period is over (law: 31 December, week 6: 30 April).
    // the account itself and its balance are kept: what happens with the remaining hours (transfer by an approved
    // transfer request, payout by payroll, holiday fund) is decided outside of this flag
    // THoliday defines the table (LawHolidays -> law_holidays, AgreedHolidays -> agreed_holidays)
    private async Task expire_accounts<THoliday>(DateOnly today) where THoliday : HolidayAccount
    {
        await db.GetTable<THoliday>()
            .Where(h => h.usable && h.usage_period_end < today)
            .Set(h => h.usable, false)
            .UpdateAsync();
    }

    // last working day of every user of the given accounts (null while employed)
    private async Task<Dictionary<Guid, DateOnly?>> get_end_dates(params IEnumerable<HolidayAccount>[] account_lists)
    {
        List<Guid> user_ids = account_lists.SelectMany(list => list).Select(a => a.user_id).Distinct().ToList();
        return await db.users
            .Where(u => user_ids.Contains(u.id))
            .ToDictionaryAsync(u => u.id, u => u.end_date);
    }

    // accounts whose last accrued month reached the user's last working day (UserService.set_end_date makes the open
    // month end on that day, create_new_history_records never goes past it)
    private static List<HolidayAccount> get_left_accounts(IEnumerable<HolidayAccount> holidays, Dictionary<Guid, DateOnly?> end_dates)
    {
        return holidays
            .Where(h => h.get__holiday_history() != null)
            .Where(h =>
            {
                DateOnly? end_date = end_dates.GetValueOrDefault(h.user_id);
                return end_date != null && h.get__holiday_history().period_end >= end_date;
            })
            .ToList();
    }

    // the accounts of a user who left stop earning (active = false). they stay usable until their usage period ends,
    // their balance is what payroll settles with the final salary
    private async Task stop_earning(List<HolidayAccount> left_accounts)
    {
        List<Guid> law_ids = left_accounts.Where(a => a.holiday_type == HolidayType.FerieEfterFerieloven).Select(a => a.id).ToList();
        List<Guid> agreed_ids = left_accounts.Where(a => a.holiday_type == HolidayType.FerieUge).Select(a => a.id).ToList();
        if (law_ids.Count > 0)
        {
            await db.law_holidays.Where(l => law_ids.Contains(l.id)).Set(l => l.active, false).UpdateAsync();
        }
        if (agreed_ids.Count > 0)
        {
            await db.agreed_holidays.Where(a => agreed_ids.Contains(a.id)).Set(a => a.active, false).UpdateAsync();
        }
    }

    // all schedules of the users of the given accounts, grouped by user (one query for everybody)
    private async Task<Dictionary<Guid, List<Schedule>>> get_schedules_by_user(params IEnumerable<HolidayAccount>[] account_lists)
    {
        List<Guid> user_ids = account_lists.SelectMany(list => list).Select(a => a.user_id).Distinct().ToList();

        List<Schedule> schedules = await db.schedules
            .Where(s => user_ids.Contains(s.user_id))
            .ToListAsync();

        return schedules
            .GroupBy(s => s.user_id)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    // pair "history id -> new accrued value", only used as VALUES source for the batch update (no table in db)
    public class AccrualRow
    {
        public Guid id { get; set; }
        public decimal accrued { get; set; }
    }

    //getting different holiday accounts with related different holiday histories
    private async Task<List<HolidayAccount>> get_holidays_with_history(HolidayType h_type)
    {
        ITable<HolidayAccount> holiday_table = h_type == HolidayType.FerieEfterFerieloven ? db.law_holidays : db.agreed_holidays;
        ITable<HolidayHistory> holiday_history_table = h_type == HolidayType.FerieEfterFerieloven ? db.law_holidays_history : db.agreed_holidays_history;
            
        var query =
            from l in holiday_table
            where l.active
            from h in holiday_history_table
                .Where(x => x.account_id == l.id)
                .OrderByDescending(x => x.period_end)
                .Take(1)
                .DefaultIfEmpty()
            select new { account = l, last = h };

        var rows = await query.ToListAsync();

        List<HolidayAccount> holidays = new List<HolidayAccount>();

        foreach (var row in rows)
        {
            if (h_type == HolidayType.FerieEfterFerieloven)
            {
                LawHolidays holiday = (LawHolidays) row.account;
                LawHolidaysHistory holiday_history = (LawHolidaysHistory) row.last;
                holiday.set_law_holiday_history(holiday_history);
                holidays.Add(holiday);
            }
            else
            {
                AgreedHolidays holiday = (AgreedHolidays) row.account;
                AgreedHolidaysHistory holiday_history = (AgreedHolidaysHistory) row.last;
                holiday.set_agreed_holiday_history(holiday_history);
                holidays.Add(holiday);
            }
        }
        return holidays;
    }
}