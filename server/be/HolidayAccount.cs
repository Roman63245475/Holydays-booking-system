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

    protected decimal month_rate;
    
    protected HolidayHistory holiday_history { get; set; }
    
    public bool active { get; set; }

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

    // returns the earned total (earned_holiday_days / earned_holiday_hours)
    public abstract decimal get_earned();

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
    
    public decimal get_month_rate()
    {
        return this.month_rate;
    }

}