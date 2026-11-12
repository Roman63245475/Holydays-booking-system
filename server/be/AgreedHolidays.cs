namespace be;

public sealed class AgreedHolidays: HolidayAccount
{
    public decimal earned_holiday_hours { get; set; }
    public decimal used_holiday_hours { get; set; }
    
    private decimal remaining_holiday_hours { get; set; }
    
    public AgreedHolidays(Guid id, Guid user_id, DateOnly earningPeriodStart, DateOnly earningPeriodEnd,
        DateOnly usagePeriodStart, DateOnly usagePeriodEnd, HolidayType holidayType, bool active, decimal earned_holiday_hours, decimal used_holiday_hours): base(id, user_id,  earningPeriodStart, earningPeriodEnd, usagePeriodStart, usagePeriodEnd, holidayType, active) 
    {
        this.earned_holiday_hours = earned_holiday_hours;
        this.used_holiday_hours = used_holiday_hours;
        this.remaining_holiday_hours = earned_holiday_hours - used_holiday_hours;
    }

    public AgreedHolidays()
    {
        
    }
}