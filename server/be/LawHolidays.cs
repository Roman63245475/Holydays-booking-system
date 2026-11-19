namespace be;

public sealed class LawHolidays: HolidayAccount
{
    public decimal earned_holiday_hours { get; set; }
    public decimal used_holiday_hours { get; set; }
    
    private decimal remaining_holiday_hours { get; set; }
    

    public LawHolidays(Guid user_id, DateOnly earningPeriodStart, DateOnly earningPeriodEnd,
        DateOnly usagePeriodStart, DateOnly usagePeriodEnd, HolidayType holidayType, bool active, decimal earned_holiday_hours, decimal used_holiday_hours): base(user_id,  earningPeriodStart, earningPeriodEnd, usagePeriodStart, usagePeriodEnd, holidayType, active) 
    {
        this.earned_holiday_hours = earned_holiday_hours;
        this.used_holiday_hours = used_holiday_hours;
        this.remaining_holiday_hours = earned_holiday_hours - used_holiday_hours;
    }

    public override void add_earned(decimal amount)
    {
        this.earned_holiday_hours += amount;
        this.remaining_holiday_hours = this.earned_holiday_hours - this.used_holiday_hours;
    }

    public override decimal get_earned()
    {
        return this.earned_holiday_hours;
    }

    public LawHolidays()
    {
    }

    public void set_law_holiday_history(LawHolidaysHistory lh)
    {
        this.holiday_history = lh;
    }


    // the law defines the rate in DAYS per month (weeks1to5_month_rate = 2.08), the balance is kept in hours.
    // 25 holiday days a year = 5 weeks, so one holiday day = 1/5 of the employee's working week
    // (full time 37 h -> 7.40 h, part time 30 h -> 6.00 h), no matter on how many weekdays the employee works.
    // full time: 2.08 * 7.40 = 15.39 hours per month
    public override decimal get_month_rate_hours(Schedule schedule, RuleSet rule_set)
    {
        return rule_set.weeks1to5_month_rate * schedule.get_average_day_hours();
    }
}