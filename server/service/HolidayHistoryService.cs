using be;
using database;
using LinqToDB;

namespace service;

public class HolidayHistoryService
{

    private Database db;

    public HolidayHistoryService(Database db)
    {
        this.db = db;
    }

    // the first history of an account starts on the day the user started working (not on the 1st of the month),
    // so only the days the user really worked in the first month are accrued
    public async Task create_history_records(LawHolidays lawHolidays, AgreedHolidays agreed_holidays, DateOnly user_start_date)
    {
        decimal accrued = 0.0m;

        LawHolidaysHistory law_holiday_history = new LawHolidaysHistory(
            lawHolidays.id, user_start_date, get_first_period_end(user_start_date, lawHolidays.earning_period_end), accrued);

        AgreedHolidaysHistory agreed_holiday_history = new AgreedHolidaysHistory(
            agreed_holidays.id, user_start_date, get_first_period_end(user_start_date, agreed_holidays.earning_period_end), accrued);

        // insertion
        await db.InsertAsync(law_holiday_history);
        await db.InsertAsync(agreed_holiday_history);
    }

    // the history ends on whatever comes first: the end of the start month or the end of the account's earning period
    private static DateOnly get_first_period_end(DateOnly period_start, DateOnly earning_period_end)
    {
        DateOnly month_end = HolidayParameters.last_day_of_month(period_start.Year, period_start.Month);
        return month_end < earning_period_end ? month_end : earning_period_end;
    }
}
