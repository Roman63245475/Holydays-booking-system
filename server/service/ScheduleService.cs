using be;
using database;
using LinqToDB;

namespace service;

public class ScheduleService
{
    private Database db;

    public ScheduleService(Database db)
    {
        this.db = db;
    }

    // the first schedule of a user starts on the day the user started working
    public async Task create_first_schedule(User user, Schedule schedule)
    {
        schedule.user_id = user.id;
        schedule.effective_from = user.start_date;
        await db.InsertAsync(schedule);
    }
}
