using System.Globalization;
using be;
using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

public class RequestController : ControllerBase
{
    private readonly RequestService requestService;

    public RequestController(RequestService requestService)
    {
        this.requestService = requestService;
    }

    [HttpPost("request/create")]
    public async Task<IActionResult> create([FromBody] CreateRequestDTO requestDTO)
    {
        var userValue = HttpContext.Session.GetString("userId");
        if (!Guid.TryParse(userValue, out var userId))
            return Unauthorized();

        if (!DateOnly.TryParseExact(requestDTO.startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate)
            || !DateOnly.TryParseExact(requestDTO.endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate)
            || endDate < startDate
            || requestDTO.hours < 0
            || !Enum.IsDefined(requestDTO.holidayType))
        {
            return BadRequest("Invalid holiday request.");
        }

        var request = new HolidayRequest
        {
            user_id = userId,
            StartDate = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            HolidayType = requestDTO.holidayType,
            Hours = requestDTO.hours,
            RequestStatus = RequestStatus.Pending
        };

        var createdRequest = await requestService.SendRequest(request);
        return Ok(createdRequest);
    }

    [HttpGet("requests/getRequests")]
    public async Task<ActionResult<List<HolidayRequestUser>>> GetRequests() {
        var userId = HttpContext.Session.GetString("userId");
        var role = HttpContext.Session.GetString("role");
        
        if (string.IsNullOrEmpty(userId)) {
            return Unauthorized("Not logged in");
        }
        if (role != nameof(Role.Accountant)) {
            return StatusCode(403, "You don't have enough permissions to access this request.");
        }
        
        var requests = await requestService.GetRequests();
        return Ok(requests);
    }

    [HttpPost("requests/declineRequest/{id}")]
    public async Task<IActionResult> DeclineRequest([FromRoute] Guid id) {
        var userId = HttpContext.Session.GetString("userId");
        var role = HttpContext.Session.GetString("role");
        
        if (string.IsNullOrEmpty(userId)) {
            return Unauthorized("Not logged in");
        }
        if (role != nameof(Role.Accountant)) {
            return StatusCode(403, "You don't have enough permissions to do this operation.");
        }

        await requestService.DeclineRequest(id);
        return Ok();
    }
}
