namespace be;

public sealed class LawHolidays: HolidayAccount
{
    public decimal earned_holiday_days { get; set; }
    public decimal used_holiday_days { get; set; }
    
    private decimal remaining_holiday_days { get; set; }

    public LawHolidays(Guid user_id, DateOnly earningPeriodStart, DateOnly earningPeriodEnd,
        DateOnly usagePeriodStart, DateOnly usagePeriodEnd, HolidayType holidayType, bool active, decimal earned_holiday_days, decimal used_holiday_days): base(user_id,  earningPeriodStart, earningPeriodEnd, usagePeriodStart, usagePeriodEnd, holidayType, active) 
    {
        this.earned_holiday_days = earned_holiday_days;
        this.used_holiday_days = used_holiday_days;
        this.remaining_holiday_days = earned_holiday_days - used_holiday_days;
    }

    public LawHolidays()
    {
        
    }

}