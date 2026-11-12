using LinqToDB.Mapping;
using be;

namespace database;

public class UserMapping
{
    public static void Configure(FluentMappingBuilder builder)
    {
        builder.Entity<User>().HasTableName("users").HasPrimaryKey(x => x.id);
        builder.Entity<User>().Property(x => x.id).HasColumnName("id").HasDbType("uuid").HasSkipOnInsert().HasSkipOnUpdate();
        builder.Entity<User>().Property(x => x.name).HasColumnName("name").IsNullable(false);
        builder.Entity<User>().Property(x => x.email).HasColumnName("email").IsNullable(false);
        builder.Entity<User>().Property(x => x.password).HasColumnName("password").IsNullable(false);
        builder.Entity<User>().Property(x => x.salary).HasColumnName("salary").IsNullable(false);
        builder.Entity<User>().Property(x => x.role).HasColumnName("role").IsNullable(false);
    }
}
