namespace api;

// the employee asks to transfer hours of one balance to the next holiday period
public class TransferDTO
{
    public Guid account_id { get; set; }
    public decimal hours { get; set; }
    public string? comment { get; set; }
}

// the accountant declines a transfer, optionally with a note for the employee
public class DeclineTransferDTO
{
    public string? note { get; set; }
}

// the system owner saves the last working day of a leaving employee ("yyyy-MM-dd"), null / empty = calls it off
public class SetEndDateDTO
{
    public string? end_date { get; set; }
}
