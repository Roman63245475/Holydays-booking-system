using be;
using LinqToDB.Mapping;

namespace database;

public class DayOffMapping
{
    public static void Configure(FluentMappingBuilder builder)
    {
        builder.Entity<DayOff>().HasTableName("public_holidays").HasPrimaryKey(x => x.id);
        builder.Entity<DayOff>().Property(x => x.id).HasColumnName("id").HasDbType("uuid").HasSkipOnInsert().HasSkipOnUpdate();
        builder.Entity<DayOff>().Property(x => x.name).HasColumnName("name").IsNullable(false);
        builder.Entity<DayOff>().Property(x => x.date).HasColumnName("date").IsNullable(false);
        builder.Entity<DayOff>().Property(x => x.holiday_type).HasColumnName("holiday_type").IsNullable(false);
    }
}