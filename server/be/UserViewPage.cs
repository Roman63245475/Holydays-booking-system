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

    // the last working day if the employee leaves the company, otherwise null
    public DateOnly? end_date { get; set; }
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
    // - hours transferred to the next holiday period, + hours received from the previous one (approved transfers)
    public decimal transferred_holiday_hours { get; set; }
    public decimal available_holiday_hours { get; set; }
    // false = the usage period is over (only shown to the accountant, as a balance payroll has to handle)
    public bool usable { get; set; } = true;

    // --- status of the balance (AccountStatusService) ---
    // one holiday day of the employee's current schedule (weekly hours / 5), to show hours as days
    public decimal day_hours { get; set; }
    // reserved by pending holiday requests and a pending transfer of this account
    public decimal pending_hours { get; set; }
    // available - pending: hours that are not planned (requested) yet
    public decimal unplanned_hours { get; set; }
    // days until usage_period_end (negative when it is over)
    public int days_until_expiry { get; set; }
    // what has to be done with the balance:
    //  "none"
    //  "plan_soon"      unplanned hours and the usage period ends within AccountStatusService.EXPIRY_WARNING_MONTHS:
    //                   they must be taken (or transferred), otherwise payroll pays them out / they go to the holiday fund
    //  "expired_unused" the usage period is over and hours were neither taken nor transferred: payroll handles them
    //  "leaving"        the employee leaves: the balance on the last working day is settled with the final salary
    public string flag { get; set; } = "none";
    // "leaving" only: what is left on the last working day = available + what is still earned until then
    // (pending requests not counted, they may still be approved)
    public decimal settlement_hours { get; set; }

    // --- transfer to the next holiday period ---
    public bool can_transfer { get; set; }
    // the most hours that can be transferred now (1 week, for weeks 1-5 only the 5th week)
    public decimal transfer_max_hours { get; set; }
    // the last day a transfer can be agreed, null if a transfer is not possible at all for this account
    public DateOnly? transfer_deadline { get; set; }
    // why a transfer is not possible (shown when it is relevant), null if it is possible
    public string? transfer_unavailable_reason { get; set; }
    // the transfer request of this account that waits for the accountant, if any
    public Guid? pending_transfer_id { get; set; }
    public decimal pending_transfer_hours { get; set; }
}
