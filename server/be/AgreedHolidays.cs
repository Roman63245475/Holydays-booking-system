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
    }

    public AgreedHolidays()
    {
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

    public override decimal get_used()
    {
        return this.used_holiday_hours;
    }
    
    public void set_agreed_holiday_history(AgreedHolidaysHistory lh)
    {
        this.holiday_history = lh;
    }

    // week 6 is defined in hours per month for a FULL TIME employee (week6_hours_per_month = 3.083),
    // a part time employee earns in proportion to the weekly hours (3.083 * weekly_hours / 37),
    // so a year always gives exactly one week of the employee's own schedule (full time 37 h, part time 30 h -> 30 h).
    // in a month where week 6 is disabled nothing is earned: the history chain still goes on (accrued = 0),
    // so accounts and periods keep working and accrual simply continues when week 6 is enabled again
    public override decimal get_month_rate_hours(Schedule schedule, RuleSet rule_set)
    {
        if (!rule_set.week6_enabled)
        {
            return 0;
        }

        decimal full_time_weekly_hours = rule_set.get_standard_schedule().get_weekly_hours();
        return rule_set.week6_hours_per_month * schedule.get_weekly_hours() / full_time_weekly_hours;
    }
    
    
}