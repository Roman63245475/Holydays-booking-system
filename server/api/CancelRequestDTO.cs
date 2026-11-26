namespace api;

// the employee asks to cancel an approved holiday, the reason is optional
public class CancelRequestDTO
{
    public string? Reason { get; set; }
}
