namespace be;

// a holiday request as the employee sees it (Ongoing Requests and Approved Holidays tabs)
public class OngoingHolidayRequest
{
    public Guid Id { get; set; }
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public decimal Hours { get; set; }
    public DateTime CreatedAt { get; set; }
    public string HolidayKind { get; set; } = "";
    public RequestStatus RequestStatus { get; set; }
    public bool IsMainHoliday { get; set; }
    // approved holidays only: a cancellation request is waiting for the accountant
    public bool HasPendingCancellation { get; set; }
    // approved holidays only: the holiday has started (or is over), so it can not be cancelled anymore
    public bool Started { get; set; }
}
