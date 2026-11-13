using be;
using LinqToDB.Mapping;

namespace database;

public class AgreedHolidaysHistoryMapping
{
    public static void Configure(FluentMappingBuilder builder)
    {
        builder.Entity<AgreedHolidaysHistory>().HasTableName("agreed_holidays_history").HasPrimaryKey(x => x.id);
        builder.Entity<AgreedHolidaysHistory>().Property(x => x.id).HasColumnName("id").HasDbType("uuid").HasSkipOnInsert().HasSkipOnUpdate();
        builder.Entity<AgreedHolidaysHistory>().Property(x => x.account_id).HasColumnName("account_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<AgreedHolidaysHistory>().Property(x => x.period_start).HasColumnName("period_start").IsNullable(false);
        builder.Entity<AgreedHolidaysHistory>().Property(x => x.period_end).HasColumnName("period_end").IsNullable(false);
        builder.Entity<AgreedHolidaysHistory>().Property(x => x.accrued).HasColumnName("accrued").IsNullable(false);

    }
}