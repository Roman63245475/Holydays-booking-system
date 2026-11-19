namespace be;

// one row = the full set of the company's holiday rules (admin-editable parameters).
// a change never updates a row: a new row with a new effective_from is added, old rows are kept,
// so months that were already accrued are still calculated with the rules that applied then.
// the rules that apply on a date = the row with the latest effective_from <= that date
public class RuleSet
{
    public Guid id { get; set; }

    // the rules apply from this date (always the 1st of a month) until the effective_from of the next rule set
    public DateOnly effective_from { get; set; }

    // --- weeks 1-5 (Ferieloven -> LawHolidays) ---
    // holiday DAYS earned per month (2.08), converted to hours with the employee's schedule
    public decimal weeks1to5_month_rate { get; set; }
    public int weeks1to5_earning_start_month { get; set; }
    public int weeks1to5_earning_end_month { get; set; }
    public int weeks1to5_usage_start_month { get; set; }
    // year offsets are relative to the year the earning period starts in
    public int weeks1to5_usage_start_year_offset { get; set; }
    public int weeks1to5_usage_end_month { get; set; }
    public int weeks1to5_usage_end_year_offset { get; set; }

    // --- week 6 (KL 05.11 -> AgreedHolidays) ---
    // false = nothing is accrued in the months it is disabled (accrued = 0), already earned hours are kept
    public bool week6_enabled { get; set; }
    // hours earned per month by a FULL TIME employee (3.083), part time in proportion to the weekly hours
    public decimal week6_hours_per_month { get; set; }
    public int week6_earning_start_month { get; set; }
    public int week6_earning_end_month { get; set; }
    public int week6_usage_start_month { get; set; }
    public int week6_usage_start_year_offset { get; set; }
    public int week6_usage_end_month { get; set; }
    public int week6_usage_end_year_offset { get; set; }

    // --- main holiday and notice ---
    public int main_holiday_length_days { get; set; }
    public int main_holiday_start_month { get; set; }
    public int main_holiday_end_month { get; set; }
    public int main_holiday_notice_months { get; set; }
    public int other_holiday_notice_months { get; set; }

    // --- standard (full time) working week of the company ---
    // default schedule in the create user form + full time weekly hours for the week 6 rate.
    // null start and end = not a working day
    public TimeOnly? monday_start { get; set; }
    public TimeOnly? monday_end { get; set; }
    public TimeOnly? tuesday_start { get; set; }
    public TimeOnly? tuesday_end { get; set; }
    public TimeOnly? wednesday_start { get; set; }
    public TimeOnly? wednesday_end { get; set; }
    public TimeOnly? thursday_start { get; set; }
    public TimeOnly? thursday_end { get; set; }
    public TimeOnly? friday_start { get; set; }
    public TimeOnly? friday_end { get; set; }

    // --- transfer of unused holiday to the next period ---
    // currently not allowed at Business Esbjerg, but it must be implemented (future-proof)
    public bool transfer_allowed { get; set; }
    public int transfer_max_days { get; set; }
    public int transfer_weeks1to5_deadline_month { get; set; }
    public int transfer_weeks1to5_deadline_day { get; set; }
    public int transfer_week6_deadline_month { get; set; }
    public int transfer_week6_deadline_day { get; set; }

    // --- holiday debt ---
    // how many days the balance may go below 0, and the day of the year it must be 0 or positive again
    public int max_holiday_debt_days { get; set; }
    public int non_negative_balance_month { get; set; }
    public int non_negative_balance_day { get; set; }

    // the standard working week as a Schedule, so its hours are calculated the same way as an employee's schedule
    public Schedule get_standard_schedule()
    {
        return new Schedule
        {
            monday_start = monday_start, monday_end = monday_end,
            tuesday_start = tuesday_start, tuesday_end = tuesday_end,
            wednesday_start = wednesday_start, wednesday_end = wednesday_end,
            thursday_start = thursday_start, thursday_end = thursday_end,
            friday_start = friday_start, friday_end = friday_end
        };
    }
}
