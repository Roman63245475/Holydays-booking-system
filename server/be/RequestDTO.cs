using be;

namespace api;

public class RequestDTO
{
    public Guid user_id { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; } 
    public decimal Hours { get; set; }
    public DateTime created_at { get; set; }
    public Guid account_id { get; set; }
    public RequestStatus RequestStatus { get; set; }
    
}
