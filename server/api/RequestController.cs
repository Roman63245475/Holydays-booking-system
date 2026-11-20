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

    private ActionResult? ValidateUser(Role userRole) {
        var userId = HttpContext.Session.GetString("userId");
        var role = HttpContext.Session.GetString("role");
        
        if (string.IsNullOrEmpty(userId)) {
            return Unauthorized("Not logged in");
        }
        if (role != userRole.ToString()) {
            return StatusCode(403, "You don't have enough permissions to do this operation.");
        }

        return null;
    }

    [HttpPost("request/create")]
    public async Task<IActionResult> create([FromBody] CreateRequestDTO requestDTO)
    {
        var userValue = HttpContext.Session.GetString("userId");
        if (!Guid.TryParse(userValue, out var userId))
            return Unauthorized();

        if (!string.Equals(requestDTO.HolidayKind, "law", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(requestDTO.HolidayKind, "agreed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Holiday kind must be 'law' or 'agreed'.");
        }

        if (!DateOnly.TryParseExact(requestDTO.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate)
            || !DateOnly.TryParseExact(requestDTO.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate)
            || endDate < startDate
            || requestDTO.Hours < 0)
        {
            return BadRequest("Invalid holiday request.");
        }

        var request = new CreateRequestDTO()
        {
            user_id = userId,
            StartDate = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            created_at = DateTime.UtcNow,
            Hours = requestDTO.Hours,
            HolidayKind = requestDTO.HolidayKind,
            RequestStatus = RequestStatus.Pending
        };

        var createdRequest = await requestService.SendRequest(request);
        return Ok(createdRequest);
    }

    [HttpGet("requests/getRequests")]
    public async Task<ActionResult<List<HolidayRequestUser>>> GetRequests() {
        ValidateUser(Role.Accountant);
        var requests = await requestService.GetRequests();
        return Ok(requests);
    }

    [HttpPost("requests/declineRequest/{id}")]
    public async Task<IActionResult> DeclineRequest([FromRoute] Guid id) {
        ValidateUser(Role.Accountant);
        await requestService.DeclineRequest(id);
        return Ok();
    }

    [HttpPost("requests/acceptRequest/{id}")]
    public async Task<IActionResult> AcceptRequest([FromRoute] Guid id) {
        
        ValidateUser(Role.Accountant);
        await requestService.AcceptRequest(id);
        return Ok();
    }
}
