using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

public class TimeController : ControllerBase
{

    private AccuralService accuralService;

    public TimeController(AccuralService accuralService)
    {
        this.accuralService = accuralService;
    }

    [HttpPost(nameof(next_month))]
    public async Task<IActionResult> next_month()
    {
        try
        {
            await accuralService.accrue_holidays();
            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest();
        }
    }
}