using be;
using LinqToDB.Mapping;

namespace database;

public class AgreedHolidayMapping
{
    public static void Configure(FluentMappingBuilder builder)
    {
        builder.Entity<AgreedHolidays>().HasTableName("agreed_holidays").HasPrimaryKey(x => x.id);;
        builder.Entity<AgreedHolidays>().Property(x => x.id).HasColumnName("id").HasDbType("uuid").HasSkipOnInsert().HasSkipOnUpdate();
        builder.Entity<AgreedHolidays>().Property(x => x.user_id).HasColumnName("user_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<AgreedHolidays>().Property(x => x.earning_period_start).HasColumnName("earning_period_start").IsNullable(false);
        builder.Entity<AgreedHolidays>().Property(x => x.earning_period_end).HasColumnName("earning_period_end").IsNullable(false);
        builder.Entity<AgreedHolidays>().Property(x => x.usage_period_start).HasColumnName("usage_period_start").IsNullable(false);
        builder.Entity<AgreedHolidays>().Property(x => x.usage_period_end).HasColumnName("usage_period_end").IsNullable(false);
        builder.Entity<AgreedHolidays>().Property(x => x.earned_holiday_hours).HasColumnName("earned_holiday_hours").IsNullable(false);
        builder.Entity<AgreedHolidays>().Property(x => x.used_holiday_hours).HasColumnName("used_holiday_hours").IsNullable(false);
        builder.Entity<AgreedHolidays>().Property(x => x.holiday_type).HasColumnName("holiday_type").IsNullable(false);
        builder.Entity<AgreedHolidays>().Property(x => x.active).HasColumnName("active").IsNullable(false);
    }
}