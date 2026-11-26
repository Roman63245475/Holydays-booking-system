namespace be;

// a pending cancellation request as the accountant sees it, with the holiday it wants to cancel
public class CancellationRequestView
{
    public Guid Id { get; set; }
    public Guid HolidayRequestId { get; set; }
    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public decimal Hours { get; set; }
    public HolidayType HolidayType { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
