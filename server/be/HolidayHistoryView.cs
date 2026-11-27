namespace be;

// one request of the employee's holiday history: every request that is not pending anymore
// (approved, declined, withdrawn, cancelled), with what happened to it in plain words
public class HolidayHistoryView
{
    public Guid id { get; set; }
    public string start_date { get; set; } = "";
    public string end_date { get; set; } = "";
    public decimal hours { get; set; }
    public HolidayType holiday_type { get; set; }
    public bool is_main_holiday { get; set; }
    public RequestStatus status { get; set; }
    public DateTime created_at { get; set; }
    // when it was approved / declined / withdrawn / cancelled
    public DateTime? decided_at { get; set; }
    // e.g. "Moved to 2027-01-11 – 2027-01-12 by the accountant: client project moved", null if nothing to add
    public string? note { get; set; }
}
