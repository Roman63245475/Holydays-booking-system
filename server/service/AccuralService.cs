using be;
using database;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;

namespace service;

public class AccuralService
{
    private Database db;
    
    // the dates below are a template of ONE earning period, built for HolidayParameters.REFERENCE_YEAR from parameters.json.
    // for a real account they are moved to the needed year with AddYears (see start_new_earning_periods)

    // weeks 1-5 (FerieEfterFerieloven -> LawHolidays)
    private DateOnly law_earning_period_start;
    private DateOnly law_earning_period_end;
    private DateOnly law_usage_period_start;
    private DateOnly law_usage_period_end;

    // week 6 (FerieUge -> AgreedHolidays)
    private DateOnly agreed_earning_period_start;
    private DateOnly agreed_earning_period_end;
    private DateOnly agreed_usage_period_start;
    private DateOnly agreed_usage_period_end;

    public AccuralService(Database db)
    {
        this.db = db;

        (law_earning_period_start, law_earning_period_end, law_usage_period_start, law_usage_period_end)
            = HolidayParameters.build_period_dates("weeks1To5");
        (agreed_earning_period_start, agreed_earning_period_end, agreed_usage_period_start, agreed_usage_period_end)
            = HolidayParameters.build_period_dates("week6");
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

            await accrue_holidays_in_histories<LawHolidaysHistory>(law_holidays_with_history);
            await accrue_holidays_in_histories<AgreedHolidaysHistory>(agreed_holidays_with_history);
        
            update_accrue_holidays(law_holidays_with_history);
            update_accrue_holidays(agreed_holidays_with_history);

            // accounts whose earning period ends with the month that was just accrued
            List<LawHolidays> ended_law_holidays = get_accounts_with_ended_earning_period(law_holidays_with_history);
            List<AgreedHolidays> ended_agreed_holidays = get_accounts_with_ended_earning_period(agreed_holidays_with_history);

            // accounts with ended earning period are skipped here, they get a new account instead
            await create_new_history_records<LawHolidaysHistory>(law_holidays_with_history);
            await create_new_history_records<AgreedHolidaysHistory>(agreed_holidays_with_history);

            await update_holiday_accounts<LawHolidays>(law_holidays_with_history, nameof(LawHolidays.earned_holiday_days));
            await update_holiday_accounts<AgreedHolidays>(agreed_holidays_with_history, nameof(AgreedHolidays.earned_holiday_hours));

            await start_new_earning_periods<LawHolidays, LawHolidaysHistory>(ended_law_holidays, HolidayType.FerieEfterFerieloven);
            await start_new_earning_periods<AgreedHolidays, AgreedHolidaysHistory>(ended_agreed_holidays, HolidayType.FerieUge);
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
    private async Task create_new_history_records<THistory>(IEnumerable<HolidayAccount> holidays)
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
    // earned_column defines the column, because it is different for every holiday type (earned_holiday_days, earned_holiday_hours)
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

    //inter thr hol.hist
    // define amount
    //change am in obj
    // save obj
    //change am in hol
    // save hol
    // THistory defines the table the histories are updated in (LawHolidaysHistory -> law_holidays_history, ...)
    private async Task accrue_holidays_in_histories<THistory>(IEnumerable<HolidayAccount> holidays)
        where THistory : HolidayHistory
    {
        List<THistory> holiday_histories = new List<THistory>();
        decimal? month_rate = null;
        foreach (var holiday in holidays)
        {
            if (month_rate == null)
            {
                month_rate = holiday.get_month_rate();
            }

            THistory history = (THistory)holiday.get__holiday_history();
            
            if (history == null)
            {
                continue;
            }

            holiday_histories.Add(history);
        }

        foreach (var history in holiday_histories)
        {
            // +1 because both period_start and period_end are included (13..30 = 18 days)
            int days = history.period_end.DayNumber - history.period_start.DayNumber + 1;
            decimal day_rate = month_rate.Value / DateTime.DaysInMonth(history.period_start.Year, history.period_start.Month);
            decimal earned_holidays = days * day_rate;
            history.accrued = Math.Round(earned_holidays, 2, MidpointRounding.AwayFromZero);
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
            // template dates of this holiday type (from parameters.json, built for HolidayParameters.REFERENCE_YEAR)
            bool is_law = h_type == HolidayType.FerieEfterFerieloven;
            DateOnly t_earning_start = is_law ? law_earning_period_start : agreed_earning_period_start;
            DateOnly t_earning_end = is_law ? law_earning_period_end : agreed_earning_period_end;
            DateOnly t_usage_start = is_law ? law_usage_period_start : agreed_usage_period_start;
            DateOnly t_usage_end = is_law ? law_usage_period_end : agreed_usage_period_end;

            // the new earning period starts the day after the old one ended,
            // the difference in years moves the whole template to that year
            int years_shift = old_account.earning_period_end.AddDays(1).Year - t_earning_start.Year;

            DateOnly earning_start = t_earning_start.AddYears(years_shift);
            DateOnly earning_end = t_earning_end.AddYears(years_shift);
            DateOnly usage_start = t_usage_start.AddYears(years_shift);
            DateOnly usage_end = t_usage_end.AddYears(years_shift);

            // new() lets us create THoliday via its parameterless constructor (it also sets month_rate),
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