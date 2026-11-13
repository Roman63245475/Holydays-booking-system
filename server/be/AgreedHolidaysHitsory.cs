namespace be;

public class AgreedHolidaysHistory
{
    
    public Guid id { get; set; }
    public Guid account_id { get; set; }
    public DateOnly period_start { get; set; }
    public DateOnly period_end { get; set; }
    
    public decimal accrued { get; set; }

    public AgreedHolidaysHistory()
    {
        
    }

    public AgreedHolidaysHistory(Guid account_id, DateOnly period_start, DateOnly period_end, decimal accrued)
    {
        this.account_id = account_id;
        this.period_start = period_start;
        this.period_end = period_end;
        this.accrued = accrued;
    }
}