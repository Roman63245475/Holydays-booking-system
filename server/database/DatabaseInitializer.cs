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

        // law_holidays
        db.CreateTable<LawHolidays>(
            tableOptions: TableOptions.CreateIfNotExists
        );
        db.Execute(
            "ALTER TABLE law_holidays ALTER COLUMN id SET DEFAULT gen_random_uuid()"
        );
        db.Execute(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_law_holidays_user') THEN
                    ALTER TABLE law_holidays
                        ADD CONSTRAINT fk_law_holidays_user
                        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
                END IF;
            END $$;"
        );
        db.Execute(
            "CREATE INDEX IF NOT EXISTS ix_law_holidays_user_id ON law_holidays (user_id)"
        );

        // agreed_holidays
        db.CreateTable<AgreedHolidays>(
            tableOptions: TableOptions.CreateIfNotExists
        );
        db.Execute(
            "ALTER TABLE agreed_holidays ALTER COLUMN id SET DEFAULT gen_random_uuid()"
        );
        db.Execute(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_agreed_holidays_user') THEN
                    ALTER TABLE agreed_holidays
                        ADD CONSTRAINT fk_agreed_holidays_user
                        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
                END IF;
            END $$;"
        );
        db.Execute(
            "CREATE INDEX IF NOT EXISTS ix_agreed_holidays_user_id ON agreed_holidays (user_id)"
        );

        // law_holidays_history
        db.CreateTable<LawHolidaysHistory>(
            tableOptions: TableOptions.CreateIfNotExists
        );
        db.Execute(
            "ALTER TABLE law_holidays_history ALTER COLUMN id SET DEFAULT gen_random_uuid()"
        );
        db.Execute(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_law_holidays_history_account') THEN
                    ALTER TABLE law_holidays_history
                        ADD CONSTRAINT fk_law_holidays_history_account
                        FOREIGN KEY (account_id) REFERENCES law_holidays(id) ON DELETE CASCADE;
                END IF;
            END $$;"
        );
        db.Execute(
            "CREATE INDEX IF NOT EXISTS ix_law_holidays_history_account_id ON law_holidays_history (account_id)"
        );

        // agreed_holidays_history
        db.CreateTable<AgreedHolidaysHistory>(
            tableOptions: TableOptions.CreateIfNotExists
        );
        db.Execute(
            "ALTER TABLE agreed_holidays_history ALTER COLUMN id SET DEFAULT gen_random_uuid()"
        );
        db.Execute(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_agreed_holidays_history_account') THEN
                    ALTER TABLE agreed_holidays_history
                        ADD CONSTRAINT fk_agreed_holidays_history_account
                        FOREIGN KEY (account_id) REFERENCES agreed_holidays(id) ON DELETE CASCADE;
                END IF;
            END $$;"
        );
        db.Execute(
            "CREATE INDEX IF NOT EXISTS ix_agreed_holidays_history_account_id ON agreed_holidays_history (account_id)"
        );
    }
}
