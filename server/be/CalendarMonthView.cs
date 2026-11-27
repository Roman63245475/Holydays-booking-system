namespace be;

// one month of the holiday calendar: who is on holiday, and the public holidays / company days off
public class CalendarMonthView
{
    public int year { get; set; }
    public int month { get; set; }
    public DateOnly today { get; set; }
    // how many employees (role User) there are, the base for "x of y absent"
    public int employee_count { get; set; }
    public List<CalendarDayOffView> days_off { get; set; } = new List<CalendarDayOffView>();
    // holidays that overlap the month (they can start before it or end after it)
    public List<CalendarHolidayView> holidays { get; set; } = new List<CalendarHolidayView>();
}

public class CalendarDayOffView
{
    public DateOnly date { get; set; }
    public string name { get; set; } = "";
    public TypeOfHolidayDay type { get; set; }
}

public class CalendarHolidayView
{
    public Guid request_id { get; set; }
    public Guid user_id { get; set; }
    public string user_name { get; set; } = "";
    public string start_date { get; set; } = "";
    public string end_date { get; set; } = "";
    public HolidayType holiday_type { get; set; }
    // "approved" or "pending"
    public string status { get; set; } = "approved";
    // the holiday of the logged in user
    public bool is_mine { get; set; }
}
