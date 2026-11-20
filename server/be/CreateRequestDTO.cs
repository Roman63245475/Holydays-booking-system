using be;

namespace api;

public class CreateRequestDTO
{
    public Guid user_id { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; } 
    public int Hours { get; set; }
    public DateTime created_at { get; set; }
    public string HolidayKind { get; set; }
    public RequestStatus RequestStatus { get; set; }
    
}
