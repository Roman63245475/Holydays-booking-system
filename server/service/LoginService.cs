using System.ComponentModel.DataAnnotations;
using database;
using be;
using Microsoft.AspNetCore.Identity;
using LinqToDB.Async;

namespace service;

public class LoginService
{
    public Database db;

    public LoginService(Database db)
    {
        this.db = db;
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
        
        return login_user;


    }
}
