using be;
using LinqToDB.Mapping;

namespace database;

public class HolidayCancellationRequestMapping
{
    public static void Configure(FluentMappingBuilder builder)
    {
        builder.Entity<HolidayCancellationRequest>().HasTableName("holiday_cancellation_request").HasPrimaryKey(x => x.id);
        builder.Entity<HolidayCancellationRequest>().Property(x => x.id).HasColumnName("id").HasDbType("uuid").HasSkipOnInsert().HasSkipOnUpdate();
        builder.Entity<HolidayCancellationRequest>().Property(x => x.holiday_request_id).HasColumnName("holiday_request_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<HolidayCancellationRequest>().Property(x => x.user_id).HasColumnName("user_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<HolidayCancellationRequest>().Property(x => x.reason).HasColumnName("reason").IsNullable(true);
        builder.Entity<HolidayCancellationRequest>().Property(x => x.status).HasColumnName("status").IsNullable(false);
        builder.Entity<HolidayCancellationRequest>().Property(x => x.created_at).HasColumnName("created_at").IsNullable(false);
        builder.Entity<HolidayCancellationRequest>().Property(x => x.decided_at).HasColumnName("decided_at").IsNullable(true);
    }
}
