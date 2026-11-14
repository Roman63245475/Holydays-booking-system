namespace be;

public sealed class AgreedHolidays: HolidayAccount
{
    public decimal earned_holiday_hours { get; set; }
    public decimal used_holiday_hours { get; set; }
    
    private decimal remaining_holiday_hours { get; set; }
    
    
    public AgreedHolidays(Guid user_id, DateOnly earningPeriodStart, DateOnly earningPeriodEnd,
        DateOnly usagePeriodStart, DateOnly usagePeriodEnd, HolidayType holidayType, bool active, decimal earned_holiday_hours, decimal used_holiday_hours): base(user_id,  earningPeriodStart, earningPeriodEnd, usagePeriodStart, usagePeriodEnd, holidayType, active) 
    {
        this.earned_holiday_hours = earned_holiday_hours;
        this.used_holiday_hours = used_holiday_hours;
        this.remaining_holiday_hours = earned_holiday_hours - used_holiday_hours;
        set_month_rate();
    }

    public AgreedHolidays()
    {
        set_month_rate();
    }
    
    public override void add_earned(decimal amount)
    {
        this.earned_holiday_hours += amount;
        //this.remaining_holiday_hours = this.earned_holiday_hours - this.used_holiday_hours;
    }

    public override decimal get_earned()
    {
        return this.earned_holiday_hours;
    }
    
    public void set_agreed_holiday_history(AgreedHolidaysHistory lh)
    {
        this.holiday_history = lh;
    }

    private void set_month_rate()
    {
        this.month_rate = 3.083m;
    }
    
    
}