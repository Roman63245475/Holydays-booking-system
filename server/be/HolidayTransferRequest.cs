namespace be;

// transfer (overførsel) of unused holiday to the next holiday period. by law it needs a WRITTEN agreement, so the
// employee asks and the accountant approves or declines (clarification: the same strategy as holiday requests).
// an approved transfer is that written agreement: who agreed (decided_by) and when (decided_at) are kept.
//  - weeks 1-5: only the 5th week (holiday beyond 4 weeks), agreed by 31 December when the usage period ends
//  - week 6: agreed before 30 September of the usage year
// max transfer_max_days days, both deadlines and whether transfers are allowed at all are rule set parameters.
// approved: the hours leave from_account (transferred_holiday_hours - hours) and are added to to_account
// (transferred_holiday_hours + hours), the next account of the same type. uses RequestStatus
// Pending / Accepted / Rejected / Withdrawn
public class HolidayTransferRequest
{
    public Guid id { get; set; }
    public Guid user_id { get; set; }
    public Guid from_account_id { get; set; }
    public Guid to_account_id { get; set; }
    public HolidayType holiday_type { get; set; }
    public decimal hours { get; set; }
    // the last day the agreement can be made (saved when the request is sent: a later change of the rules does not
    // move it). a request that is still pending after it is declined automatically by next_month
    public DateOnly deadline { get; set; }
    // optional comment of the employee
    public string? comment { get; set; }
    public RequestStatus status { get; set; }
    public DateTime created_at { get; set; }
    public DateTime? decided_at { get; set; }
    // the accountant who approved / declined it, null if the system declined it (deadline passed, employee leaves)
    public Guid? decided_by { get; set; }
    // why it was declined by the accountant or the system, null if nothing to add
    public string? note { get; set; }
}
