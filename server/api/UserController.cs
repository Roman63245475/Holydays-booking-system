using System.Net;
using be;
using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

public class UserController: ControllerBase
{
    
    private UserService userService;
    private CurrentDateService currentDateService;
    private ScheduleService scheduleService;

    public UserController(UserService userService, CurrentDateService currentDateService, ScheduleService scheduleService)
    {
        this.userService = userService;
        this.currentDateService = currentDateService;
        this.scheduleService = scheduleService;
    }

    // null if the logged in user is the system owner (Admin), otherwise the 401 / 403 response
    private ActionResult? require_admin(string action)
    {
        if (HttpContext.Session.GetString("userId") == null)
        {
            return Unauthorized();
        }
        if (HttpContext.Session.GetString("role") != Role.Admin.ToString())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only the system owner can " + action);
        }
        return null;
    }

    private bool is_me(Guid id)
    {
        return HttpContext.Session.GetString("userId") == id.ToString();
    }

    // the schedule that applies now, to prefill the edit form
    [HttpGet("get_user_schedule/{id}")]
    public async Task<ActionResult<ScheduleDTO>> get_user_schedule([FromRoute] Guid id)
    {
        ActionResult? error = require_admin("see working schedules");
        if (error != null) return error;

        DateOnly today = await currentDateService.get_new_user_start_date();
        Schedule? schedule = await scheduleService.get_schedule_for_date(id, today);
        if (schedule == null)
        {
            return NotFound("User or schedule not found");
        }
        return Ok(ScheduleDTO.from_schedule(schedule));
    }

    // edit from the admin's user form. a changed schedule starts a new schedule period from the current
    // (simulated) date, the old schedule stays in the history
    [HttpPut("update_user/{id}")]
    public async Task<IActionResult> update_user([FromRoute] Guid id, [FromBody] UpdateUserDTO userDTO)
    {
        ActionResult? auth_error = require_admin("edit users");
        if (auth_error != null) return auth_error;

        string? error = userDTO.validate();
        if (error != null)
        {
            return BadRequest(error);
        }
        // the system owner can not take away their own admin role, otherwise nobody could manage users anymore
        if (is_me(id) && userDTO.role != Role.Admin)
        {
            return BadRequest("You can not change your own role");
        }

        // same date logic as for a new user: the real date kept inside the open (not yet accrued) month
        DateOnly schedule_date = await currentDateService.get_new_user_start_date();
        try
        {
            await userService.update_user(id, userDTO.name, userDTO.email, userDTO.password, userDTO.role,
                userDTO.schedule.to_schedule(), schedule_date);
            return Ok();
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(e.Message);
        }
    }

    // removes the user and everything that belongs to them from the db (accounts, histories, schedules, requests)
    [HttpDelete("delete_user/{id}")]
    public async Task<IActionResult> delete_user([FromRoute] Guid id)
    {
        ActionResult? auth_error = require_admin("delete users");
        if (auth_error != null) return auth_error;

        if (is_me(id))
        {
            return BadRequest("You can not delete yourself");
        }
        if (!await userService.delete_user(id))
        {
            return NotFound("User not found");
        }
        return Ok();
    }

    [HttpPost(nameof(create))]
    public async Task<IActionResult> create([FromBody] CreateUserDTO userDTO)
    {
        // only the system owner (Admin) creates users. the very first admin is created on startup (Program.cs)
        if (HttpContext.Session.GetString("userId") == null)
        {
            return Unauthorized();
        }
        if (HttpContext.Session.GetString("role") != Role.Admin.ToString())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only the system owner can create users");
        }

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

        // not DateTime.Now: with the time simulator ahead, the user must start in the same open month as everybody else
        DateOnly start_date = await currentDateService.get_new_user_start_date();
        User user = new User(userDTO.name, userDTO.email, userDTO.password, userDTO.role, start_date);
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
    public async Task<ActionResult<List<ClientUser>>> get_all_users()
    {
        // the list of all employees is for the system owner and the accountant only
        string? role = HttpContext.Session.GetString("role");
        if (HttpContext.Session.GetString("userId") == null)
        {
            return Unauthorized();
        }
        if (role != Role.Admin.ToString() && role != Role.Accountant.ToString())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only the system owner and the accountant can see all users");
        }

        return Ok(await userService.get_all_users());
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