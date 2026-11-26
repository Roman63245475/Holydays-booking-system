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
        try
        {
            await requestService.AcceptRequest(id);
            return Ok();
        }
        catch (RequestStateException e)
        {
            return Conflict(e.Message);
        }
        catch (InvalidOperationException e)
        {
            // the request is checked again before approval, e.g. the balance is not enough anymore
            return Conflict("The request can not be approved: " + e.Message);
        }
    }

    // Edit: new dates for a pending request of the logged in user. the old request becomes Withdrawn,
    // a new pending request with the new dates replaces it
    [HttpPut("request/{id}/edit")]
    public async Task<ActionResult<HolidayRequest>> EditRequest([FromRoute] Guid id, [FromBody] EditRequestDTO dto)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized("Not logged in");
        if (!TryParseDates(dto.StartDate, dto.EndDate, out DateOnly start, out DateOnly end))
        {
            return BadRequest("Invalid holiday dates.");
        }

        try
        {
            return Ok(await requestService.EditRequest(userId, id, start, end));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (RequestStateException e)
        {
            return Conflict(e.Message);
        }
        catch (InvalidOperationException e)
        {
            // the new dates break a holiday rule, nothing was changed
            return BadRequest(e.Message);
        }
    }

    // the logged in user takes back a pending request (no new request is created)
    [HttpPost("request/{id}/withdraw")]
    public async Task<IActionResult> WithdrawRequest([FromRoute] Guid id)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized("Not logged in");
        try
        {
            await requestService.WithdrawRequest(userId, id);
            return Ok();
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (RequestStateException e)
        {
            return Conflict(e.Message);
        }
    }

    // approved holidays of the logged in user that are not over yet
    [HttpGet("request/approved")]
    public async Task<ActionResult<List<OngoingHolidayRequest>>> GetApprovedRequests()
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized("Not logged in");
        return Ok(await requestService.GetApprovedRequests(userId));
    }

    // the logged in user asks to cancel one of their approved holidays (the accountant decides)
    [HttpPost("request/{id}/cancel")]
    public async Task<ActionResult<HolidayCancellationRequest>> CancelApprovedRequest([FromRoute] Guid id, [FromBody] CancelRequestDTO dto)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized("Not logged in");
        if (dto.Reason != null && dto.Reason.Length > 500)
        {
            return BadRequest("The reason can be at most 500 characters long.");
        }

        try
        {
            return Ok(await requestService.RequestCancellation(userId, id, dto.Reason));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (RequestStateException e)
        {
            return Conflict(e.Message);
        }
    }

    [HttpGet("requests/getCancellationRequests")]
    public async Task<ActionResult<List<CancellationRequestView>>> GetCancellationRequests()
    {
        var error = ValidateUser(Role.Accountant);
        if (error != null) return error;
        return Ok(await requestService.GetCancellationRequests());
    }

    [HttpPost("requests/acceptCancellation/{id}")]
    public async Task<IActionResult> AcceptCancellation([FromRoute] Guid id)
    {
        var error = ValidateUser(Role.Accountant);
        if (error != null) return error;
        try
        {
            await requestService.AcceptCancellation(id);
            return Ok();
        }
        catch (RequestStateException e)
        {
            return Conflict(e.Message);
        }
    }

    [HttpPost("requests/declineCancellation/{id}")]
    public async Task<IActionResult> DeclineCancellation([FromRoute] Guid id)
    {
        var error = ValidateUser(Role.Accountant);
        if (error != null) return error;
        try
        {
            await requestService.DeclineCancellation(id);
            return Ok();
        }
        catch (RequestStateException e)
        {
            return Conflict(e.Message);
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(HttpContext.Session.GetString("userId"), out userId);
    }

    private static bool TryParseDates(string? start_text, string? end_text, out DateOnly start, out DateOnly end)
    {
        end = default;
        return DateOnly.TryParseExact(start_text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)
               && DateOnly.TryParseExact(end_text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end)
               && end >= start;
    }
}
