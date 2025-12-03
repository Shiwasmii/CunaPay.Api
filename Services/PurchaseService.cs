using MongoDB.Driver;
using CunaPay.Api.Data;
using CunaPay.Api.Models;
using CunaPay.Api.Patterns.Behavioral;
using CunaPay.Api.Patterns.Structural;

namespace CunaPay.Api.Services;

public class PurchaseService
{
    private readonly MongoDbContext _db;
    private readonly BinanceService _binanceService;
    private readonly WalletService _walletService;
    private readonly TronService _tronService;
    private readonly CryptoService _cryptoService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PurchaseService> _logger;
    private const decimal PRICE_MARKUP = 0.13m; // 0.13 BS adicionales al precio de compra de Binance
    private const decimal DEFAULT_TRX_DEPOSIT = 150m; // TRX predeterminado a depositar al aprobar compra

    public PurchaseService(
        MongoDbContext db,
        BinanceService binanceService,
        WalletService walletService,
        TronService tronService,
        CryptoService cryptoService,
        IEventBus eventBus,
        ILogger<PurchaseService> logger)
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
    /// Obtiene el precio actual de USDT en BS (precio de compra de Binance + 0.13 BS)
    /// </summary>
    public async Task<decimal> GetCurrentPriceAsync()
    {
        try
        {
            // Obtener precio de compra de Binance P2P (BOB = Bolivianos)
            var (success, averagePrice, error) = await _binanceService.GetAverageBuyPriceAsync(
                asset: "USDT",
                fiat: "BOB", // Bolivianos (no VES)
                rows: 10
            );

            if (!success || !averagePrice.HasValue)
            {
                _logger.LogWarning("No se pudo obtener precio de Binance, usando precio por defecto");
                // Precio por defecto si Binance falla
                return 36.50m + PRICE_MARKUP;
            }

            // Agregar markup de 0.13 BS al precio de compra de Binance
            var finalPrice = (decimal)averagePrice.Value + PRICE_MARKUP;
            
            _logger.LogDebug("Precio calculado: Binance Buy Price = {BinancePrice}, Markup = {Markup}, Final = {FinalPrice}",
                averagePrice.Value, PRICE_MARKUP, finalPrice);
            
            return finalPrice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo precio de Binance");
            // Precio por defecto en caso de error
            return 36.50m + PRICE_MARKUP;
        }
    }

    /// <summary>
    /// Crea una nueva solicitud de compra de USDT
    /// </summary>
    public async Task<PurchaseDto> CreatePurchaseAsync(string userId, decimal amountUsdt)
    {
        if (amountUsdt <= 0)
            throw new ArgumentException("Amount must be greater than zero");

        // Obtener wallet del usuario
        var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        if (wallet == null)
            throw new KeyNotFoundException("Wallet not found");

        // Obtener precio actual
        var pricePerUsdt = await GetCurrentPriceAsync();
        var amountBs = amountUsdt * pricePerUsdt;

        // Crear purchase - el status inicial es "pending"
        var purchase = new Purchase
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

        await _db.Purchases.InsertOneAsync(purchase);

        _logger.LogInformation("Purchase created: {PurchaseId} for user {UserId}, {AmountUsdt} USDT at {Price} BS/USDT",
            purchase.Id, userId, amountUsdt, pricePerUsdt);

        // Publicar evento
        await _eventBus.PublishAsync(new PurchaseCreatedEvent
        {
            PurchaseId = purchase.Id,
            UserId = userId,
            AmountUsdt = amountUsdt,
            AmountBs = amountBs
        });

        return new PurchaseDto
        {
            Id = purchase.Id,
            AmountUsdt = purchase.AmountUsdt,
            AmountBs = purchase.AmountBs,
            PricePerUsdt = purchase.PricePerUsdt,
            Status = purchase.Status,
            CreatedAt = purchase.CreatedAt
        };
    }

