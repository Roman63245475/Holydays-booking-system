using System.Collections.Immutable;
using be;
using LinqToDB.Mapping;

namespace database;

public class LawHolidayMapping
{
    public static void Configure(FluentMappingBuilder builder)
    {
        builder.Entity<LawHolidays>().HasTableName("law_holidays").HasPrimaryKey(x => x.id);;
        builder.Entity<LawHolidays>().Property(x => x.id).HasColumnName("id").HasDbType("uuid").HasSkipOnInsert().HasSkipOnUpdate();
        builder.Entity<LawHolidays>().Property(x => x.user_id).HasColumnName("user_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<LawHolidays>().Property(x => x.earning_period_start).HasColumnName("earning_period_start").IsNullable(false);
        builder.Entity<LawHolidays>().Property(x => x.earning_period_end).HasColumnName("earning_period_end").IsNullable(false);
        builder.Entity<LawHolidays>().Property(x => x.usage_period_start).HasColumnName("usage_period_start").IsNullable(false);
        builder.Entity<LawHolidays>().Property(x => x.usage_period_end).HasColumnName("usage_period_end").IsNullable(false);
        builder.Entity<LawHolidays>().Property(x => x.earned_holiday_days).HasColumnName("earned_holiday_days").IsNullable(false);
        builder.Entity<LawHolidays>().Property(x => x.used_holiday_days).HasColumnName("used_holiday_days").IsNullable(false);
        builder.Entity<LawHolidays>().Property(x => x.holiday_type).HasColumnName("holiday_type").IsNullable(false);
        builder.Entity<LawHolidays>().Property(x => x.active).HasColumnName("active").IsNullable(false);
    }
}