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

    public UserService(Database db, HolidayAccountsService holidayAccountsService, HolidayHistoryService holidayHistoryService)
    {
        this.db = db;
        this.holidayAccountsService = holidayAccountsService;
        this.holidayHistoryService = holidayHistoryService;
    }

    public async Task create_user(User user)
    {
        
        PasswordHasher<User> hasher = new PasswordHasher<User>();
        var hashedPassword = hasher.HashPassword(user, user.password);
        user.password = hashedPassword;
        var transaction = db.BeginTransaction();
        try
        {
            User inserted = await db.users.InsertWithOutputAsync(user);
            var (law_holidays, agreed_holidays) = await holidayAccountsService.initialize_users_holidays_accounts(inserted);
            await holidayHistoryService.create_history_records(law_holidays, agreed_holidays);

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
            { name = u.name, email = u.email, role = u.role, salary = u.salary, start_date = u.start_date }).ToListAsync();
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
            .Where(x => x.User.id == userId)
            .Select(x => new UserViewPage
            {
                name = x.User.name,
                email = x.User.email,
                earned_holiday_hours = x.Agreed.earned_holiday_hours + x.Law.earned_holiday_days*24,
                used_holiday_hours = x.Agreed.used_holiday_hours +  x.Law.used_holiday_days*24,
                available_holiday_hours =
                    (x.Agreed.earned_holiday_hours+x.Law.earned_holiday_days) - (x.Agreed.used_holiday_hours +x.Law.used_holiday_days*24)
            })
            .FirstOrDefaultAsync();
    }
}
