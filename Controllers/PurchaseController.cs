using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CunaPay.Api.Services;

namespace CunaPay.Api.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize]
public class PurchaseController : ControllerBase
{
    private readonly PurchaseService _purchaseService;
    private readonly ILogger<PurchaseController> _logger;

    public PurchaseController(
        PurchaseService purchaseService,
        ILogger<PurchaseController> logger)
    {
        _purchaseService = purchaseService;
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
    /// Obtiene el precio actual de USDT en BS
    /// </summary>
    [HttpGet("price")]
    public async Task<IActionResult> GetCurrentPrice()
    {
        try
        {
            var price = await _purchaseService.GetCurrentPriceAsync();
            return Ok(new { ok = true, price = price, currency = "BS" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current price");
            return StatusCode(500, new { ok = false, error = "Error getting price" });
        }
    }

    /// <summary>
    /// Crea una nueva solicitud de compra de USDT
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreatePurchase([FromBody] CreatePurchaseRequest request)
    {
        try
        {
            if (request.AmountUsdt <= 0)
                return BadRequest(new { ok = false, error = "Amount must be greater than zero" });

            var userId = GetUserId();
            var purchase = await _purchaseService.CreatePurchaseAsync(userId, request.AmountUsdt);

            return Ok(new { ok = true, purchase });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase");
            return StatusCode(500, new { ok = false, error = "Error creating purchase" });
        }
    }

    /// <summary>
    /// Obtiene las compras del usuario actual
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyPurchases([FromQuery] string? status = null)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("GetMyPurchases called. Extracted userId from token: {UserId}", userId);
            
            var purchases = await _purchaseService.GetUserPurchasesAsync(userId, status);
            
            _logger.LogInformation("GetMyPurchases returning {Count} purchases for userId: {UserId}", purchases.Count, userId);
            
            return Ok(new { ok = true, purchases });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user purchases");
            return StatusCode(500, new { ok = false, error = "Error getting purchases" });
        }
    }

    /// <summary>
    /// Obtiene una compra específica del usuario
    /// </summary>
    [HttpGet("me/{id}")]
    public async Task<IActionResult> GetMyPurchase(string id)
    {
        try
        {
            var userId = GetUserId();
            var purchase = await _purchaseService.GetPurchaseByIdAsync(id, userId);

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
    /// Obtiene la URL del QR para pagos (siempre la misma)
    /// </summary>
    [HttpGet("qr")]
    public IActionResult GetQrCode()
    {
        // QR estático - siempre el mismo
        // En producción, esto debería venir de configuración o base de datos
        var qrUrl = "/images/payment-qr.png"; // Ruta relativa a la carpeta wwwroot
        
        return Ok(new 
        { 
            ok = true, 
            qrUrl = qrUrl,
            message = "Escanea este código QR para realizar el pago"
        });
    }

}

public class CreatePurchaseRequest
{
    public decimal AmountUsdt { get; set; }
}

