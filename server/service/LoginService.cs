using System.ComponentModel.DataAnnotations;
using database;
using be;
using Microsoft.AspNetCore.Identity;
using LinqToDB.Async;

namespace service;

public class LoginService
{
    public Database db;
    private CurrentDateService currentDateService;

    public LoginService(Database db, CurrentDateService currentDateService)
    {
        this.db = db;
        this.currentDateService = currentDateService;
    }

    public async Task<User> login(User user)
    {
        User login_user = await db.users.FirstOrDefaultAsync(u => u.email == user.email);
        if (login_user == null)
        {
            throw new ValidationException("Make sure that email and password are correct");
        }

        var passwrod_hasher = new PasswordHasher<User>();
        var result = passwrod_hasher.VerifyHashedPassword(login_user, login_user.password, user.password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new ValidationException("Make sure that email and password are correct");
        }

        // an employee who left the company (after their last working day) can not log in anymore
        if (login_user.end_date != null && login_user.end_date < await currentDateService.get_current_date())
        {
            throw new ValidationException($"This account was closed: the employment ended on {login_user.end_date:yyyy-MM-dd}");
        }

        return login_user;


    }
}
