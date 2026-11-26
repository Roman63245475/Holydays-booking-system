namespace api;

// new dates of a pending request (Edit), "yyyy-MM-dd". the account stays the same
public class EditRequestDTO
{
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
}
