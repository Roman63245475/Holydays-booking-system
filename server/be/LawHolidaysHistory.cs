namespace be;

public class LawHolidaysHistory
{
    
    public Guid id { get; set; }
    public Guid account_id { get; set; }
    public DateOnly period_start { get; set; }
    public DateOnly period_end { get; set; }
    
    public decimal accrued { get; set; }

    public LawHolidaysHistory()
    {
        
    }

    public LawHolidaysHistory(Guid accountId, DateOnly periodStart, DateOnly periodEnd, decimal accrued)
    {
        this.account_id = accountId;
        this.period_start = periodStart;
        this.period_end = periodEnd;
        this.accrued = accrued;
    }
}