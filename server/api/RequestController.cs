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
}
