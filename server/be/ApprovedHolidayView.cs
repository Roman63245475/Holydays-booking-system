namespace be;

// an approved holiday that is not over yet, as the accountant sees it (Approved Holidays tab)
public class ApprovedHolidayView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public decimal Hours { get; set; }
    public HolidayType HolidayType { get; set; }
    public bool IsMainHoliday { get; set; }
    // the holiday has started: it can never be cancelled anymore
    public bool Started { get; set; }
    // the employee has asked to cancel it; cancelling it here approves the employee's request
    public bool HasPendingCancellation { get; set; }
    public string? PendingCancellationReason { get; set; }
}
