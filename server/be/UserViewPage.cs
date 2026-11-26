namespace be;

// what the employee sees on their page: every holiday account that has not expired yet.
// there can be up to 2 accounts of each type at the same time:
// weeks 1-5 from 1 Sep to 31 Dec (the old account's last 4 months + the new one),
// week 6 always (one is being earned, the previous one is being used / waits for 1 May)
public class UserViewPage
{
    public string name  { get; set; }
    public string email { get; set; }
    public List<HolidayAccountView> accounts { get; set; } = new List<HolidayAccountView>();

    // main holiday (hovedferie) of the coming / current summer: the employee has the right to 3 weeks between
    // 1 May and 30 September. "none" = not requested yet, "pending" = requested, waits for approval, "approved"
    public int main_holiday_year { get; set; }
    public DateOnly main_holiday_window_start { get; set; }
    public DateOnly main_holiday_window_end { get; set; }
    public int main_holiday_length_days { get; set; }
    public int main_holiday_notice_months { get; set; }
    public string main_holiday_status { get; set; } = "none";
}

public class HolidayAccountView
{
    public Guid id { get; set; }
    public HolidayType holiday_type { get; set; }
    // holidays are still being earned on this account
    public bool active { get; set; }
    public DateOnly earning_period_start { get; set; }
    public DateOnly earning_period_end { get; set; }
    // holiday can be taken from usage_period_start until usage_period_end (expiry date)
    public DateOnly usage_period_start { get; set; }
    public DateOnly usage_period_end { get; set; }
    public decimal earned_holiday_hours { get; set; }
    public decimal used_holiday_hours { get; set; }
    // + / - hours from working schedule changes (the balance is converted so the number of days stays the same)
    public decimal converted_holiday_hours { get; set; }
    public decimal available_holiday_hours { get; set; }
}
