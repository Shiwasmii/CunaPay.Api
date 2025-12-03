using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CunaPay.Api.Services;
using CunaPay.Api.Attributes;

namespace CunaPay.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize]
[AuthorizeRole("Admin")]
public class AdminUserController : ControllerBase
{
    private readonly AdminUserService _adminUserService;
    private readonly AdminWalletService _adminWalletService;
    private readonly WalletService _walletService;
    private readonly TronService _tronService;
    private readonly ILogger<AdminUserController> _logger;

    public AdminUserController(
        AdminUserService adminUserService,
        AdminWalletService adminWalletService,
        WalletService walletService,
        TronService tronService,
        ILogger<AdminUserController> logger)
    {
        _adminUserService = adminUserService;
        _adminWalletService = adminWalletService;
        _walletService = walletService;
        _tronService = tronService;
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
    /// Obtiene todos los usuarios con paginación y filtros
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var (users, total) = await _adminUserService.GetAllUsersAsync(page, pageSize, search, role);

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            return Ok(new
            {
                ok = true,
                users,
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    totalPages
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all users");
            return StatusCode(500, new { ok = false, error = "Error getting users" });
        }
    }

    /// <summary>
    /// Busca usuarios por email (búsqueda rápida)
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] string q,
        [FromQuery] int limit = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { ok = false, error = "Search term is required" });

            if (limit < 1 || limit > 100) limit = 50;

            var users = await _adminUserService.SearchUsersAsync(q, limit);

            return Ok(new
            {
                ok = true,
                users,
                count = users.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users");
            return StatusCode(500, new { ok = false, error = "Error searching users" });
        }
    }

    /// <summary>
    /// Obtiene un usuario específico con todos sus datos, incluyendo saldo
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(string id)
    {
        try
        {
            var user = await _adminUserService.GetUserByIdAsync(id, _adminWalletService);

            if (user == null)
                return NotFound(new { ok = false, error = "User not found" });

            // Obtener balance del usuario
            BalanceDto? balance = null;
            try
            {
                balance = await _walletService.GetBalancesAsync(id);
            }
            catch (KeyNotFoundException)
            {
                // Si no tiene wallet, el balance será null
                _logger.LogWarning("User {UserId} does not have a wallet", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting balance for user {UserId}", id);
                // Continuar sin balance si hay error
            }

            return Ok(new 
            { 
                ok = true, 
                user,
                balance = balance != null ? new
                {
                    trx = balance.Trx,
                    usdt = balance.Usdt,
                    lockedInStaking = balance.LockedInStaking,
                    available = balance.Available
                } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by id");
            return StatusCode(500, new { ok = false, error = "Error getting user" });
        }
    }

    /// <summary>
    /// Obtiene la wallet del admin actual
    /// </summary>
    [HttpGet("me/wallet")]
    public async Task<IActionResult> GetMyWallet()
    {
        try
        {
            var adminId = GetAdminId();
            
            // Asegurar que el admin tenga wallet
            await _adminWalletService.EnsureAdminWalletAsync(adminId);
            
            var wallet = await _walletService.GetMyWalletAsync(adminId);
            return Ok(new { ok = true, wallet });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin wallet");
            return StatusCode(500, new { ok = false, error = "Error getting wallet" });
        }
    }

    /// <summary>
    /// Obtiene el balance del admin actual (solo TRX y USDT, sin staking)
    /// </summary>
    [HttpGet("me/balance")]
    public async Task<IActionResult> GetMyBalance()
    {
        try
        {
            var adminId = GetAdminId();
            
            // Asegurar que el admin tenga wallet
            var wallet = await _adminWalletService.EnsureAdminWalletAsync(adminId);
            
            // Obtener balances directamente (sin considerar staking)
            var (trx, usdt) = await _adminWalletService.GetAdminBalanceFullAsync(adminId);
            
            var balance = new
            {
                walletId = wallet.Id,
                address = wallet.Address,
                trx = trx,
                usdt = usdt
            };
            
            return Ok(new { ok = true, balance });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin balance");
            return StatusCode(500, new { ok = false, error = "Error getting balance" });
        }
    }

    /// <summary>
    /// Obtiene todas las transacciones del admin actual
    /// </summary>
    [HttpGet("me/transactions")]
    public async Task<IActionResult> GetMyTransactions(
        [FromQuery] string? source = "db",
        [FromQuery] int? limit = null,
        [FromQuery] string? status = null,
        [FromQuery] string? direction = null,
        [FromQuery] string? fingerprint = null)
    {
        try
        {
            var adminId = GetAdminId();
            
            // Asegurar que el admin tenga wallet
            await _adminWalletService.EnsureAdminWalletAsync(adminId);

            // Obtener transacciones según la fuente
            if (source?.ToLower() == "onchain")
            {
                var onChainTransactions = await _walletService.ListOnChainTransactionsAsync(
                    adminId, limit, direction, fingerprint);

                return Ok(new
                {
                    ok = true,
                    source = "onchain",
                    transactions = onChainTransactions
                });
            }

            // Transacciones de la base de datos (off-chain)
            var transactions = await _walletService.ListTransactionsAsync(adminId, limit, status);

            return Ok(new
            {
                ok = true,
                source = "db",
                transactions = new
                {
                    items = transactions,
                    count = transactions.Count
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin transactions");
            return StatusCode(500, new { ok = false, error = "Error getting transactions" });
        }
    }

    /// <summary>
    /// Obtiene estadísticas generales de usuarios
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetUserStats()
    {
        try
        {
            var totalUsers = await _adminUserService.GetAllUsersAsync(1, 1);
            var totalAdmins = await _adminUserService.GetAllUsersAsync(1, 1, null, "Admin");
            var totalNormalUsers = await _adminUserService.GetAllUsersAsync(1, 1, null, "User");

            var stats = new
            {
                total = totalUsers.Total,
                admins = totalAdmins.Total,
                users = totalNormalUsers.Total
            };

            return Ok(new { ok = true, stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user stats");
            return StatusCode(500, new { ok = false, error = "Error getting stats" });
        }
    }

    /// <summary>
    /// Obtiene todas las transacciones de un usuario específico
    /// </summary>
    [HttpGet("{id}/transactions")]
    public async Task<IActionResult> GetUserTransactions(
        string id,
        [FromQuery] string? source = "db",
        [FromQuery] int? limit = null,
        [FromQuery] string? status = null,
        [FromQuery] string? direction = null,
        [FromQuery] string? fingerprint = null)
    {
        try
        {
            // Verificar que el usuario existe
            var user = await _adminUserService.GetUserByIdAsync(id, _adminWalletService);
            if (user == null)
            {
                return NotFound(new { ok = false, error = "User not found" });
            }

            // Obtener transacciones según la fuente
            if (source?.ToLower() == "onchain")
            {
                var onChainTransactions = await _walletService.ListOnChainTransactionsAsync(
                    id, limit, direction, fingerprint);

                return Ok(new
                {
                    ok = true,
                    source = "onchain",
                    userId = id,
                    transactions = onChainTransactions
                });
            }

            // Transacciones de la base de datos (off-chain)
            var transactions = await _walletService.ListTransactionsAsync(id, limit, status);

            return Ok(new
            {
                ok = true,
                source = "db",
                userId = id,
                transactions = new
                {
                    items = transactions,
                    count = transactions.Count
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions for user {UserId}", id);
            return StatusCode(500, new { ok = false, error = "Error getting transactions" });
        }
    }

    /// <summary>
    /// Envía USDT desde la wallet del admin a otra dirección
    /// </summary>
    [HttpPost("me/send")]
    public async Task<IActionResult> SendTransaction([FromBody] SendTransactionRequest request)
    {
        try
        {
            var adminId = GetAdminId();

            // Validaciones
            if (string.IsNullOrWhiteSpace(request.To))
                return BadRequest(new { ok = false, error = "La dirección de destino es requerida" });

            if (string.IsNullOrWhiteSpace(request.Amount))
                return BadRequest(new { ok = false, error = "El monto es requerido" });

            // Validar formato del monto
            var amountPattern = @"^(\d+)(\.\d{1,6})?$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Amount, amountPattern))
                return BadRequest(new { ok = false, error = "Formato de monto inválido. Use hasta 6 decimales." });

            // Validar dirección TRON
            if (!await _tronService.IsValidAddressAsync(request.To))
                return BadRequest(new { ok = false, error = "Dirección TRON inválida" });

            // Enviar transacción
            var (ok, txid, status) = await _walletService.SendFromCustodyAsync(
                adminId, request.To, request.Amount);

            if (ok && !string.IsNullOrEmpty(txid))
            {
                return Ok(new
                {
                    ok = true,
                    txid,
                    status,
                    message = $"Transacción enviada exitosamente. TXID: {txid}"
                });
            }
            else
            {
                return BadRequest(new
                {
                    ok = false,
                    error = $"Error al enviar transacción: {status}",
                    status
                });
            }
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
            _logger.LogError(ex, "Error sending transaction from admin wallet");
            return StatusCode(500, new { ok = false, error = "Error al enviar transacción" });
        }
    }
}

public class SendTransactionRequest
{
    public string To { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
}

