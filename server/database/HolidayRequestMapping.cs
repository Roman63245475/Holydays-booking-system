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
        builder.Entity<HolidayRequest>().Property(x => x.Hours).HasColumnName("Hours").HasDbType("numeric(10,2)").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.RequestStatus).HasColumnName("status").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.account_id).HasColumnName("account_id").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.created_at).HasColumnName("created_at").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.is_main_holiday).HasColumnName("is_main_holiday").IsNullable(false);
        builder.Entity<HolidayRequest>().Property(x => x.updated_at).HasColumnName("updated_at").IsNullable(true);
        builder.Entity<HolidayRequest>().Property(x => x.decided_at).HasColumnName("decided_at").IsNullable(true);
        builder.Entity<HolidayRequest>().Property(x => x.replaces_request_id).HasColumnName("replaces_request_id").HasDbType("uuid").IsNullable(true);
   
    }

}