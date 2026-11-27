namespace be;

// the employee's holiday on a chosen future date ("how much holiday will I have on ..."). nothing is saved,
// everything is calculated from the current balances, the pending requests and the accrual still to come
public class ProjectedBalanceView
{
    public DateOnly date { get; set; }
    public DateOnly today { get; set; }
    // one holiday day of the employee's schedule on that date (weekly hours / 5), to show the hours as days
    public decimal day_hours { get; set; }
    // the sum of projected_hours of the accounts that can be used on that date, per type
    public decimal law_hours { get; set; }
    public decimal agreed_hours { get; set; }
    // how much more weeks 1-5 holiday could be borrowed on that date (holiday debt, paid back by 31 August), 0 if none
    public decimal debt_hours { get; set; }
    public List<ProjectedAccountView> accounts { get; set; } = new List<ProjectedAccountView>();
}

public class ProjectedAccountView
{
    // null for an account that does not exist yet (its earning period starts before the date)
    public Guid? id { get; set; }
    public HolidayType holiday_type { get; set; }
    public DateOnly earning_period_start { get; set; }
    public DateOnly earning_period_end { get; set; }
    public DateOnly usage_period_start { get; set; }
    public DateOnly usage_period_end { get; set; }
    // available today (earned + converted - used, approved holidays already counted as used)
    public decimal available_now { get; set; }
    // reserved by the employee's pending requests on this account
    public decimal pending_hours { get; set; }
    // what the account still earns until that date (the months that are over by then)
    public decimal future_accrual { get; set; }
    // available_now - pending_hours + future_accrual
    public decimal projected_hours { get; set; }
    // "usable": can be spent on that date, "not_yet": its usage period starts later, "expired": its usage period ended
    // before that date (projected_hours is then what is left at the end of it, unless used or transferred)
    public string state { get; set; } = "usable";
}
