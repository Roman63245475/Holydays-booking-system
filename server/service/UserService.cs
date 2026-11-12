using be;
using database;
using LinqToDB;
using Microsoft.AspNetCore.Identity;

namespace service;


public class UserService
{

    private Database db;
    private HolidayAccountsService holidayAccountsService;

    public UserService(Database db, HolidayAccountsService holidayAccountsService)
    {
        this.db = db;
        this.holidayAccountsService = holidayAccountsService;
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
            await holidayAccountsService.initialize_users_holidays_accountsService(inserted);
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw new Exception("Failed to create new user", e);
        }
    }
    
    
}