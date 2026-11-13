using be;
using LinqToDB;
using LinqToDB.Data;

namespace database;

public class Database: DataConnection
{

    public Database(DataOptions<Database> options) : base(options.Options)
    {

    }

    public ITable<User> users => this.GetTable<User>();
    public ITable<LawHolidays> law_holidays => this.GetTable<LawHolidays>();
    public ITable<AgreedHolidays> agreed_holidays => this.GetTable<AgreedHolidays>();
    public ITable<LawHolidaysHistory> law_holidays_history => this.GetTable<LawHolidaysHistory>();
    public ITable<AgreedHolidaysHistory> agreed_holidays_history => this.GetTable<AgreedHolidaysHistory>();
    
}
