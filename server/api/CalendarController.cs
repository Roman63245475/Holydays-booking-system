using be;
using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

public class CalendarController : ControllerBase
{
    private readonly CalendarService calendarService;

    public CalendarController(CalendarService calendarService)
    {
        this.calendarService = calendarService;
    }

    // one month of the holiday calendar, for every logged in user (employees see their colleagues' holidays too)
    [HttpGet("calendar")]
    public async Task<ActionResult<CalendarMonthView>> get_month([FromQuery] int year, [FromQuery] int month)
    {
        if (!Guid.TryParse(HttpContext.Session.GetString("userId"), out Guid userId)
            || !Enum.TryParse(HttpContext.Session.GetString("role"), out Role role))
        {
            return Unauthorized("Not logged in");
        }
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
        {
            return BadRequest("Invalid month");
        }
        return Ok(await calendarService.get_month(userId, role, year, month));
    }
}
