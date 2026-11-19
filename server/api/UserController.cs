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
        if (userDTO.schedule == null)
        {
            return BadRequest("schedule is required");
        }

        string? schedule_error = userDTO.schedule.validate();
        if (schedule_error != null)
        {
            return BadRequest(schedule_error);
        }

        Schedule schedule = userDTO.schedule.to_schedule();

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        User user = new User(userDTO.name, userDTO.email, userDTO.password, userDTO.role, today);
        try
        {
            await userService.create_user(user, schedule);
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
    public async Task<ActionResult<UserViewPage>> me()
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