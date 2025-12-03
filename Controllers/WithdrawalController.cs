using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CunaPay.Api.Services;

namespace CunaPay.Api.Controllers;

[ApiController]
[Route("api/withdrawals")]
[Authorize]
public class WithdrawalController : ControllerBase
{
    private readonly WithdrawalService _withdrawalService;
    private readonly ILogger<WithdrawalController> _logger;

    public WithdrawalController(
        WithdrawalService withdrawalService,
        ILogger<WithdrawalController> logger)
    {
        _withdrawalService = withdrawalService;
        _logger = logger;
    }

    private string GetUserId()
    {
        var userId = User.FindFirst("uid")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User ID not found in token");

        return userId;
    }

    /// <summary>
    /// Obtiene el precio actual de retiro de USDT en BS
    /// </summary>
    [HttpGet("price")]
    public async Task<IActionResult> GetCurrentPrice()
    {
        try
        {
            var price = await _withdrawalService.GetCurrentPriceAsync();
            return Ok(new { ok = true, price = price, currency = "BS" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting withdrawal price");
            return StatusCode(500, new { ok = false, error = "Error getting price" });
        }
    }

    /// <summary>
    /// Crea una nueva solicitud de retiro de USDT
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateWithdrawal([FromBody] CreateWithdrawalRequest request)
    {
        try
        {
            if (request.AmountUsdt <= 0)
                return BadRequest(new { ok = false, error = "Amount must be greater than zero" });

            var userId = GetUserId();
            var withdrawal = await _withdrawalService.CreateWithdrawalAsync(userId, request.AmountUsdt);

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
        catch (ArgumentException ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating withdrawal");
            return StatusCode(500, new { ok = false, error = "Error creating withdrawal" });
        }
    }

    /// <summary>
    /// Obtiene los retiros del usuario actual
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyWithdrawals([FromQuery] string? status = null)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("GetMyWithdrawals called. Extracted userId from token: {UserId}", userId);
            
            var withdrawals = await _withdrawalService.GetUserWithdrawalsAsync(userId, status);
            
            _logger.LogInformation("GetMyWithdrawals returning {Count} withdrawals for userId: {UserId}", withdrawals.Count, userId);
            
            return Ok(new { ok = true, withdrawals });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user withdrawals");
            return StatusCode(500, new { ok = false, error = "Error getting withdrawals" });
        }
    }

    /// <summary>
    /// Obtiene un retiro específico del usuario
    /// </summary>
    [HttpGet("me/{id}")]
    public async Task<IActionResult> GetMyWithdrawal(string id)
    {
        try
        {
            var userId = GetUserId();
            var withdrawal = await _withdrawalService.GetWithdrawalByIdAsync(id, userId);

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
}

public class CreateWithdrawalRequest
{
    public decimal AmountUsdt { get; set; }
}

