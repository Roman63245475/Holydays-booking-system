using System.Runtime.InteropServices.JavaScript;

namespace be;

public abstract class HolidayAccount
{

    public Guid id { get; set; }
    public Guid user_id { get; set; }
    
    public DateOnly earning_period_start { get; set; }
    public DateOnly earning_period_end { get; set; }
    
    public DateOnly usage_period_start { get; set; }
    public DateOnly usage_period_end { get; set; }
    
    public HolidayType holiday_type { get; set; }
    private string name { get; set; }

    protected HolidayHistory holiday_history { get; set; }
    
    // active = holidays are still being EARNED on this account (its earning period is not over)
    public bool active { get; set; }

    // usable = the account has not expired yet: holiday can still be requested from it.
    // true from the moment the account is created (also before its usage period starts, e.g. week 6 earned in 2026
    // can be requested in Nov 2026 for June 2027), set to false by next_month once usage_period_end has passed.
    // it does NOT mean "can be spent today": whether the holiday dates fit the account is checked with covers()
    public bool usable { get; set; } = true;

    // correction of the balance from working schedule changes (positive or negative). when the schedule changes,
    // the remaining balance is converted so the number of DAYS stays the same: hours * new day length / old day length.
    // earned_holiday_hours is not changed for this, it must stay the exact sum of the monthly accruals
    public decimal converted_holiday_hours { get; set; }

    // hours that can still be spent: earned + schedule conversions - spent on approved holidays
    public decimal get_available()
    {
        return get_earned() + converted_holiday_hours - get_used();
    }

    // whether a holiday from start to end (both included) lies completely inside the usage period of this account
    public bool covers(DateOnly start, DateOnly end)
    {
        return usage_period_start <= start && end <= usage_period_end;
    }

    protected HolidayAccount(Guid user_id, DateOnly earningPeriodStart, DateOnly earningPeriodEnd, DateOnly usagePeriodStart, DateOnly usagePeriodEnd, HolidayType holidayType, bool active)
    {
        this.id  = id;
        this.user_id = user_id;
        this.earning_period_start = earningPeriodStart;
        this.earning_period_end = earningPeriodEnd;
        this.usage_period_start = usagePeriodStart;
        this.usage_period_end = usagePeriodEnd;
        this.holiday_type = holidayType;
        this.active = active;
        define_name();
    }

    // adds an accrued amount (from history.accrued) to the earned total and recalculates remaining
    public abstract void add_earned(decimal amount);

    // returns the earned total in hours (earned_holiday_hours)
    public abstract decimal get_earned();

    // returns the hours already spent on approved holidays (used_holiday_hours)
    public abstract decimal get_used();

    // hours earned in one full month with the given working schedule and the rules of that month
    public abstract decimal get_month_rate_hours(Schedule schedule, RuleSet rule_set);

    private void define_name()
    {
        switch (this.holiday_type)
        {
            case HolidayType.FerieEfterFerieloven:
                this.name = "Ferie Efter Ferieloven";
                break;
            case HolidayType.FerieUge:
                this.name = "Ferie Uge";
                break;
            default:
                this.name = "Ferie";
                break;
        }
    }
    
    public HolidayHistory get__holiday_history()
    {
        return this.holiday_history;
    }

    public HolidayAccount()
    {
        
    }


}