    /// <summary>
    /// Obtiene las compras de un usuario
    /// </summary>
    public async Task<List<PurchaseDto>> GetUserPurchasesAsync(string userId, string? status = null)
    {
        _logger.LogInformation("GetUserPurchasesAsync called for userId: {UserId}, status filter: {Status}", userId, status ?? "none");

        var filterBuilder = Builders<Purchase>.Filter;
        var filter = filterBuilder.Eq(p => p.UserId, userId);

        if (!string.IsNullOrEmpty(status))
            filter &= filterBuilder.Eq(p => p.Status, status);

        // Log para debug: contar todas las compras en la base de datos
        var totalPurchases = await _db.Purchases.CountDocumentsAsync(FilterDefinition<Purchase>.Empty);
        _logger.LogDebug("Total purchases in database: {TotalPurchases}", totalPurchases);

        // Log para debug: verificar si hay compras con este userId
        var purchasesWithUserId = await _db.Purchases
            .Find(filterBuilder.Eq(p => p.UserId, userId))
            .ToListAsync();
        _logger.LogDebug("Purchases found with userId {UserId}: {Count}", userId, purchasesWithUserId.Count);

        if (purchasesWithUserId.Count > 0)
        {
            _logger.LogDebug("Sample purchase userId from DB: {SampleUserId}", purchasesWithUserId.First().UserId);
        }

        var purchases = await _db.Purchases
            .Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("GetUserPurchasesAsync returning {Count} purchases for userId: {UserId}", purchases.Count, userId);

        return purchases.Select(p => MapToDto(p)).ToList();
    }

    /// <summary>
    /// Obtiene una compra por ID (solo si pertenece al usuario o es admin)
    /// </summary>
    public async Task<PurchaseDto?> GetPurchaseByIdAsync(string purchaseId, string? userId = null)
    {
        var filter = Builders<Purchase>.Filter.Eq(p => p.Id, purchaseId);
        
        if (userId != null)
            filter &= Builders<Purchase>.Filter.Eq(p => p.UserId, userId);

        var purchase = await _db.Purchases.Find(filter).FirstOrDefaultAsync();
        return purchase != null ? MapToDto(purchase) : null;
    }

