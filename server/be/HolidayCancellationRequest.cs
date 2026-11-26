namespace be;

// the employee asks to cancel an approved holiday that has not started yet. the accountant approves or declines it.
// kept in its own table (not as a status of the holiday request), so every attempt is kept in the history:
// the holiday stays Accepted until a cancellation is approved, a declined cancellation stays here as Rejected.
// uses RequestStatus Pending / Accepted / Rejected
public class HolidayCancellationRequest
{
    public Guid id { get; set; }
    public Guid holiday_request_id { get; set; }
    public Guid user_id { get; set; }
    // optional comment from the employee
    public string? reason { get; set; }
    public RequestStatus status { get; set; }
    public DateTime created_at { get; set; }
    public DateTime? decided_at { get; set; }
}
