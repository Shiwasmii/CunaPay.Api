using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CunaPay.Api.Services;
using CunaPay.Api.Data;
using CunaPay.Api.Models;
using MongoDB.Driver;

namespace CunaPay.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ApiController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly StakingService _stakingService;
    private readonly TronService _tronService;
    private readonly IRateLimitService _rateLimitService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly MongoDbContext _db;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        WalletService walletService,
        StakingService stakingService,
        TronService tronService,
        IRateLimitService rateLimitService,
        IIdempotencyService idempotencyService,
        MongoDbContext db,
        ILogger<ApiController> logger)
    {
        _walletService = walletService;
        _stakingService = stakingService;
        _tronService = tronService;
        _rateLimitService = rateLimitService;
        _idempotencyService = idempotencyService;
        _db = db;
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

    // ---------------------------------------------------------------------
    // GET ME
    // ---------------------------------------------------------------------
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        try
        {
            var userId = GetUserId();
            
            // Obtener usuario completo de la base de datos
            var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            
            if (user == null)
                return NotFound(new { error = "User not found" });

            return Ok(new 
            { 
                id = user.Id, 
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                bankAccountNumber = user.BankAccountNumber,
                bankEntity = user.BankEntity
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info");
            return StatusCode(500, new { error = "Error getting user info" });
        }
    }

    // ---------------------------------------------------------------------
    // PUT ME (Update Profile)
    // ---------------------------------------------------------------------
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = GetUserId();
            
            // Obtener usuario actual
            var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            
            if (user == null)
                return NotFound(new { ok = false, error = "User not found" });

            // Actualizar solo los campos proporcionados (opcionales)
            var update = Builders<User>.Update
                .Set(u => u.UpdatedAt, Helpers.DateTimeHelper.UtcNow);

            if (!string.IsNullOrWhiteSpace(request.BankAccountNumber))
            {
                update = update.Set(u => u.BankAccountNumber, request.BankAccountNumber.Trim());
            }
            else if (request.BankAccountNumber == string.Empty)
            {
                // Permitir limpiar el campo enviando string vacío
                update = update.Set(u => u.BankAccountNumber, null);
            }

            if (!string.IsNullOrWhiteSpace(request.BankEntity))
            {
                update = update.Set(u => u.BankEntity, request.BankEntity.Trim());
            }
            else if (request.BankEntity == string.Empty)
            {
                // Permitir limpiar el campo enviando string vacío
                update = update.Set(u => u.BankEntity, null);
            }

            await _db.Users.UpdateOneAsync(
                Builders<User>.Filter.Eq(u => u.Id, userId),
                update
            );

            // Obtener usuario actualizado
            user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();

            return Ok(new 
            { 
                ok = true,
                id = user!.Id, 
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                bankAccountNumber = user.BankAccountNumber,
                bankEntity = user.BankEntity
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return StatusCode(500, new { ok = false, error = "Error updating user profile" });
        }
    }

    // ---------------------------------------------------------------------
    // GET WALLET
    // ---------------------------------------------------------------------
    [HttpGet("me/wallet")]
    public async Task<IActionResult> GetWallet()
    {
        try
        {
            var wallet = await _walletService.GetMyWalletAsync(GetUserId());
            return Ok(wallet);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------------------------------------------------------------------
    // GET BALANCE
    // ---------------------------------------------------------------------
    [HttpGet("me/balance")]
    public async Task<IActionResult> GetBalance()
    {
        try
        {
            var balance = await _walletService.GetBalancesAsync(GetUserId());
            return Ok(balance);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------------------------------------------------------------------
    // LIST TRANSACTIONS (db + onchain)
    // ---------------------------------------------------------------------
    [HttpGet("me/transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] string? source = "db",
        [FromQuery] int? limit = null,
        [FromQuery] string? status = null,
        [FromQuery] string? direction = null,
        [FromQuery] string? fingerprint = null)
    {
        try
        {
            if (source?.ToLower() == "onchain")
            {
                var result = await _walletService.ListOnChainTransactionsAsync(
                    GetUserId(), limit, direction, fingerprint);

                return Ok(result);
            }

            var transactions = await _walletService.ListTransactionsAsync(
                GetUserId(), limit, status);

            return Ok(new { items = transactions, source = "db" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------------------------------------------------------------------
    // SEND USDT
    // ---------------------------------------------------------------------
    [HttpPost("me/send")]
    public async Task<IActionResult> Send([FromBody] SendRequest request)
    {
        var userId = GetUserId();

        // Rate limiting
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateLimitKey = $"send:{userId}:{remoteIp}";

        var allowed = _rateLimitService.TryConsume(rateLimitKey, 3, TimeSpan.FromSeconds(30));
        if (!allowed)
        {
            return StatusCode(429, new { error = "Too many requests. Try again later." });
        }

        // Idempotency
        string? idempotencyKey = null;
        if (Request.Headers.TryGetValue("Idempotency-Key", out var headerValues))
        {
            idempotencyKey = headerValues.ToString();

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                if (_idempotencyService.TryGet(idempotencyKey, out var cached) && cached != null)
                    return StatusCode(cached.StatusCode, cached.Body);
            }
        }

        try
        {
            // Required fields
            if (string.IsNullOrWhiteSpace(request.To) || string.IsNullOrWhiteSpace(request.Amount))
                return BadRequest(new { error = "To and Amount are required." });

            // Amount validation
            var amountPattern = @"^(\d+)(\.\d{1,6})?$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Amount, amountPattern))
                return BadRequest(new { error = "Invalid amount format. Use up to 6 decimals." });

            // TRON Address Validation (FIXED)
            if (!await _tronService.IsValidAddressAsync(request.To))
                return BadRequest(new { error = "Invalid TRON address." });

            // Send transaction
            var (ok, txid, status) = await _walletService.SendFromCustodyAsync(
                userId, request.To, request.Amount);

            var responseBody = new { ok, txid, status };

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var entry = new IdempotencyEntry(200, responseBody, Helpers.DateTimeHelper.UtcNow);
                _idempotencyService.Store(idempotencyKey, entry);
            }

            return Ok(responseBody);
        }
        catch (ArgumentException ex)
        {
            var error = new { error = ex.Message };
            CacheError(idempotencyKey, 400, error);
            return BadRequest(error);
        }
        catch (InvalidOperationException ex)
        {
            var error = new { error = ex.Message };
            CacheError(idempotencyKey, 400, error);
            return BadRequest(error);
        }
        catch (KeyNotFoundException ex)
        {
            var error = new { error = ex.Message };
            CacheError(idempotencyKey, 404, error);
            return NotFound(error);
        }
    }

    private void CacheError(string? key, int status, object body)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var entry = new IdempotencyEntry(status, body, Helpers.DateTimeHelper.UtcNow);
            _idempotencyService.Store(key, entry);
        }
    }

    // ---------------------------------------------------------------------
    // STAKING ENDPOINTS
    // ---------------------------------------------------------------------
    /// <summary>
    /// Crea un nuevo stake: transfiere USDT a la wallet del admin
    /// </summary>
    [HttpPost("me/stakes")]
    public async Task<IActionResult> CreateStake([FromBody] CreateStakeRequest request)
    {
        try
        {
            if (request.AmountUsdt <= 0)
                return BadRequest(new { ok = false, error = "amount_usdt must be > 0" });

            var balances = await _walletService.GetBalancesAsync(GetUserId());
            if (balances.Available < request.AmountUsdt)
                return BadRequest(new { ok = false, error = "Insufficient available USDT" });

            // El DailyRateBp viene del backend, no del request
            var stake = await _stakingService.CreateStakeAsync(GetUserId(), request.AmountUsdt);

            return Ok(new { ok = true, stake });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ok = false, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    [HttpGet("me/stakes")]
    public async Task<IActionResult> GetStakes()
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("GetStakes called for userId: {UserId}", userId);
            
            var stakes = await _stakingService.ListStakesAsync(userId);
            _logger.LogInformation("GetStakes returning {Count} stakes for userId: {UserId}", stakes.Count, userId);
            
            return Ok(stakes);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "GetStakes: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetStakes");
            return StatusCode(500, new { error = "Error getting stakes" });
        }
    }

    /// <summary>
    /// Cierra un stake y recibe principal + recompensas desde la wallet del admin
    /// </summary>
    [HttpPost("me/stakes/{id}/close")]
    public async Task<IActionResult> CloseStake(string id)
    {
        try
        {
            var (principal, rewards) = await _stakingService.CloseStakeAsync(GetUserId(), id);
            return Ok(new
            {
                ok = true,
                closed = true,
                principal_usdt = principal,
                rewards_usdt = rewards,
                total_usdt = principal + rewards
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
    }

}


// DTOs
public class SendRequest
{
    public string To { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
}

public class CreateStakeRequest
{
    public decimal AmountUsdt { get; set; }
}

public class UpdateProfileRequest
{
    public string? BankAccountNumber { get; set; }
    public string? BankEntity { get; set; }
}
