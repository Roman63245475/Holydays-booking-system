using be;
using database;
using LinqToDB;
using LinqToDB.Internal.DataProvider.Firebird;

namespace service;

public class HolidayHistoryService
{

    private Database db;

    public HolidayHistoryService(Database db)
    {
        this.db = db;
    }

    public async Task create_history_records(LawHolidays lawHolidays, AgreedHolidays agreed_holidays)
    {
        
        // Law holidays history instance creation for following insertion in the db
        Guid law_account_id = lawHolidays.id;
        DateOnly start_period_law_history = lawHolidays.earning_period_start;
        int last_day_of_month = DateTime.DaysInMonth(start_period_law_history.Year,  start_period_law_history.Month);
        DateOnly end_period_law_history = new DateOnly(start_period_law_history.Year, start_period_law_history.Month, last_day_of_month);
        decimal accrued = 0.0m;

        LawHolidaysHistory law_holiday_history =
            new LawHolidaysHistory(law_account_id, start_period_law_history, end_period_law_history, accrued);
        
        //Same with the Agreed holidays history
        
        Guid agreed_account_id = agreed_holidays.id;
        DateOnly start_period_agreed_history = agreed_holidays.earning_period_start;
        int last_day_of_month_agreed = DateTime.DaysInMonth(start_period_agreed_history.Year, start_period_agreed_history.Month);
        DateOnly end_period_agreed_history = new DateOnly(start_period_agreed_history.Year, start_period_agreed_history.Month, last_day_of_month_agreed);
        
        AgreedHolidaysHistory agreed_holiday_history = new AgreedHolidaysHistory(agreed_account_id,  start_period_agreed_history,  end_period_agreed_history, accrued);
        
        
        // insertion
        await db.InsertAsync(law_holiday_history);
        await db.InsertAsync(agreed_holiday_history);
    }
    
}