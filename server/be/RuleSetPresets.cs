namespace be;

// ready-made rule sets. the first one is saved to the db on the first start (see DatabaseInitializer),
// later the admin can start a new rule set from a preset and adjust it
public static class RuleSetPresets
{
    // "Ferieloven + 6. ferieuge (KL 05.11 model)": what Business Esbjerg uses
    public static RuleSet business_esbjerg(DateOnly effective_from)
    {
        return new RuleSet
        {
            effective_from = effective_from,

            // weeks 1-5: 2.08 days a month, earned 1 Sep - 31 Aug, used 1 Sep - 31 Dec of the following year
            weeks1to5_month_rate = 2.08m,
            weeks1to5_earning_start_month = 9,
            weeks1to5_earning_end_month = 8,
            weeks1to5_usage_start_month = 9,
            weeks1to5_usage_start_year_offset = 0,
            weeks1to5_usage_end_month = 12,
            weeks1to5_usage_end_year_offset = 1,

            // week 6: 3.083 hours a month (full time), earned 1 Jan - 31 Dec, used 1 May - 30 Apr after the earning year
            week6_enabled = true,
            week6_hours_per_month = 3.083m,
            week6_earning_start_month = 1,
            week6_earning_end_month = 12,
            week6_usage_start_month = 5,
            week6_usage_start_year_offset = 1,
            week6_usage_end_month = 4,
            week6_usage_end_year_offset = 2,

            // main holiday: 15 consecutive days between 1 May and 30 Sep, notice 3 months (main) / 1 month (other)
            main_holiday_length_days = 15,
            main_holiday_start_month = 5,
            main_holiday_end_month = 9,
            main_holiday_notice_months = 3,
            other_holiday_notice_months = 1,

            // standard week: Mon-Thu 7.5 h, Fri 7 h = 37 h
            monday_start = new TimeOnly(8, 0), monday_end = new TimeOnly(15, 30),
            tuesday_start = new TimeOnly(8, 0), tuesday_end = new TimeOnly(15, 30),
            wednesday_start = new TimeOnly(8, 0), wednesday_end = new TimeOnly(15, 30),
            thursday_start = new TimeOnly(8, 0), thursday_end = new TimeOnly(15, 30),
            friday_start = new TimeOnly(8, 0), friday_end = new TimeOnly(15, 0),

            // transfer: max 5 days, currently not allowed; deadlines 31 Dec (5th week) and 30 Sep (week 6)
            transfer_allowed = false,
            transfer_max_days = 5,
            transfer_weeks1to5_deadline_month = 12,
            transfer_weeks1to5_deadline_day = 31,
            transfer_week6_deadline_month = 9,
            transfer_week6_deadline_day = 30,

            // holiday debt: max 1 week, the balance must be 0 or positive on 31 August
            max_holiday_debt_days = 5,
            non_negative_balance_month = 8,
            non_negative_balance_day = 31
        };
    }
}
