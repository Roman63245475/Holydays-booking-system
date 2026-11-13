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
        set_month_rate();
    }

    public override void add_earned(decimal amount)
    {
        this.earned_holiday_days += amount;
        this.remaining_holiday_days = this.earned_holiday_days - this.used_holiday_days;
    }

    public override decimal get_earned()
    {
        return this.earned_holiday_days;
    }

    public LawHolidays()
    {
        set_month_rate();
    }

    public void set_law_holiday_history(LawHolidaysHistory lh)
    {
        this.holiday_history = lh;
    }


    private void set_month_rate()
    {
        this.month_rate = 2.08m;
    }
}