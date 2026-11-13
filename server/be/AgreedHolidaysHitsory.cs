namespace be;

public sealed class AgreedHolidaysHistory : HolidayHistory
{
    public AgreedHolidaysHistory()
    {

    }

    public AgreedHolidaysHistory(Guid account_id, DateOnly period_start, DateOnly period_end, decimal accrued)
        : base(account_id, period_start, period_end, accrued)
    {

    }
}
