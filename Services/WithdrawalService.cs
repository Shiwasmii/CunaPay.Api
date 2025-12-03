using MongoDB.Driver;
using CunaPay.Api.Data;
using CunaPay.Api.Models;
using CunaPay.Api.Patterns.Behavioral;

namespace CunaPay.Api.Services;

public class WithdrawalService
{
    private readonly MongoDbContext _db;
    private readonly BinanceService _binanceService;
    private readonly WalletService _walletService;
    private readonly TronService _tronService;
    private readonly CryptoService _cryptoService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<WithdrawalService> _logger;
    private const decimal PRICE_DISCOUNT = 0.10m; // 0.10 BS de descuento al precio de venta de Binance

    public WithdrawalService(
        MongoDbContext db,
        BinanceService binanceService,
        WalletService walletService,
        TronService tronService,
        CryptoService cryptoService,
        IEventBus eventBus,
        ILogger<WithdrawalService> logger)
    {
        _db = db;
        _binanceService = binanceService;
        _walletService = walletService;
        _tronService = tronService;
        _cryptoService = cryptoService;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el precio actual de retiro de USDT en BS (precio de venta de Binance - 0.10 BS)
    /// </summary>
    public async Task<decimal> GetCurrentPriceAsync()
    {
        try
        {
            // Obtener precio de venta de Binance P2P (BOB = Bolivianos)
            var (success, averagePrice, error) = await _binanceService.GetAverageSellPriceAsync(
                asset: "USDT",
                fiat: "BOB",
                rows: 10
            );

            if (!success || !averagePrice.HasValue)
            {
                _logger.LogWarning("No se pudo obtener precio de venta de Binance, usando precio por defecto");
                // Precio por defecto si Binance falla
                return 36.50m - PRICE_DISCOUNT;
            }

            // Restar descuento de 0.10 BS al precio de venta de Binance
            var finalPrice = (decimal)averagePrice.Value - PRICE_DISCOUNT;
            
            _logger.LogDebug("Precio calculado: Binance Sell Price = {BinancePrice}, Discount = {Discount}, Final = {FinalPrice}",
                averagePrice.Value, PRICE_DISCOUNT, finalPrice);
            
            return finalPrice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo precio de venta de Binance");
            // Precio por defecto en caso de error
            return 36.50m - PRICE_DISCOUNT;
        }
    }

    /// <summary>
    /// Crea una nueva solicitud de retiro de USDT
    /// </summary>
    public async Task<WithdrawalDto> CreateWithdrawalAsync(string userId, decimal amountUsdt)
    {
        if (amountUsdt <= 0)
            throw new ArgumentException("Amount must be greater than zero");

        // Verificar que el usuario tenga información bancaria
        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
            throw new KeyNotFoundException("User not found");

        if (string.IsNullOrWhiteSpace(user.BankAccountNumber) || string.IsNullOrWhiteSpace(user.BankEntity))
        {
            throw new InvalidOperationException("User must have bank account information to create a withdrawal");
        }

        // Verificar saldo disponible del usuario
        var balances = await _walletService.GetBalancesAsync(userId);
        if (balances.Available < amountUsdt)
        {
            throw new InvalidOperationException("Insufficient available USDT");
        }

        // Obtener wallet del usuario
        var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        if (wallet == null)
            throw new KeyNotFoundException("Wallet not found");

        // Obtener precio actual
        var pricePerUsdt = await GetCurrentPriceAsync();
        var amountBs = amountUsdt * pricePerUsdt;

        // Crear withdrawal - el status inicial es "pending"
        var withdrawal = new Withdrawal
        {
            UserId = userId,
            WalletId = wallet.Id,
            AmountUsdt = amountUsdt,
            AmountBs = amountBs,
            PricePerUsdt = pricePerUsdt,
            Status = "pending", // Estado inicial: pendiente de revisión por admin
            CreatedAt = Helpers.DateTimeHelper.UtcNow,
            UpdatedAt = Helpers.DateTimeHelper.UtcNow
        };

        await _db.Withdrawals.InsertOneAsync(withdrawal);

        _logger.LogInformation("Withdrawal created: {WithdrawalId} for user {UserId}, {AmountUsdt} USDT at {Price} BS/USDT",
            withdrawal.Id, userId, amountUsdt, pricePerUsdt);

        return new WithdrawalDto
        {
            Id = withdrawal.Id,
            AmountUsdt = withdrawal.AmountUsdt,
            AmountBs = withdrawal.AmountBs,
            PricePerUsdt = withdrawal.PricePerUsdt,
            Status = withdrawal.Status,
            CreatedAt = withdrawal.CreatedAt
        };
    }

    /// <summary>
    /// Obtiene los retiros de un usuario
    /// </summary>
    public async Task<List<WithdrawalDto>> GetUserWithdrawalsAsync(string userId, string? status = null)
    {
        _logger.LogInformation("GetUserWithdrawalsAsync called for userId: {UserId}, status filter: {Status}", userId, status ?? "none");

        var filterBuilder = Builders<Withdrawal>.Filter;
        var filter = filterBuilder.Eq(w => w.UserId, userId);

        if (!string.IsNullOrEmpty(status))
            filter &= filterBuilder.Eq(w => w.Status, status);

        var withdrawals = await _db.Withdrawals
            .Find(filter)
            .SortByDescending(w => w.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("GetUserWithdrawalsAsync returning {Count} withdrawals for userId: {UserId}", withdrawals.Count, userId);

        return withdrawals.Select(w => MapToDto(w)).ToList();
    }

    /// <summary>
    /// Obtiene un retiro por ID (solo si pertenece al usuario)
    /// </summary>
    public async Task<WithdrawalDto?> GetWithdrawalByIdAsync(string withdrawalId, string? userId = null)
    {
        var filter = Builders<Withdrawal>.Filter.Eq(w => w.Id, withdrawalId);
        
        if (userId != null)
            filter &= Builders<Withdrawal>.Filter.Eq(w => w.UserId, userId);

        var withdrawal = await _db.Withdrawals.Find(filter).FirstOrDefaultAsync();
        return withdrawal != null ? MapToDto(withdrawal) : null;
    }

    /// <summary>
    /// Obtiene todos los retiros (para admin)
    /// </summary>
    public async Task<List<WithdrawalDto>> GetAllWithdrawalsAsync(string? status = null, int? limit = null)
    {
        try
        {
            var filterBuilder = Builders<Withdrawal>.Filter;
            FilterDefinition<Withdrawal> filter;

            if (!string.IsNullOrWhiteSpace(status))
            {
                filter = filterBuilder.Eq(w => w.Status, status);
            }
            else
            {
                filter = filterBuilder.Empty; // Sin filtro = todos los retiros
            }

            var query = _db.Withdrawals.Find(filter).SortByDescending(w => w.CreatedAt);

            List<Withdrawal> withdrawals;
            if (limit.HasValue && limit.Value > 0)
            {
                withdrawals = await query.Limit(limit.Value).ToListAsync();
            }
            else
            {
                withdrawals = await query.ToListAsync();
            }

            _logger.LogInformation("GetAllWithdrawalsAsync: Found {Count} withdrawals (status: {Status}, limit: {Limit})", 
                withdrawals.Count, status ?? "all", limit?.ToString() ?? "none");

            return withdrawals.Select(w => MapToDto(w)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAllWithdrawalsAsync");
            throw;
        }
    }

    /// <summary>
    /// Aprueba el retiro y transfiere USDT del usuario al admin (solo admin)
    /// </summary>
    public async Task<WithdrawalDto> ApproveWithdrawalAsync(string withdrawalId, string adminId, string? notes = null)
    {
        var withdrawal = await _db.Withdrawals.Find(w => w.Id == withdrawalId).FirstOrDefaultAsync();
        if (withdrawal == null)
            throw new KeyNotFoundException("Withdrawal not found");

        if (withdrawal.Status != "pending")
            throw new InvalidOperationException($"Cannot approve withdrawal. Current status: {withdrawal.Status}");

        // Obtener wallet del usuario
        var userWallet = await _db.Wallets.Find(w => w.Id == withdrawal.WalletId).FirstOrDefaultAsync();
        if (userWallet == null)
            throw new KeyNotFoundException("User wallet not found");

        // Obtener wallet del admin
        var adminUser = await _db.Users.Find(u => u.Email == "admin@gmail.com").FirstOrDefaultAsync();
        if (adminUser == null)
            throw new KeyNotFoundException("Admin user not found");

        var adminWallet = await _db.Wallets.Find(w => w.UserId == adminUser.Id).FirstOrDefaultAsync();
        if (adminWallet == null)
            throw new KeyNotFoundException("Admin wallet not found");

        try
        {
            // Verificar saldo del usuario
            var userUsdtBalance = await _tronService.GetUsdtBalanceAsync(userWallet.Address);
            if (userUsdtBalance < withdrawal.AmountUsdt)
            {
                throw new InvalidOperationException(
                    $"Insufficient user balance. Required: {withdrawal.AmountUsdt} USDT, Available: {userUsdtBalance} USDT");
            }

            // Transferir USDT desde la wallet del usuario a la wallet del admin
            var userPk = _cryptoService.Decrypt(userWallet.PkEncrypted);
            var (ok, txid, error) = await _tronService.SendUsdtAsync(
                userWallet.Address,
                userPk,
                adminWallet.Address,
                withdrawal.AmountUsdt
            );

            if (!ok || string.IsNullOrEmpty(txid))
            {
                throw new InvalidOperationException($"Failed to transfer USDT. Error: {error}");
            }

            // Actualizar estado del retiro a "completed"
            await _db.Withdrawals.UpdateOneAsync(
                Builders<Withdrawal>.Filter.Eq(w => w.Id, withdrawalId),
                Builders<Withdrawal>.Update
                    .Set(w => w.Status, "completed")
                    .Set(w => w.ProcessedBy, adminId)
                    .Set(w => w.ProcessedAt, Helpers.DateTimeHelper.UtcNow)
                    .Set(w => w.AdminNotes, notes)
                    .Set(w => w.TransactionId, txid)
                    .Set(w => w.UpdatedAt, Helpers.DateTimeHelper.UtcNow)
            );

            _logger.LogInformation(
                "Withdrawal {WithdrawalId} approved by admin {AdminId}. Transferred {AmountUsdt} USDT from user wallet {UserWallet} to admin wallet {AdminWallet}. TXID: {Txid}",
                withdrawalId, adminId, withdrawal.AmountUsdt, userWallet.Address, adminWallet.Address, txid);

            var updated = await _db.Withdrawals.Find(w => w.Id == withdrawalId).FirstOrDefaultAsync();
            return MapToDto(updated!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving withdrawal {WithdrawalId}", withdrawalId);
            throw;
        }
    }

    /// <summary>
    /// Rechaza el retiro (solo admin)
    /// </summary>
    public async Task<WithdrawalDto> RejectWithdrawalAsync(string withdrawalId, string adminId, string reason)
    {
        var withdrawal = await _db.Withdrawals.Find(w => w.Id == withdrawalId).FirstOrDefaultAsync();
        if (withdrawal == null)
            throw new KeyNotFoundException("Withdrawal not found");

        if (withdrawal.Status == "completed")
            throw new InvalidOperationException("Cannot reject a completed withdrawal");

        await _db.Withdrawals.UpdateOneAsync(
            Builders<Withdrawal>.Filter.Eq(w => w.Id, withdrawalId),
            Builders<Withdrawal>.Update
                .Set(w => w.Status, "rejected")
                .Set(w => w.RejectionReason, reason)
                .Set(w => w.ProcessedBy, adminId)
                .Set(w => w.ProcessedAt, Helpers.DateTimeHelper.UtcNow)
                .Set(w => w.UpdatedAt, Helpers.DateTimeHelper.UtcNow)
        );

        _logger.LogInformation("Withdrawal {WithdrawalId} rejected by admin {AdminId}. Reason: {Reason}",
            withdrawalId, adminId, reason);

        var updated = await _db.Withdrawals.Find(w => w.Id == withdrawalId).FirstOrDefaultAsync();
        return MapToDto(updated!);
    }

    private WithdrawalDto MapToDto(Withdrawal withdrawal)
    {
        return new WithdrawalDto
        {
            Id = withdrawal.Id,
            UserId = withdrawal.UserId,
            AmountUsdt = withdrawal.AmountUsdt,
            AmountBs = withdrawal.AmountBs,
            PricePerUsdt = withdrawal.PricePerUsdt,
            Status = withdrawal.Status,
            RejectionReason = withdrawal.RejectionReason,
            AdminNotes = withdrawal.AdminNotes,
            ProcessedBy = withdrawal.ProcessedBy,
            ProcessedAt = withdrawal.ProcessedAt,
            TransactionId = withdrawal.TransactionId,
            CreatedAt = withdrawal.CreatedAt,
            UpdatedAt = withdrawal.UpdatedAt
        };
    }
}

public class WithdrawalDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal AmountUsdt { get; set; }
    public decimal AmountBs { get; set; }
    public decimal PricePerUsdt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string? AdminNotes { get; set; }
    public string? ProcessedBy { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

