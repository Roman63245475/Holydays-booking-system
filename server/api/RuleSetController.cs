using be;
using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

// [ApiController]: if the JSON can not be read into RuleSetDTO (e.g. "" for a time, null for a number),
// ASP.NET answers 400 with the name of the wrong field instead of passing a null DTO to the action
[ApiController]
public class RuleSetController : ControllerBase
{
    private RulesetService rulesetService;

    public RuleSetController(RulesetService rulesetService)
    {
        this.rulesetService = rulesetService;
    }

    // the rules that apply now, in the same shape set_parameters expects (to fill the admin form).
    // any logged in user may read them, the rules are not secret
    [HttpGet(nameof(get_parameters))]
    public async Task<ActionResult<RuleSetDTO>> get_parameters()
    {
        if (HttpContext.Session.GetString("userId") == null)
        {
            return Unauthorized();
        }

        RuleSet rule_set = await rulesetService.get_current_rule_set();
        return Ok(RuleSetDTO.from_rule_set(rule_set));
    }

    // the ready-made rule sets the admin can start from (the form is filled with them)
    [HttpGet(nameof(get_presets))]
    public ActionResult<List<RuleSetPresetDTO>> get_presets()
    {
        if (HttpContext.Session.GetString("userId") == null)
        {
            return Unauthorized();
        }

        return Ok(RuleSetPresets.all.Select(p => new RuleSetPresetDTO
        {
            key = p.key,
            name = p.name,
            description = p.description,
            rules = RuleSetDTO.from_rule_set(p.create(new DateOnly(HolidayParameters.REFERENCE_YEAR, 1, 1)))
        }).ToList());
    }

    // only the system owner (Admin) defines the global holiday rules
    [HttpPost(nameof(set_parameters))]
    public async Task<IActionResult> set_parameters([FromBody] RuleSetDTO ruleSetDTO)
    {
        if (HttpContext.Session.GetString("role") != Role.Admin.ToString())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only the system owner can change the holiday rules");
        }

        string? error = ruleSetDTO.validate();
        if (error != null)
        {
            return BadRequest(error);
        }

        try
        {
            await rulesetService.save_rule_set(ruleSetDTO.to_rule_set());
            return Ok();
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
    }
}
