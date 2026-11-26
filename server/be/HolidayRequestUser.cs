namespace be;

// a pending holiday request as the accountant sees it
public class HolidayRequestUser {
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public decimal Hours { get; set; }
    public bool IsMainHoliday { get; set; }
    public RequestStatus Status { get; set; }
    public HolidayType HolidayType { get; set; }
}
