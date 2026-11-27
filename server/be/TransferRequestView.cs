namespace be;

// a transfer request as the employee (their own) and the accountant (all pending ones) see it
public class TransferRequestView
{
    public Guid id { get; set; }
    public Guid user_id { get; set; }
    public string name { get; set; } = "";
    public HolidayType holiday_type { get; set; }
    // the account the hours come from
    public DateOnly earning_period_start { get; set; }
    public DateOnly earning_period_end { get; set; }
    public DateOnly usage_period_end { get; set; }
    // the account they go to: usable until
    public DateOnly to_usage_period_end { get; set; }
    public decimal hours { get; set; }
    // one holiday day of the employee's current schedule, to show the hours as days
    public decimal day_hours { get; set; }
    public DateOnly deadline { get; set; }
    public string? comment { get; set; }
    public RequestStatus status { get; set; }
    public DateTime created_at { get; set; }
    public DateTime? decided_at { get; set; }
    public string? note { get; set; }
    // pending only: what can be transferred right now (the balance may have changed since the request was sent).
    // can_approve = false -> approving it fails, the reason tells why
    public decimal max_hours_now { get; set; }
    public bool can_approve { get; set; }
    public string? not_approvable_reason { get; set; }
}
