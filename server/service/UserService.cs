using api;
using be;
using database;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.AspNetCore.Identity;

namespace service;


public class UserService
{

    private Database db;
    private HolidayAccountsService holidayAccountsService;
    private HolidayHistoryService holidayHistoryService;
    private ScheduleService scheduleService;

    public UserService(Database db, HolidayAccountsService holidayAccountsService, HolidayHistoryService holidayHistoryService, ScheduleService scheduleService)
    {
        this.scheduleService = scheduleService;
        this.db = db;
        this.holidayAccountsService = holidayAccountsService;
        this.holidayHistoryService = holidayHistoryService;
    }

    public async Task create_user(User user, Schedule schedule)
    {
        PasswordHasher<User> hasher = new PasswordHasher<User>();
        var hashedPassword = hasher.HashPassword(user, user.password);
        user.password = hashedPassword;
        var transaction = db.BeginTransaction();
        try
        {
            User inserted = await db.users.InsertWithOutputAsync(user);
            // in the same transaction: a user is never saved without a schedule
            await scheduleService.create_first_schedule(inserted, schedule);
            var (law_holidays, agreed_holidays) = await holidayAccountsService.initialize_users_holidays_accounts(inserted);
            await holidayHistoryService.create_history_records(law_holidays, agreed_holidays, inserted.start_date);

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw new Exception("Failed to create new user", e);
        }
    }

    public async Task<List<ClientUser>> get_all_users()
    {
        List<ClientUser> users = await db.users.Select(u => new ClientUser
            { id = u.id, name = u.name, email = u.email, role = u.role, start_date = u.start_date }).ToListAsync();
        return users;
    }

    public async Task<UserViewPage?> get_user(Guid userId)
    {
        return await db.users
            .Join(db.agreed_holidays,
                u => u.id,
                d => d.user_id,
                (u, d) => new { User = u, Agreed = d })
            .Join(db.law_holidays,
                x => x.User.id,
                l => l.user_id,
                (x, l) => new { x.User, x.Agreed, Law = l })
            .Where(x => x.User.id == userId && x.Agreed.active && x.Law.active)
            .Select(x => new UserViewPage
            {
                name = x.User.name,
                email = x.User.email,
                law_account_id = x.Law.id,
                agreed_account_id = x.Agreed.id,
                agreed_earned_holiday_hours = x.Agreed.earned_holiday_hours,
                agreed_used_holiday_hours = x.Agreed.used_holiday_hours,
                available_holiday_hours =
                    x.Agreed.earned_holiday_hours - x.Agreed.used_holiday_hours,
                agreed_earning_period_start = x.Agreed.earning_period_start,
                law_earning_period_start = x.Law.earning_period_start,
                law_earned_holiday_hours = x.Law.earned_holiday_hours,
                law_used_holiday_hours = x.Law.used_holiday_hours,
                law_available_holiday_hours = x.Law.earned_holiday_hours - x.Law.used_holiday_hours
            })
            .FirstOrDefaultAsync();
    }

    public async Task<Guid?> get_accId(Guid userId, string kind)
    {
        if (string.Equals(kind, "law", StringComparison.OrdinalIgnoreCase))
        {
            return await db.law_holidays
                .Where(account => account.user_id == userId && account.active)
                .Select(account => (Guid?)account.id)
                .FirstOrDefaultAsync();
        }

        if (string.Equals(kind, "agreed", StringComparison.OrdinalIgnoreCase))
        {
            return await db.agreed_holidays
                .Where(account => account.user_id == userId && account.active)
                .Select(account => (Guid?)account.id)
                .FirstOrDefaultAsync();
        }

        return null;
    }
}
