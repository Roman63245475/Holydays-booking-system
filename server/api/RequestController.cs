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
            || !Enum.IsDefined(requestDTO.holidayKind))
        {
            return BadRequest("Invalid holiday request.");
        }

        var request = new HolidayRequest
        {
            user_id = userId,
            StartDate = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            HolidayKind = requestDTO.holidayKind,
            Hours = requestDTO.hours,
            RequestStatus = RequestStatus.Pending
        };

        var createdRequest = await requestService.SendRequest(request);
        return Ok(createdRequest);
    }
}
