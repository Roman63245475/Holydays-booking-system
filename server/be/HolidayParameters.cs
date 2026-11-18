using System.Text.Json;

namespace be;

// reads parameters.json and builds the period dates of the holiday types from it.
// used by AccuralService, HolidayAccountsService (period dates) and LawHolidays, AgreedHolidays (month rates),
// so the parsing logic is in one place
public static class HolidayParameters
{
    // the period dates are a template of ONE earning period, built for REFERENCE_YEAR.
    // for a real account they are moved to the needed year with AddYears
    public const int REFERENCE_YEAR = 2000;

    // the file is read only once, on first use.
    // parameters.json is copied next to api.dll on build, so the path does not depend on the working directory
    private static readonly Lazy<Dictionary<string, JsonElement>> parameters = new Lazy<Dictionary<string, JsonElement>>(() =>
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "parameters.json"));
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
    });

    public static int get_int(string key)
    {
        return parameters.Value[key].GetInt32();
    }

    public static decimal get_decimal(string key)
    {
        return parameters.Value[key].GetDecimal();
    }

    // hours of a full time working week of the company (sum of workSchedule.*, Business Esbjerg: 7.5 * 4 + 7 = 37)
    public static decimal get_full_time_weekly_hours()
    {
        string[] days = { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" };
        return days.Sum(day => get_decimal("workSchedule." + day));
    }

    public static DateOnly last_day_of_month(int year, int month)
    {
        return new DateOnly(year, month, DateTime.DaysInMonth(year, month));
    }

    // prefix is "weeks1To5" (law holidays) or "week6" (agreed holidays),
    // all year offsets are relative to the year the earning period starts in
    public static (DateOnly earning_start, DateOnly earning_end, DateOnly usage_start, DateOnly usage_end) build_period_dates(string prefix)
    {
        int earning_start_month = get_int(prefix + ".earningPeriodStartMonth");
        int earning_end_month = get_int(prefix + ".earningPeriodEndMonth");

        DateOnly earning_start = new DateOnly(REFERENCE_YEAR, earning_start_month, 1);
        // the earning period is one year: if it ends in an "earlier" month (Sep -> Aug), it ends in the next year
        int earning_end_year = earning_end_month < earning_start_month ? REFERENCE_YEAR + 1 : REFERENCE_YEAR;
        DateOnly earning_end = last_day_of_month(earning_end_year, earning_end_month);

        DateOnly usage_start = new DateOnly(
            REFERENCE_YEAR + get_int(prefix + ".usagePeriodStartYearOffset"),
            get_int(prefix + ".usagePeriodStartMonth"),
            1);
        DateOnly usage_end = last_day_of_month(
            REFERENCE_YEAR + get_int(prefix + ".usagePeriodEndYearOffset"),
            get_int(prefix + ".usagePeriodEndMonth"));

        return (earning_start, earning_end, usage_start, usage_end);
    }
}
