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
    public async Task<ActionResult<HolidayRequest>> create([FromBody] RequestDTO requestDTO)
    {
        var userValue = HttpContext.Session.GetString("userId");
        if (!Guid.TryParse(userValue, out var userId))
            return Unauthorized();

        if (requestDTO.account_id == Guid.Empty) {
            return BadRequest("A valid account_id is required.");
        }

        if (!DateOnly.TryParseExact(requestDTO.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate)
            || !DateOnly.TryParseExact(requestDTO.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate)
            || endDate < startDate)
        {
            return BadRequest("Invalid holiday request.");
        }

        var request = new RequestDTO()
        {
            user_id = userId,
            account_id = requestDTO.account_id,
            StartDate = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            created_at = DateTime.UtcNow,
            // the hours are calculated by the server from the schedule and days off (RequestService), not sent by the client
            RequestStatus = RequestStatus.Pending
        };

        try
        {
            var createdRequest = await requestService.SendRequest(request);
            return Ok(createdRequest);
        }
        catch (InvalidOperationException e)
        {
            // the request breaks a holiday rule, the message tells the employee why
            return BadRequest(e.Message);
        }
    }

    [HttpGet("request/ongoing")]
    public async Task<ActionResult<List<OngoingHolidayRequest>>> GetOngoingRequests()
    {
        var userValue = HttpContext.Session.GetString("userId");
        if (!Guid.TryParse(userValue, out var userId))
            return Unauthorized("Not logged in");

        return Ok(await requestService.GetOngoingRequests(userId));
    }

    [HttpGet("requests/getRequests")]
    public async Task<ActionResult<List<HolidayRequestUser>>> GetRequests() {
        // ValidateUser does not throw, it returns the 401/403 response, which must be returned to stop the action
        var error = ValidateUser(Role.Accountant);
        if (error != null) return error;
        var requests = await requestService.GetRequests();
        return Ok(requests);
    }

    [HttpPost("requests/declineRequest/{id}")]
    public async Task<IActionResult> DeclineRequest([FromRoute] Guid id) {
        var error = ValidateUser(Role.Accountant);
        if (error != null) return error;
        if (!await requestService.DeclineRequest(id))
            return Conflict("Only a pending request can be declined. This request does not exist or was already handled.");
        return Ok();
    }

    [HttpPost("requests/acceptRequest/{id}")]
    public async Task<IActionResult> AcceptRequest([FromRoute] Guid id) {
        var error = ValidateUser(Role.Accountant);
        if (error != null) return error;
        if (!await requestService.AcceptRequest(id))
            return Conflict("Only a pending request can be approved. This request does not exist or was already handled.");
        return Ok();
    }
}
