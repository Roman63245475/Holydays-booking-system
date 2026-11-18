namespace be;

public class HolidayRequest
{
    public Guid id { get; set; }
    public Guid user_id { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public int Hours { get; set; }
    public HolidayKind HolidayKind { get; set; }
    public RequestStatus RequestStatus { get; set; }
    
    
}
