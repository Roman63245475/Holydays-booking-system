using be;
using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

// transfer of unused holiday to the next holiday period (employee asks, accountant decides) and the accountant's
// overview of all balances
public class TransferController : ControllerBase
{
    private readonly TransferService transferService;
    private readonly AccountStatusService accountStatusService;

    public TransferController(TransferService transferService, AccountStatusService accountStatusService)
    {
        this.transferService = transferService;
        this.accountStatusService = accountStatusService;
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(HttpContext.Session.GetString("userId"), out userId);
    }

    // null if the logged in user has one of the roles, otherwise the 401 / 403 response
    private ActionResult? RequireRole(params Role[] roles)
    {
        if (!TryGetUserId(out _))
        {
            return Unauthorized("Not logged in");
        }
        string? role = HttpContext.Session.GetString("role");
        if (!roles.Any(r => r.ToString() == role))
        {
            return StatusCode(403, "You don't have enough permissions to do this operation.");
        }
        return null;
    }

    [HttpPost("transfer/create")]
    public async Task<ActionResult<HolidayTransferRequest>> CreateTransfer([FromBody] TransferDTO dto)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized("Not logged in");
        // null when the body could not be read (e.g. an invalid account id)
        if (dto == null || dto.account_id == Guid.Empty) return BadRequest("A valid account_id and hours are required.");
        if (dto.comment != null && dto.comment.Length > 500)
        {
            return BadRequest("The comment can be at most 500 characters long.");
        }
        try
        {
            return Ok(await transferService.request_transfer(userId, dto.account_id, dto.hours, dto.comment));
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

    [HttpPost("transfer/{id}/withdraw")]
    public async Task<IActionResult> WithdrawTransfer([FromRoute] Guid id)
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized("Not logged in");
        try
        {
            await transferService.withdraw(userId, id);
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

    // the logged in user's transfer requests, newest first
    [HttpGet("transfer/mine")]
    public async Task<ActionResult<List<TransferRequestView>>> GetMyTransfers()
    {
        if (!TryGetUserId(out Guid userId)) return Unauthorized("Not logged in");
        return Ok(await transferService.get_user_transfers(userId));
    }

    [HttpGet("transfers/pending")]
    public async Task<ActionResult<List<TransferRequestView>>> GetPendingTransfers()
    {
        var error = RequireRole(Role.Accountant);
        if (error != null) return error;
        return Ok(await transferService.get_pending());
    }

    [HttpPost("transfers/accept/{id}")]
    public async Task<IActionResult> AcceptTransfer([FromRoute] Guid id)
    {
        var error = RequireRole(Role.Accountant);
        if (error != null) return error;
        TryGetUserId(out Guid accountantId);
        try
        {
            await transferService.accept(id, accountantId);
            return Ok();
        }
        catch (RequestStateException e)
        {
            return Conflict(e.Message);
        }
        catch (InvalidOperationException e)
        {
            return Conflict("The transfer can not be approved: " + e.Message);
        }
    }

    [HttpPost("transfers/decline/{id}")]
    public async Task<IActionResult> DeclineTransfer([FromRoute] Guid id, [FromBody] DeclineTransferDTO dto)
    {
        var error = RequireRole(Role.Accountant);
        if (error != null) return error;
        if (dto?.note != null && dto.note.Length > 500)
        {
            return BadRequest("The note can be at most 500 characters long.");
        }
        TryGetUserId(out Guid accountantId);
        try
        {
            await transferService.decline(id, accountantId, dto?.note);
            return Ok();
        }
        catch (RequestStateException e)
        {
            return Conflict(e.Message);
        }
    }

    // every balance of every employee with its flag (accountant and system owner)
    [HttpGet("balances/overview")]
    public async Task<ActionResult<List<EmployeeBalanceRow>>> GetBalanceOverview()
    {
        var error = RequireRole(Role.Accountant, Role.Admin);
        if (error != null) return error;
        return Ok(await accountStatusService.get_overview());
    }
}
