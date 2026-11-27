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
    private ProjectionService projectionService;

    public UserController(UserService userService, CurrentDateService currentDateService, ScheduleService scheduleService,
        ProjectionService projectionService)
    {
        this.projectionService = projectionService;
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

    // null if the logged in user is the system owner, or the accountant and the user `id` is an employee (the
    // accountant can see and edit employees and their working schedule, nothing else), otherwise the 401 / 403 / 404
    private async Task<ActionResult?> require_admin_or_accountant_for_employee(Guid id, string action)
    {
        string? role = HttpContext.Session.GetString("role");
        if (HttpContext.Session.GetString("userId") == null)
        {
            return Unauthorized();
        }
        if (role == Role.Admin.ToString())
        {
            return null;
        }
        if (role != Role.Accountant.ToString())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only the system owner and the accountant can " + action);
        }
        ClientUser? target = (await userService.get_all_users()).FirstOrDefault(u => u.id == id);
        if (target == null)
        {
            return NotFound("User not found");
        }
        if (target.role != Role.User)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "The accountant can only " + action + " of employees");
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
        ActionResult? error = await require_admin_or_accountant_for_employee(id, "see working schedules");
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
        // the system owner edits every user, the accountant only employees (their data and working schedule)
        ActionResult? auth_error = await require_admin_or_accountant_for_employee(id, "edit users");
        if (auth_error != null) return auth_error;

        string? error = userDTO.validate();
        if (error != null)
        {
            return BadRequest(error);
        }

        // same date logic as for a new user: the real date kept inside the open (not yet accrued) month
        DateOnly schedule_date = await currentDateService.get_new_user_start_date();
        try
        {
            await userService.update_user(id, userDTO.name, userDTO.email, userDTO.password, userDTO.role,
                userDTO.schedule?.to_schedule(), schedule_date);
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

    // the system owner saves the last working day of an employee who leaves the company, or calls it off (empty).
    // returns what was changed (cancelled holidays, declined requests / transfers)
    [HttpPut("set_end_date/{id}")]
    public async Task<ActionResult<LeavingResult>> set_end_date([FromRoute] Guid id, [FromBody] SetEndDateDTO dto)
    {
        ActionResult? auth_error = require_admin("register that an employee leaves");
        if (auth_error != null) return auth_error;
        if (is_me(id))
        {
            return BadRequest("You can not set your own last working day");
        }

        if (dto == null) return BadRequest("end_date must be yyyy-MM-dd or null");
        DateOnly? end_date = null;
        if (!string.IsNullOrWhiteSpace(dto.end_date))
        {
            if (!DateOnly.TryParseExact(dto.end_date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateOnly parsed))
            {
                return BadRequest("The date must be yyyy-MM-dd");
            }
            end_date = parsed;
        }

        Guid.TryParse(HttpContext.Session.GetString("userId"), out Guid admin_id);
        try
        {
            return Ok(await userService.set_end_date(id, end_date, admin_id));
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

        if (userDTO == null || !Enum.IsDefined(typeof(Role), userDTO.role))
        {
            return BadRequest("role is not valid");
        }

        // only an employee has a working schedule and holiday. the accountant and the system owner are role
        // accounts (separate logins), the person behind them has their own employee account for their holiday
        Schedule? schedule = null;
        if (userDTO.role == Role.User)
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
            schedule = userDTO.schedule.to_schedule();
        }

        // not DateTime.Now: with the time simulator ahead, the user must start in the same open month as everybody else
        DateOnly start_date = await currentDateService.get_new_user_start_date();
        User user = new User(userDTO.name, userDTO.email, userDTO.password, userDTO.role, start_date);
        try
        {
            await userService.create_user(user, schedule);
            return Ok();
        }
        catch (InvalidOperationException e)
        {
            // e.g. there is already an accountant account
            return BadRequest(e.Message);
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

    // the page of one user for the system owner: data, current schedule and holiday accounts
    [HttpGet("user_details/{id}")]
    public async Task<ActionResult<UserDetailsDTO>> get_user_details([FromRoute] Guid id)
    {
        ActionResult? error = await require_admin_or_accountant_for_employee(id, "see the details of users");
        if (error != null) return error;

        ClientUser? user = (await userService.get_all_users()).FirstOrDefault(u => u.id == id);
        UserViewPage? page = await userService.get_user(id);
        if (user == null || page == null)
        {
            return NotFound("User not found");
        }

        DateOnly today = await currentDateService.get_current_date();
        Schedule? schedule = await scheduleService.get_schedule_for_date(id, today);
        return Ok(new UserDetailsDTO
        {
            id = user.id,
            name = user.name,
            email = user.email,
            role = user.role,
            start_date = user.start_date,
            end_date = user.end_date,
            weekly_hours = schedule?.get_weekly_hours() ?? 0,
            schedule = schedule != null ? ScheduleDTO.from_schedule(schedule) : null,
            page = page
        });
    }

    // the logged in user's holiday on a future date (date = "yyyy-MM-dd"): today's balances - pending requests
    // + what will be earned until then. a projection only, nothing is saved
    [HttpGet(nameof(projected_balance))]
    public async Task<ActionResult<ProjectedBalanceView>> projected_balance([FromQuery] string date)
    {
        if (!Guid.TryParse(HttpContext.Session.GetString("userId"), out Guid userId))
        {
            return Unauthorized();
        }
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateOnly day))
        {
            return BadRequest("The date must be yyyy-MM-dd");
        }
        try
        {
            return Ok(await projectionService.get_projected_balance(userId, day));
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(e.Message);
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(e.Message);
        }
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