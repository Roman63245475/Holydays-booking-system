using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

// "today" of the system. the time simulator (next_month) can be ahead of the real date, so every check that depends on
// the current date (expired accounts, which accounts to show, later the notice rules) must use this date,
// not DateTime.Now
public class CurrentDateService
{
    private Database db;

    public CurrentDateService(Database db)
    {
        this.db = db;
    }

    // the newest period_start of all histories = the month that will be accrued next (every month before it is over).
    // a new user's first history starts on their start date, so without simulation this is simply the real date.
    // null if nothing exists yet (no users)
    public async Task<DateOnly?> get_simulated_date()
    {
        DateOnly? last_law = await db.law_holidays_history.Select(h => (DateOnly?)h.period_start).MaxAsync();
        DateOnly? last_agreed = await db.agreed_holidays_history.Select(h => (DateOnly?)h.period_start).MaxAsync();

        // law and agreed histories always move together, the agreed date is only a safety net. Max() skips null values
        return new[] { last_law, last_agreed }.Max();
    }

    // the current date: the simulated date, or the real date if nothing has been accrued yet
    public async Task<DateOnly> get_current_date()
    {
        return await get_simulated_date() ?? DateOnly.FromDateTime(DateTime.Now);
    }

    // start date of a new user. their first history must lie in the same open (not yet accrued) month as everybody
    // else's, otherwise next_month moves them one month at a time from a different month and they never catch up.
    // so the real date is used, but kept inside the open month:
    //   - real date inside the open month (no simulation)           -> the real date (exact day)
    //   - simulation is ahead (open month after the real date)      -> 1st of the open month (the simulated "today")
    //   - accrual is behind (real date after the open month)        -> last day of the open month
    //   - no histories yet (first user)                             -> the real date
    public async Task<DateOnly> get_new_user_start_date()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        DateOnly? simulated = await get_simulated_date();
        if (simulated == null)
        {
            return today;
        }

        DateOnly open_month_start = new DateOnly(simulated.Value.Year, simulated.Value.Month, 1);
        DateOnly open_month_end = open_month_start.AddMonths(1).AddDays(-1);

        if (today < open_month_start)
        {
            return open_month_start;
        }
        if (today > open_month_end)
        {
            return open_month_end;
        }
        return today;
    }
}
