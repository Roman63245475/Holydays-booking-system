using System.Net;
using be;
using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

public class UserController: ControllerBase
{
    
    private UserService userService;

    public UserController(UserService userService)
    {
        this.userService = userService;
    }

    [HttpPost(nameof(create))]
    public async Task<IActionResult> create([FromBody] CreateUserDTO userDTO)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        User user = new User(userDTO.name, userDTO.email, userDTO.password, userDTO.role, userDTO.salary, today);
        try
        {
            await userService.create_user(user);
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpGet(nameof(get_all_users))]
    public async Task<List<ClientUser>> get_all_users()
    {
        return await userService.get_all_users();
    }

    [HttpGet(nameof(me))]
    /**
     * An endpoint created to get the certain users data from their
     * session after they logged in.
     */
    public async Task<ActionResult<ClientUser>> me()
    {
        var userValue = HttpContext.Session.GetString("userId");
        if (!Guid.TryParse(userValue, out var userId))
            return Unauthorized();

        var user = await userService.get_user(userId);
        if (user is null)
        {
            return Unauthorized();
        }
        return Ok(user);
    }
    
}