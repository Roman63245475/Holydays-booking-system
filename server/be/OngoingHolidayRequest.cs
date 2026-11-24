namespace be;

public class OngoingHolidayRequest
{
    public Guid Id { get; set; }
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public decimal Hours { get; set; }
    public DateTime CreatedAt { get; set; }
    public string HolidayKind { get; set; } = "";
    public RequestStatus RequestStatus { get; set; }
}
