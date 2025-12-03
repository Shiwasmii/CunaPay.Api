using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CunaPay.Api.Services;
using CunaPay.Api.Attributes;

namespace CunaPay.Api.Controllers;

[ApiController]
[Route("api/admin/purchases")]
[Authorize]
[AuthorizeRole("Admin")]
public class AdminPurchaseController : ControllerBase
{
    private readonly PurchaseService _purchaseService;
    private readonly AdminWalletService _adminWalletService;
    private readonly ILogger<AdminPurchaseController> _logger;

    public AdminPurchaseController(
        PurchaseService purchaseService,
        AdminWalletService adminWalletService,
        ILogger<AdminPurchaseController> logger)
    {
        _purchaseService = purchaseService;
        _adminWalletService = adminWalletService;
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
    /// Obtiene todas las solicitudes de compra (para admin)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllPurchases(
        [FromQuery] string? status = null,
        [FromQuery] int? limit = null)
    {
        try
        {
            var purchases = await _purchaseService.GetAllPurchasesAsync(status, limit);
            return Ok(new { ok = true, purchases, count = purchases.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all purchases");
            return StatusCode(500, new { ok = false, error = "Error getting purchases" });
        }
    }

    /// <summary>
    /// Obtiene una compra específica (para admin)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPurchase(string id)
    {
        try
        {
            var purchase = await _purchaseService.GetPurchaseByIdAsync(id);

            if (purchase == null)
                return NotFound(new { ok = false, error = "Purchase not found" });

            return Ok(new { ok = true, purchase });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting purchase");
            return StatusCode(500, new { ok = false, error = "Error getting purchase" });
        }
    }

    /// <summary>
    /// Aprueba una compra y deposita USDT al usuario
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApprovePurchase(string id, [FromBody] ApprovePurchaseRequest? request = null)
    {
        try
        {
            var adminId = GetAdminId();
            
            // Asegurar que el admin tenga wallet
            await _adminWalletService.EnsureAdminWalletAsync(adminId);
            
            var purchase = await _purchaseService.ApprovePurchaseAsync(
                id, 
                adminId, 
                request?.Notes
            );

            return Ok(new 
            { 
                ok = true, 
                purchase,
                message = $"Purchase approved. {purchase.AmountUsdt} USDT transferred from admin wallet to user {purchase.UserId}"
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
            _logger.LogError(ex, "Error approving purchase");
            return StatusCode(500, new { ok = false, error = "Error approving purchase" });
        }
    }

    /// <summary>
    /// Rechaza una compra
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectPurchase(string id, [FromBody] RejectPurchaseRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { ok = false, error = "Reason is required" });

            var adminId = GetAdminId();
            var purchase = await _purchaseService.RejectPurchaseAsync(id, adminId, request.Reason);

            return Ok(new { ok = true, purchase });
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
            _logger.LogError(ex, "Error rejecting purchase");
            return StatusCode(500, new { ok = false, error = "Error rejecting purchase" });
        }
    }

    /// <summary>
    /// Obtiene estadísticas de compras
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var allPurchases = await _purchaseService.GetAllPurchasesAsync();
            
            var stats = new
            {
                total = allPurchases.Count,
                pending = allPurchases.Count(p => p.Status == "pending"),
                accepted = allPurchases.Count(p => p.Status == "accepted"),
                rejected = allPurchases.Count(p => p.Status == "rejected"),
                totalUsdt = allPurchases.Where(p => p.Status == "accepted").Sum(p => p.AmountUsdt),
                totalBs = allPurchases.Where(p => p.Status == "accepted").Sum(p => p.AmountBs)
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

public class ApprovePurchaseRequest
{
    public string? Notes { get; set; }
}

public class RejectPurchaseRequest
{
    public string Reason { get; set; } = string.Empty;
}

