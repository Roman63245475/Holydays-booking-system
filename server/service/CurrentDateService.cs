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
}
