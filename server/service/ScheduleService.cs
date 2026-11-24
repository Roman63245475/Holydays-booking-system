using be;
using database;
using LinqToDB;
using LinqToDB.Async;

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

    // the schedule that applies on the given date, or the newest one if none applies yet
    public async Task<Schedule?> get_schedule_for_date(Guid user_id, DateOnly date)
    {
        List<Schedule> schedules = await db.schedules.Where(s => s.user_id == user_id).ToListAsync();
        return Schedule.find_for_date(schedules, date) ?? schedules.OrderByDescending(s => s.effective_from).FirstOrDefault();
    }

    // a changed schedule starts a new period from the given date, the old rows are kept (full history),
    // so months that were already accrued / holidays already spent keep the hours of the old schedule.
    // if the newest period already starts on that date (changed twice the same day), that period is corrected instead.
    // nothing is saved if the times did not change.
    // must run inside the caller's transaction (UserService.update_user): schedule, balances and requests change together
    public async Task change_schedule(Guid user_id, Schedule new_schedule, DateOnly effective_from)
    {
        List<Schedule> schedules = await db.schedules.Where(s => s.user_id == user_id).ToListAsync();
        Schedule? newest = schedules.OrderByDescending(s => s.effective_from).FirstOrDefault();

        if (newest != null && newest.has_same_times(new_schedule))
        {
            return;
        }

        new_schedule.user_id = user_id;
        if (newest != null && newest.effective_from >= effective_from)
        {
            new_schedule.id = newest.id;
            new_schedule.effective_from = newest.effective_from;
            await db.UpdateAsync(new_schedule);
            schedules.Remove(newest);
        }
        else
        {
            new_schedule.effective_from = effective_from;
            await db.InsertAsync(new_schedule);
        }
        schedules.Add(new_schedule);

        if (newest != null)
        {
            await convert_balances(user_id, newest, new_schedule);
        }
        await reprice_pending_requests(user_id, schedules);
    }

    // holiday is a right to DAYS, hours are only the unit (clarification): the remaining balance of every account that
    // can still be used is converted so the number of days stays the same with the new day length.
    // example: 74 h with 7.40 h/day (37 h/week) = 10 days -> part time 22.5 h/week = 4.50 h/day -> 10 days = 45 h.
    // the difference goes to converted_holiday_hours. a negative balance (holiday debt) is converted the same way.
    // approved holidays keep their hours: they are already out of the remaining balance, so the days still add up
    private async Task convert_balances(Guid user_id, Schedule old_schedule, Schedule new_schedule)
    {
        decimal old_day = old_schedule.get_average_day_hours();
        decimal new_day = new_schedule.get_average_day_hours();
        if (old_day == new_day || old_day == 0)
        {
            return;
        }

        foreach (LawHolidays account in await db.law_holidays.Where(l => l.user_id == user_id && l.usable).ToListAsync())
        {
            decimal correction = get_conversion(account, old_day, new_day);
            await db.law_holidays.Where(l => l.id == account.id)
                .Set(l => l.converted_holiday_hours, l => l.converted_holiday_hours + correction)
                .UpdateAsync();
        }
        foreach (AgreedHolidays account in await db.agreed_holidays.Where(a => a.user_id == user_id && a.usable).ToListAsync())
        {
            decimal correction = get_conversion(account, old_day, new_day);
            await db.agreed_holidays.Where(a => a.id == account.id)
                .Set(a => a.converted_holiday_hours, a => a.converted_holiday_hours + correction)
                .UpdateAsync();
        }
    }

    // hours to add to converted_holiday_hours so the remaining balance keeps its number of days
    public static decimal get_conversion(HolidayAccount account, decimal old_day, decimal new_day)
    {
        decimal remaining = account.get_available();
        decimal converted = Math.Round(remaining * new_day / old_day, 2, MidpointRounding.AwayFromZero);
        return converted - remaining;
    }

    // pending requests were priced with the schedule known when they were sent. days on or after the change now cost
    // the hours of the new schedule (days before it are unchanged), otherwise approving them later would spend
    // old-schedule hours from a balance that is already converted to the new day length
    private async Task reprice_pending_requests(Guid user_id, List<Schedule> schedules)
    {
        List<HolidayRequest> pending = await db.HolidayRequest
            .Where(r => r.user_id == user_id && r.RequestStatus == RequestStatus.Pending)
            .ToListAsync();

        foreach (HolidayRequest request in pending)
        {
            DateOnly start = DateOnly.ParseExact(request.StartDate, "yyyy-MM-dd");
            DateOnly end = DateOnly.ParseExact(request.EndDate, "yyyy-MM-dd");
            HashSet<DateOnly> days_off = (await db.days_off
                    .Where(d => d.date >= start && d.date <= end)
                    .Select(d => d.date)
                    .ToListAsync())
                .ToHashSet();

            decimal hours = RequestService.calculate_holiday_hours(start, end, schedules, days_off);
            if (hours != request.Hours)
            {
                await db.HolidayRequest.Where(r => r.id == request.id).Set(r => r.Hours, hours).UpdateAsync();
            }
        }
    }
}
