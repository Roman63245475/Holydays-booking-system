using be;
using LinqToDB.Mapping;

namespace database;

public class ScheduleMapping
{
    public static void Configure(FluentMappingBuilder builder)
    {
        builder.Entity<Schedule>().HasTableName("schedules").HasPrimaryKey(x => x.id);
        builder.Entity<Schedule>().Property(x => x.id).HasColumnName("id").HasDbType("uuid").HasSkipOnInsert().HasSkipOnUpdate();
        builder.Entity<Schedule>().Property(x => x.user_id).HasColumnName("user_id").HasDbType("uuid").IsNullable(false);
        builder.Entity<Schedule>().Property(x => x.effective_from).HasColumnName("effective_from").IsNullable(false);

        // null start / end = day off
        builder.Entity<Schedule>().Property(x => x.monday_start).HasColumnName("monday_start").HasDbType("time").IsNullable();
        builder.Entity<Schedule>().Property(x => x.monday_end).HasColumnName("monday_end").HasDbType("time").IsNullable();
        builder.Entity<Schedule>().Property(x => x.tuesday_start).HasColumnName("tuesday_start").HasDbType("time").IsNullable();
        builder.Entity<Schedule>().Property(x => x.tuesday_end).HasColumnName("tuesday_end").HasDbType("time").IsNullable();
        builder.Entity<Schedule>().Property(x => x.wednesday_start).HasColumnName("wednesday_start").HasDbType("time").IsNullable();
        builder.Entity<Schedule>().Property(x => x.wednesday_end).HasColumnName("wednesday_end").HasDbType("time").IsNullable();
        builder.Entity<Schedule>().Property(x => x.thursday_start).HasColumnName("thursday_start").HasDbType("time").IsNullable();
        builder.Entity<Schedule>().Property(x => x.thursday_end).HasColumnName("thursday_end").HasDbType("time").IsNullable();
        builder.Entity<Schedule>().Property(x => x.friday_start).HasColumnName("friday_start").HasDbType("time").IsNullable();
        builder.Entity<Schedule>().Property(x => x.friday_end).HasColumnName("friday_end").HasDbType("time").IsNullable();

        // get_hours / get_weekly_hours are methods, they are not mapped to columns
    }
}
