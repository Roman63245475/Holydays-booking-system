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
        // the system does not handle money (payroll does), so salary was removed from the model.
        // CreateIfNotExists does not change an already existing table, so the old columns are dropped here
        // (otherwise inserting a user fails on the NOT NULL salary column)
        db.Execute(
            "ALTER TABLE users DROP COLUMN IF EXISTS salary, DROP COLUMN IF EXISTS salary_type"
        );

        db.CreateTable<HolidayRequest>(
            tableOptions: TableOptions.CreateIfNotExists
        );
        db.Execute(
            "ALTER TABLE holiday_request ALTER COLUMN id SET DEFAULT gen_random_uuid()"
        );
        // the hours of a request are calculated from the schedule (e.g. 7.5 + 7 = 14.50), they were an integer before.
        // CreateIfNotExists does not change an already existing table, so the type is changed here
        db.Execute(
            "ALTER TABLE holiday_request ALTER COLUMN \"Hours\" TYPE numeric(10,2)"
        );

        db.Execute(@"
            DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'fk_holiday_request_user'
                    ) THEN
                        ALTER TABLE holiday_request
                        ADD CONSTRAINT fk_holiday_request_user
                        FOREIGN KEY (user_id) REFERENCES users(id)
                        ON DELETE CASCADE;
                END IF;
            END $$;"
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

        // usable = the account has not expired yet (see HolidayAccount). CreateIfNotExists does not change an already
        // existing table, so the column is added here for dbs created before it existed
        db.Execute(
            "ALTER TABLE law_holidays ADD COLUMN IF NOT EXISTS usable boolean NOT NULL DEFAULT true"
        );
        db.Execute(
            "ALTER TABLE agreed_holidays ADD COLUMN IF NOT EXISTS usable boolean NOT NULL DEFAULT true"
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

        // schedules
        db.CreateTable<Schedule>(
            tableOptions: TableOptions.CreateIfNotExists
        );
        db.Execute(
            "ALTER TABLE schedules ALTER COLUMN id SET DEFAULT gen_random_uuid()"
        );
        db.Execute(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_schedules_user') THEN
                    ALTER TABLE schedules
                        ADD CONSTRAINT fk_schedules_user
                        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
                END IF;
            END $$;"
        );
        // one user can not have two schedules starting on the same day
        // (the index on (user_id, effective_from) is also used to find the schedule of a user)
        db.Execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_schedules_user_effective_from ON schedules (user_id, effective_from)"
        );
    
        // rule_sets (admin-editable holiday rules with effective_from)
        db.CreateTable<RuleSet>(
            tableOptions: TableOptions.CreateIfNotExists
        );
        
        db.CreateTable<DayOff>(
            tableOptions: TableOptions.CreateIfNotExists
        );
        // the id of a day off is generated by the db (DayOffMapping skips it on insert)
        db.Execute(
            "ALTER TABLE public_holidays ALTER COLUMN id SET DEFAULT gen_random_uuid()"
        );
        // CreateIfNotExists does not change an already existing table, so NOT NULL is set here as well
        db.Execute(
            "ALTER TABLE public_holidays ALTER COLUMN name SET NOT NULL"
        );
        // one day can be a day off only once (also used to find whether a date is a day off)
        db.Execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_public_holidays_date ON public_holidays (date)"
        );
        db.Execute(
            "ALTER TABLE rule_sets ALTER COLUMN id SET DEFAULT gen_random_uuid()"
        );
        // only one rule set can start on a date
        // (the index is also used to find the rule set that applies on a date)
        db.Execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_sets_effective_from ON rule_sets (effective_from)"
        );
        // the first rule set on an empty db: the Business Esbjerg rules, valid "since always" (2000-01-01)
        // until the admin adds a new rule set
        if (!db.rule_sets.Any())
        {
            db.Insert(RuleSetPresets.business_esbjerg(new DateOnly(2000, 1, 1)));
        }
    }
}
