namespace be;

// a cancellation of an approved holiday that has not started yet. two kinds (initiated_by):
//  - Employee: the employee asks, the accountant approves or declines it. the holiday stays Accepted until a
//    cancellation is approved, a declined cancellation stays here as Rejected
//  - Employer: the accountant cancels the holiday on the employer's decision. saved directly as Accepted,
//    with the reason (required by the law: the employer must record why)
// kept in its own table (not as a status of the holiday request), so every cancellation is kept in the history.
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
    public CancellationInitiator initiated_by { get; set; } = CancellationInitiator.Employee;
    // the accountant who approved / declined it (Employee) or cancelled the holiday (Employer)
    public Guid? decided_by { get; set; }
}
