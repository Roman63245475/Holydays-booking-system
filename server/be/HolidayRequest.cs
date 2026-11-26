namespace be;

public class HolidayRequest
{
    public Guid id { get; set; }
    public Guid user_id { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public decimal Hours { get; set; }
    public Guid account_id { get; set; }
    public DateTime created_at { get; set; }
    public RequestStatus RequestStatus { get; set; }

    // the request was a main holiday (hovedferie) when it was sent, so the main holiday notice applied to it.
    // saved at that moment, so a later change of the rules does not change what an old request was
    public bool is_main_holiday { get; set; }

    // the last time the status was changed by the employee (withdrawn, replaced, cancelled)
    public DateTime? updated_at { get; set; }

    // when the accountant approved or declined the request
    public DateTime? decided_at { get; set; }

    // Edit of a pending request: the old request becomes Withdrawn and a new request with the new dates is created,
    // which points to the old one here. a chain A <- B <- C is the full history of the changes.
    // a Withdrawn request that no other request points to was withdrawn by the employee without new dates
    public Guid? replaces_request_id { get; set; }
}
