using be;
using LinqToDB.Mapping;

namespace database;

public class HolidayRequestMapping
{
    public static void Configure(FluentMappingBuilder builder)
    {
        builder.Entity<HolidayRequest>().HasTableName("holiday_request").HasPrimaryKey(x => x.id);
        builder.Entity<HolidayRequest>().Property(x => x.id).HasColumnName("id").HasDbType("uuid").HasSkipOnInsert().HasSkipOnUpdate();
        builder.Entity<HolidayRequest>().Property(x => x.user_id).HasColumnName("user_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.StartDate).HasColumnName("StartDate").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.EndDate).HasColumnName("EndDate").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x =>x.Hours).HasColumnName("Hours").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.RequestStatus).HasColumnName("status").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.account_id).HasColumnName("account_id").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.created_at).HasColumnName("created_at").IsNullable(false);
   
    }

}