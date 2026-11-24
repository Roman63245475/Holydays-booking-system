namespace be;

// weekly working schedule of an employee: for every weekday Monday-Friday the time the work starts and ends.
// a day off (part time) has both start and end = null. Saturday and Sunday are never working days.
// a new row with a new effective_from is added when the schedule changes, old rows are kept (full history),
// so the hours that were already earned / spent are still calculated with the schedule that applied then
public class Schedule
{
    public Guid id { get; set; }
    public Guid user_id { get; set; }

    // the schedule applies from this date until the effective_from of the next schedule of the same user
    public DateOnly effective_from { get; set; }

    public TimeOnly? monday_start { get; set; }
    public TimeOnly? monday_end { get; set; }
    public TimeOnly? tuesday_start { get; set; }
    public TimeOnly? tuesday_end { get; set; }
    public TimeOnly? wednesday_start { get; set; }
    public TimeOnly? wednesday_end { get; set; }
    public TimeOnly? thursday_start { get; set; }
    public TimeOnly? thursday_end { get; set; }
    public TimeOnly? friday_start { get; set; }
    public TimeOnly? friday_end { get; set; }

    // working hours of the given weekday (e.g. 08:00 - 15:30 = 7.50), 0 for a day off and for the weekend
    public decimal get_hours(DayOfWeek day)
    {
        var (start, end) = get_times(day);
        if (start == null || end == null)
        {
            return 0;
        }

        decimal hours = (decimal)(end.Value - start.Value).TotalHours;
        return Math.Round(hours, 2, MidpointRounding.AwayFromZero);
    }

    // sum of the working hours of the whole week (full time at Business Esbjerg = 37)
    public decimal get_weekly_hours()
    {
        return get_hours(DayOfWeek.Monday) + get_hours(DayOfWeek.Tuesday) + get_hours(DayOfWeek.Wednesday)
               + get_hours(DayOfWeek.Thursday) + get_hours(DayOfWeek.Friday);
    }

    // one "holiday day" in hours = 1/5 of the working week (the law counts 5 holiday days per week,
    // also for part time employees who work fewer than 5 days). NOT the hours of a specific weekday
    public decimal get_average_day_hours()
    {
        return get_weekly_hours() / 5;
    }

    // true if both schedules have the same working times on every weekday (id, user and effective_from are ignored)
    public bool has_same_times(Schedule other)
    {
        return monday_start == other.monday_start && monday_end == other.monday_end
            && tuesday_start == other.tuesday_start && tuesday_end == other.tuesday_end
            && wednesday_start == other.wednesday_start && wednesday_end == other.wednesday_end
            && thursday_start == other.thursday_start && thursday_end == other.thursday_end
            && friday_start == other.friday_start && friday_end == other.friday_end;
    }

    // the schedule that applies on the given date: the latest one with effective_from <= date, null if there is none
    public static Schedule? find_for_date(IEnumerable<Schedule> schedules, DateOnly date)
    {
        return schedules
            .Where(s => s.effective_from <= date)
            .OrderByDescending(s => s.effective_from)
            .FirstOrDefault();
    }

    private (TimeOnly? start, TimeOnly? end) get_times(DayOfWeek day)
    {
        switch (day)
        {
            case DayOfWeek.Monday: return (monday_start, monday_end);
            case DayOfWeek.Tuesday: return (tuesday_start, tuesday_end);
            case DayOfWeek.Wednesday: return (wednesday_start, wednesday_end);
            case DayOfWeek.Thursday: return (thursday_start, thursday_end);
            case DayOfWeek.Friday: return (friday_start, friday_end);
            default: return (null, null);
        }
    }
}
