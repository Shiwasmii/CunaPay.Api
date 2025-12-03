using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CunaPay.Api.Services;
using CunaPay.Api.Attributes;

namespace CunaPay.Api.Controllers;

[ApiController]
[Route("api/admin/withdrawals")]
[Authorize]
[AuthorizeRole("Admin")]
public class AdminWithdrawalController : ControllerBase
{
    private readonly WithdrawalService _withdrawalService;
    private readonly ILogger<AdminWithdrawalController> _logger;

    public AdminWithdrawalController(
        WithdrawalService withdrawalService,
        ILogger<AdminWithdrawalController> logger)
    {
        _withdrawalService = withdrawalService;
        _logger = logger;
    }

    private string GetAdminId()
    {
        var adminId = User.FindFirst("uid")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(adminId))
            throw new UnauthorizedAccessException("Admin ID not found in token");

        return adminId;
    }

    /// <summary>
    /// Obtiene todas las solicitudes de retiro (para admin)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllWithdrawals(
        [FromQuery] string? status = null,
        [FromQuery] int? limit = null)
    {
        try
        {
            var withdrawals = await _withdrawalService.GetAllWithdrawalsAsync(status, limit);
            return Ok(new { ok = true, withdrawals, count = withdrawals.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all withdrawals");
            return StatusCode(500, new { ok = false, error = "Error getting withdrawals" });
        }
    }

    /// <summary>
    /// Obtiene un retiro específico (para admin)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetWithdrawal(string id)
    {
        try
        {
            var withdrawal = await _withdrawalService.GetWithdrawalByIdAsync(id);

            if (withdrawal == null)
                return NotFound(new { ok = false, error = "Withdrawal not found" });

            return Ok(new { ok = true, withdrawal });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting withdrawal");
            return StatusCode(500, new { ok = false, error = "Error getting withdrawal" });
        }
    }

    /// <summary>
    /// Aprueba un retiro y transfiere USDT del usuario al admin
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveWithdrawal(string id, [FromBody] ApproveWithdrawalRequest? request = null)
    {
        try
        {
            var adminId = GetAdminId();
            
            var withdrawal = await _withdrawalService.ApproveWithdrawalAsync(
                id, 
                adminId, 
                request?.Notes
            );

            return Ok(new 
            { 
                ok = true, 
                withdrawal,
                message = $"Withdrawal approved. {withdrawal.AmountUsdt} USDT transferred from user wallet to admin wallet"
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ok = false, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving withdrawal");
            return StatusCode(500, new { ok = false, error = "Error approving withdrawal" });
        }
    }

    /// <summary>
    /// Rechaza un retiro
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectWithdrawal(string id, [FromBody] RejectWithdrawalRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { ok = false, error = "Reason is required" });

            var adminId = GetAdminId();
            var withdrawal = await _withdrawalService.RejectWithdrawalAsync(id, adminId, request.Reason);

            return Ok(new { ok = true, withdrawal });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ok = false, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting withdrawal");
            return StatusCode(500, new { ok = false, error = "Error rejecting withdrawal" });
        }
    }

    /// <summary>
    /// Obtiene estadísticas de retiros
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var allWithdrawals = await _withdrawalService.GetAllWithdrawalsAsync();
            
            var stats = new
            {
                total = allWithdrawals.Count,
                pending = allWithdrawals.Count(w => w.Status == "pending"),
                completed = allWithdrawals.Count(w => w.Status == "completed"),
                rejected = allWithdrawals.Count(w => w.Status == "rejected"),
                totalUsdt = allWithdrawals.Where(w => w.Status == "completed").Sum(w => w.AmountUsdt),
                totalBs = allWithdrawals.Where(w => w.Status == "completed").Sum(w => w.AmountBs)
            };

            return Ok(new { ok = true, stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stats");
            return StatusCode(500, new { ok = false, error = "Error getting stats" });
        }
    }
}

public class ApproveWithdrawalRequest
{
    public string? Notes { get; set; }
}

public class RejectWithdrawalRequest
{
    public string Reason { get; set; } = string.Empty;
}

