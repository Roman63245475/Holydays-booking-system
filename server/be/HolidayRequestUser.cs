namespace be;

public class HolidayRequestUser {
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public RequestStatus Status { get; set; }
    public HolidayType HolidayType { get; set; }
}