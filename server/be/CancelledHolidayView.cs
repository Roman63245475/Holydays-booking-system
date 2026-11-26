namespace be;

// an approved holiday of the employee that the accountant cancelled on the employer's decision, with the reason
public class CancelledHolidayView
{
    public Guid Id { get; set; }
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public decimal Hours { get; set; }
    public string HolidayKind { get; set; } = "";
    public string? Reason { get; set; }
    public DateTime CancelledAt { get; set; }
}
