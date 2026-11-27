using be;
using LinqToDB.Mapping;

namespace database;

public class HolidayTransferRequestMapping
{
    public static void Configure(FluentMappingBuilder builder)
    {
        builder.Entity<HolidayTransferRequest>().HasTableName("holiday_transfer_request").HasPrimaryKey(x => x.id);
        builder.Entity<HolidayTransferRequest>().Property(x => x.id).HasColumnName("id").HasDbType("uuid").HasSkipOnInsert().HasSkipOnUpdate();
        builder.Entity<HolidayTransferRequest>().Property(x => x.user_id).HasColumnName("user_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<HolidayTransferRequest>().Property(x => x.from_account_id).HasColumnName("from_account_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<HolidayTransferRequest>().Property(x => x.to_account_id).HasColumnName("to_account_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<HolidayTransferRequest>().Property(x => x.holiday_type).HasColumnName("holiday_type").IsNullable(false);
        builder.Entity<HolidayTransferRequest>().Property(x => x.hours).HasColumnName("hours").HasDbType("numeric(10,2)").IsNullable(false);
        builder.Entity<HolidayTransferRequest>().Property(x => x.deadline).HasColumnName("deadline").IsNullable(false);
        builder.Entity<HolidayTransferRequest>().Property(x => x.comment).HasColumnName("comment").IsNullable(true);
        builder.Entity<HolidayTransferRequest>().Property(x => x.status).HasColumnName("status").IsNullable(false);
        builder.Entity<HolidayTransferRequest>().Property(x => x.created_at).HasColumnName("created_at").IsNullable(false);
        builder.Entity<HolidayTransferRequest>().Property(x => x.decided_at).HasColumnName("decided_at").IsNullable(true);
        builder.Entity<HolidayTransferRequest>().Property(x => x.decided_by).HasColumnName("decided_by").HasDbType("uuid").IsNullable(true);
        builder.Entity<HolidayTransferRequest>().Property(x => x.note).HasColumnName("note").IsNullable(true);
    }
}