    /// <summary>
    /// Obtiene todas las compras (para admin)
    /// </summary>
    public async Task<List<PurchaseDto>> GetAllPurchasesAsync(string? status = null, int? limit = null)
    {
        try
        {
            var filterBuilder = Builders<Purchase>.Filter;
            FilterDefinition<Purchase> filter;

            if (!string.IsNullOrWhiteSpace(status))
            {
                filter = filterBuilder.Eq(p => p.Status, status);
            }
            else
            {
                filter = filterBuilder.Empty; // Sin filtro = todas las compras
            }

            var query = _db.Purchases.Find(filter).SortByDescending(p => p.CreatedAt);

            List<Purchase> purchases;
            if (limit.HasValue && limit.Value > 0)
            {
                purchases = await query.Limit(limit.Value).ToListAsync();
            }
            else
            {
                purchases = await query.ToListAsync();
            }

            _logger.LogInformation("GetAllPurchasesAsync: Found {Count} purchases (status: {Status}, limit: {Limit})", 
                purchases.Count, status ?? "all", limit?.ToString() ?? "none");

            return purchases.Select(p => MapToDto(p)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAllPurchasesAsync");
            throw;
        }
    }

    /// <summary>
    /// Aprueba la compra y deposita USDT al usuario desde la wallet del admin (solo admin)
    /// </summary>
    public async Task<PurchaseDto> ApprovePurchaseAsync(string purchaseId, string adminId, string? notes = null)
    {
        var purchase = await _db.Purchases.Find(p => p.Id == purchaseId).FirstOrDefaultAsync();
        if (purchase == null)
            throw new KeyNotFoundException("Purchase not found");

        if (purchase.Status != "pending")
            throw new InvalidOperationException($"Cannot approve purchase. Current status: {purchase.Status}");

        // Obtener wallet del usuario
        var userWallet = await _db.Wallets.Find(w => w.Id == purchase.WalletId).FirstOrDefaultAsync();
        if (userWallet == null)
            throw new KeyNotFoundException("User wallet not found");

        // Obtener wallet del admin
        var adminWallet = await _db.Wallets.Find(w => w.UserId == adminId).FirstOrDefaultAsync();
        if (adminWallet == null)
            throw new KeyNotFoundException("Admin wallet not found. Admin must have a wallet to approve purchases.");

        try
        {
            // Verificar saldo del admin (solo USDT, sin considerar staking)
            var adminUsdtBalance = await _tronService.GetUsdtBalanceAsync(adminWallet.Address);
        if (adminUsdtBalance < purchase.AmountUsdt)
        {
            throw new InvalidOperationException(
                $"Insufficient admin balance. Required: {purchase.AmountUsdt} USDT, Available: {adminUsdtBalance} USDT");
        }

            // Transferir USDT desde la wallet del admin a la wallet del usuario
            var (ok, txid, status) = await _walletService.SendFromCustodyAsync(
                adminId,
                userWallet.Address,
                purchase.AmountUsdt.ToString("F6")
            );

            if (!ok || string.IsNullOrEmpty(txid))
            {
                throw new InvalidOperationException($"Failed to transfer USDT. Status: {status}");
            }

            // Transferir TRX desde la wallet del admin a la wallet del usuario (150 TRX predeterminado)
            string? trxTxid = null;
            try
            {
                // Verificar saldo de TRX del admin
                var adminTrxBalance = await _tronService.GetTrxBalanceAsync(adminWallet.Address);
                _logger.LogInformation(
                    "Checking admin TRX balance. Required: {Required} TRX, Available: {Available} TRX",
                    DEFAULT_TRX_DEPOSIT, adminTrxBalance);

                if (adminTrxBalance < DEFAULT_TRX_DEPOSIT)
                {
                    _logger.LogWarning(
                        "Admin {AdminId} has insufficient TRX balance. Required: {Required} TRX, Available: {Available} TRX. TRX deposit skipped.",
                        adminId, DEFAULT_TRX_DEPOSIT, adminTrxBalance);
                }
                else
                {
                    _logger.LogInformation(
                        "Initiating TRX transfer: {Amount} TRX from {From} to {To}",
                        DEFAULT_TRX_DEPOSIT, adminWallet.Address, userWallet.Address);

                    var adminPrivateKey = _cryptoService.Decrypt(adminWallet.PkEncrypted);
                    
                    if (string.IsNullOrEmpty(adminPrivateKey))
                    {
                        _logger.LogError("Admin private key is null or empty. Cannot transfer TRX.");
                    }
                    else
                    {
                        var (trxOk, trxTxidResult, trxError) = await _tronService.SendTrxAsync(
                            adminWallet.Address,
                            adminPrivateKey,
                            userWallet.Address,
                            DEFAULT_TRX_DEPOSIT
                        );

                        _logger.LogInformation(
                            "TRX transfer result: Ok={Ok}, Txid={Txid}, Error={Error}",
                            trxOk, trxTxidResult ?? "null", trxError ?? "null");

                        if (trxOk && !string.IsNullOrEmpty(trxTxidResult))
                        {
                            trxTxid = trxTxidResult;
                            _logger.LogInformation(
                                "Successfully transferred {AmountTrx} TRX from admin wallet {AdminWallet} to user wallet {UserWallet}. TXID: {TrxTxid}",
                                DEFAULT_TRX_DEPOSIT, adminWallet.Address, userWallet.Address, trxTxid);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Failed to transfer TRX to user wallet. Ok={Ok}, Txid={Txid}, Error: {Error}. Purchase approval continues with USDT only.",
                                trxOk, trxTxidResult ?? "null", trxError ?? "Unknown error");
                        }
                    }
                }
            }
            catch (Exception trxEx)
            {
                // No fallar la aprobación si falla el envío de TRX, solo loguear el error
                _logger.LogError(trxEx,
                    "Exception transferring TRX to user wallet {UserWallet} during purchase approval. Purchase approval continues with USDT only.",
                    userWallet.Address);
            }

            // Actualizar estado de la compra a "accepted"
            await _db.Purchases.UpdateOneAsync(
                Builders<Purchase>.Filter.Eq(p => p.Id, purchaseId),
                Builders<Purchase>.Update
                    .Set(p => p.Status, "accepted")
                    .Set(p => p.ProcessedBy, adminId)
                    .Set(p => p.ProcessedAt, Helpers.DateTimeHelper.UtcNow)
                    .Set(p => p.AdminNotes, notes)
                    .Set(p => p.UpdatedAt, Helpers.DateTimeHelper.UtcNow)
            );

            _logger.LogInformation(
                "Purchase {PurchaseId} approved by admin {AdminId}. Transferred {AmountUsdt} USDT from admin wallet {AdminWallet} to user wallet {UserWallet}. USDT TXID: {Txid}. TRX TXID: {TrxTxid}",
                purchaseId, adminId, purchase.AmountUsdt, adminWallet.Address, userWallet.Address, txid, trxTxid ?? "N/A");

            // Publicar evento
            await _eventBus.PublishAsync(new PurchaseCompletedEvent
            {
                PurchaseId = purchaseId,
                UserId = purchase.UserId,
                AmountUsdt = purchase.AmountUsdt
            });

            var updated = await _db.Purchases.Find(p => p.Id == purchaseId).FirstOrDefaultAsync();
            return MapToDto(updated!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving purchase {PurchaseId}", purchaseId);
            throw;
        }
    }

    /// <summary>
    /// Rechaza la compra (solo admin)
    /// </summary>
    public async Task<PurchaseDto> RejectPurchaseAsync(string purchaseId, string adminId, string reason)
    {
        var purchase = await _db.Purchases.Find(p => p.Id == purchaseId).FirstOrDefaultAsync();
        if (purchase == null)
            throw new KeyNotFoundException("Purchase not found");

        if (purchase.Status == "accepted")
            throw new InvalidOperationException("Cannot reject an accepted purchase");

        await _db.Purchases.UpdateOneAsync(
            Builders<Purchase>.Filter.Eq(p => p.Id, purchaseId),
            Builders<Purchase>.Update
                .Set(p => p.Status, "rejected")
                .Set(p => p.RejectionReason, reason)
                .Set(p => p.ProcessedBy, adminId)
                .Set(p => p.ProcessedAt, Helpers.DateTimeHelper.UtcNow)
                .Set(p => p.UpdatedAt, Helpers.DateTimeHelper.UtcNow)
        );

        _logger.LogInformation("Purchase {PurchaseId} rejected by admin {AdminId}. Reason: {Reason}",
            purchaseId, adminId, reason);

        // Publicar evento
        await _eventBus.PublishAsync(new PurchaseRejectedEvent
        {
            PurchaseId = purchaseId,
            UserId = purchase.UserId,
            Reason = reason
        });

        var updated = await _db.Purchases.Find(p => p.Id == purchaseId).FirstOrDefaultAsync();
        return MapToDto(updated!);
    }

    private PurchaseDto MapToDto(Purchase purchase)
    {
        return new PurchaseDto
        {
            Id = purchase.Id,
            UserId = purchase.UserId,
            AmountUsdt = purchase.AmountUsdt,
            AmountBs = purchase.AmountBs,
            PricePerUsdt = purchase.PricePerUsdt,
            Status = purchase.Status,
            ReceiptImageUrl = purchase.ReceiptImageUrl,
            ReceiptFileName = purchase.ReceiptFileName,
            RejectionReason = purchase.RejectionReason,
            AdminNotes = purchase.AdminNotes,
            ProcessedBy = purchase.ProcessedBy,
            ProcessedAt = purchase.ProcessedAt,
            CreatedAt = purchase.CreatedAt,
            UpdatedAt = purchase.UpdatedAt
        };
    }
}

public class PurchaseDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal AmountUsdt { get; set; }
    public decimal AmountBs { get; set; }
    public decimal PricePerUsdt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReceiptImageUrl { get; set; }
    public string? ReceiptFileName { get; set; }
    public string? RejectionReason { get; set; }
    public string? AdminNotes { get; set; }
    public string? ProcessedBy { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

