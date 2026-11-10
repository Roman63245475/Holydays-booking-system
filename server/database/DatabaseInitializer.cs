using be;
using LinqToDB;
using LinqToDB.Data;

namespace database;

public class DatabaseInitializer
{
    public static void Initialize(Database db)
    {
        db.CreateTable<User>(
            tableOptions: TableOptions.CreateIfNotExists
        );
        db.Execute(
            "ALTER TABLE users ALTER COLUMN id SET DEFAULT gen_random_uuid()"
        );
        db.Execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS users_email_unique ON users (email)"
        );
    }
}
