namespace be;

// keeps all rule sets (admin-editable holiday rules) in memory and answers "which rules apply on this date".
// the rule sets are read from the db once on startup (Program.cs) and again after a new rule set is saved,
// so calculations do not query the db for every account / month
public static class HolidayParameters
{
    // the period dates are first built as a template of ONE earning period for REFERENCE_YEAR,
    // then moved to the needed year with AddYears
    public const int REFERENCE_YEAR = 2000;

    // all rule sets sorted by effective_from. a new list is assigned on reload (never changed in place),
    // so a request that is reading the old list at that moment is not affected
    private static List<RuleSet> rule_sets = new List<RuleSet>();

    public static void load(IEnumerable<RuleSet> sets)
    {
        rule_sets = sets.OrderBy(s => s.effective_from).ToList();
    }

    // the rules that apply on the given date: the rule set with the latest effective_from <= date
    public static RuleSet get_rule_set(DateOnly date)
    {
        RuleSet? result = rule_sets.LastOrDefault(s => s.effective_from <= date);
        if (result == null)
        {
            throw new InvalidOperationException($"No holiday rule set applies on {date}");
        }
        return result;
    }

    public static DateOnly last_day_of_month(int year, int month)
    {
        return new DateOnly(year, month, DateTime.DaysInMonth(year, month));
    }

    // earning and usage period of the account (of the given holiday type) that contains the given date,
    // calculated with the rules that apply on that date.
    // example (law, 1 Sep - 31 Aug): 17.11.2026 -> 01.09.2026 - 31.08.2027, 10.03.2026 -> 01.09.2025 - 31.08.2026
    public static (DateOnly earning_start, DateOnly earning_end, DateOnly usage_start, DateOnly usage_end) get_account_dates(
        HolidayType h_type, DateOnly date)
    {
        var (t_earning_start, t_earning_end, t_usage_start, t_usage_end) = build_period_template(get_rule_set(date), h_type);

        int years_shift = date.Year - t_earning_start.Year;

        // the earning period of this year has not started yet on that date, so the date belongs to the previous one
        if (t_earning_start.AddYears(years_shift) > date)
        {
            years_shift--;
        }

        return (t_earning_start.AddYears(years_shift),
                t_earning_end.AddYears(years_shift),
                t_usage_start.AddYears(years_shift),
                t_usage_end.AddYears(years_shift));
    }

    // template of one earning period for REFERENCE_YEAR, all year offsets are relative to the year the earning period starts in
    private static (DateOnly earning_start, DateOnly earning_end, DateOnly usage_start, DateOnly usage_end) build_period_template(
        RuleSet rs, HolidayType h_type)
    {
        bool is_law = h_type == HolidayType.FerieEfterFerieloven;
        int earning_start_month = is_law ? rs.weeks1to5_earning_start_month : rs.week6_earning_start_month;
        int earning_end_month = is_law ? rs.weeks1to5_earning_end_month : rs.week6_earning_end_month;
        int usage_start_month = is_law ? rs.weeks1to5_usage_start_month : rs.week6_usage_start_month;
        int usage_start_year_offset = is_law ? rs.weeks1to5_usage_start_year_offset : rs.week6_usage_start_year_offset;
        int usage_end_month = is_law ? rs.weeks1to5_usage_end_month : rs.week6_usage_end_month;
        int usage_end_year_offset = is_law ? rs.weeks1to5_usage_end_year_offset : rs.week6_usage_end_year_offset;

        DateOnly earning_start = new DateOnly(REFERENCE_YEAR, earning_start_month, 1);
        // the earning period is one year: if it ends in an "earlier" month (Sep -> Aug), it ends in the next year
        int earning_end_year = earning_end_month < earning_start_month ? REFERENCE_YEAR + 1 : REFERENCE_YEAR;
        DateOnly earning_end = last_day_of_month(earning_end_year, earning_end_month);

        DateOnly usage_start = new DateOnly(REFERENCE_YEAR + usage_start_year_offset, usage_start_month, 1);
        DateOnly usage_end = last_day_of_month(REFERENCE_YEAR + usage_end_year_offset, usage_end_month);

        return (earning_start, earning_end, usage_start, usage_end);
    }
}
