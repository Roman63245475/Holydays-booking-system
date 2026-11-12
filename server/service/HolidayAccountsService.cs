using System.Runtime.InteropServices.JavaScript;
using be;
using database;
using LinqToDB;

namespace service;

public class HolidayAccountsService
{
    private Database db;

    public HolidayAccountsService(Database db)
    {
        this.db = db;
    }

    public async Task initialize_users_holidays_accountsService(User user)
    {
        DateOnly earningPeriodEnd = user.start_date.Month < 9 ? new DateOnly(DateTime.Today.Year, 8, 31) : new DateOnly(DateTime.Today.Year + 1, 8, 31);
        LawHolidays law_holidays = new LawHolidays(user.id, user.start_date, earningPeriodEnd, new DateOnly(DateTime.Today.Year, 8, 31),  new DateOnly(DateTime.Today.Year + 1, 12, 31), HolidayType.FerieEfterFerieloven, true, 0, 0);
        
        DateOnly b_earning_period_end = new DateOnly(DateTime.Today.Year, 12, 31);
        DateOnly b_usage_period_start = new DateOnly(DateTime.Today.Year + 1, 4, 30);
        DateOnly b_usage_period_end = new DateOnly(DateTime.Today.Year + 2, 5, 1);
        AgreedHolidays agreed_holidays = new AgreedHolidays(user.id, user.start_date, b_earning_period_end, b_usage_period_start,  b_usage_period_end, HolidayType.FerieUge, true, 0, 0);
        
        
        await db.InsertAsync(law_holidays);
        await db.InsertAsync(agreed_holidays);
    }
}