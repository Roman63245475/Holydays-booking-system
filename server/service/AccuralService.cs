using be;
using database;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;

namespace service;

public class AccuralService
{
    private Database db;
    
    public AccuralService(Database db)
    {
        this.db = db;
    }

    public async Task accrue_holidays()
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
        
        await create_new_history_records<LawHolidaysHistory>(law_holidays_with_history);
        await create_new_history_records<AgreedHolidaysHistory>(agreed_holidays_with_history);

        await update_holiday_accounts<LawHolidays>(law_holidays_with_history, nameof(LawHolidays.earned_holiday_days));
        await update_holiday_accounts<AgreedHolidays>(agreed_holidays_with_history, nameof(AgreedHolidays.earned_holiday_hours));

        
        
        

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

            // first day of the month after the last history period
            DateOnly next_start = new DateOnly(h.period_end.Year, h.period_end.Month, 1).AddMonths(1);
            DateOnly next_end = next_start.AddMonths(1).AddDays(-1);

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