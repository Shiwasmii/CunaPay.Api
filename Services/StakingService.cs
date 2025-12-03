using MongoDB.Driver;
using CunaPay.Api.Data;
using CunaPay.Api.Models;
using CunaPay.Api.Configuration;

namespace CunaPay.Api.Services;

/// <summary>
/// Servicio de staking centralizado: el dinero va a la wallet del admin
/// </summary>
public class StakingService
{
    private readonly MongoDbContext _db;
    private readonly WalletService _walletService;
    private readonly AdminWalletService _adminWalletService;
    private readonly TronService _tronService;
    private readonly CryptoService _cryptoService;
    private readonly IConfiguration _config;
    private readonly ILogger<StakingService> _logger;
    private readonly StakingSettings _stakingSettings;

    public StakingService(
        MongoDbContext db,
        WalletService walletService,
        AdminWalletService adminWalletService,
        TronService tronService,
        CryptoService cryptoService,
        IConfiguration config,
        ILogger<StakingService> logger)
    {
        _db = db;
        _walletService = walletService;
        _adminWalletService = adminWalletService;
        _tronService = tronService;
        _cryptoService = cryptoService;
        _config = config;
        _logger = logger;
        _stakingSettings = config.GetSection("Staking").Get<StakingSettings>() ?? new StakingSettings();
    }

    /// <summary>
    /// Lista todos los stakes activos de un usuario
    /// </summary>
    public async Task<List<StakeDto>> ListStakesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("ListStakesAsync called with empty userId");
            return new List<StakeDto>();
        }

        _logger.LogInformation("ListStakesAsync called for userId: {UserId}", userId);

        // Obtener la wallet del usuario primero para usar WalletId como alternativa
        var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        if (wallet == null)
        {
            _logger.LogWarning("Wallet not found for userId: {UserId}", userId);
            return new List<StakeDto>();
        }

        // Intentar buscar por UserId primero, y si no encuentra nada, buscar por WalletId
        // Esto maneja casos donde el userId podría no coincidir exactamente
        var filterByUserId = Builders<Stake>.Filter.Eq(s => s.UserId, userId);
        var stakesByUserId = await _db.Stakes
            .Find(filterByUserId)
            .ToListAsync();

        _logger.LogInformation("Found {Count} stakes by UserId for userId: {UserId}", stakesByUserId.Count, userId);

        // Si no encuentra por UserId, intentar por WalletId (como hace el admin)
        if (stakesByUserId.Count == 0)
        {
            _logger.LogInformation("No stakes found by UserId, trying WalletId: {WalletId}", wallet.Id);
            var filterByWalletId = Builders<Stake>.Filter.Eq(s => s.WalletId, wallet.Id);
            stakesByUserId = await _db.Stakes
                .Find(filterByWalletId)
                .ToListAsync();
            _logger.LogInformation("Found {Count} stakes by WalletId for walletId: {WalletId}", stakesByUserId.Count, wallet.Id);
        }

        var stakes = stakesByUserId
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        _logger.LogInformation("Total stakes found: {Count} for userId: {UserId}", stakes.Count, userId);

        var now = Helpers.DateTimeHelper.UtcNow;
        return stakes.Select(s =>
        {
            // Calcular recompensas acumuladas hasta ahora
            var last = s.LastAccrualAt ?? s.StartAt;
            var days = Math.Max(0, (now - last).TotalDays);
            var dailyRate = s.DailyRateBp / 10000m;
            var accrued = s.AccruedUsdt + s.PrincipalUsdt * dailyRate * (decimal)days;

            return new StakeDto
            {
                Id = s.Id,
                UserId = s.UserId,
                WalletId = s.WalletId,
                PrincipalUsdt = s.PrincipalUsdt,
                AccruedUsdt = s.AccruedUsdt,
                AccruedUsdtLive = Math.Round(accrued, 6),
                DailyRateBp = s.DailyRateBp,
                Status = s.Status,
                StartAt = s.StartAt,
                LastAccrualAt = s.LastAccrualAt,
                ClosedAt = s.ClosedAt,
                TransactionId = s.TransactionId,
                CreatedAt = s.CreatedAt
            };
        }).ToList();
    }

    /// <summary>
    /// Crea un nuevo stake: transfiere USDT del usuario a la wallet del admin
    /// </summary>
    public async Task<StakeDto> CreateStakeAsync(string userId, decimal amountUsdt)
    {
        // Validaciones básicas
        if (amountUsdt <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero");
        }

        // Validar saldo disponible del usuario
        var balances = await _walletService.GetBalancesAsync(userId);
        if (balances.Available < amountUsdt)
        {
            throw new InvalidOperationException("Insufficient available USDT");
        }

        // Obtener wallet del usuario
        var userWallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        if (userWallet == null)
        {
            throw new KeyNotFoundException("User wallet not found");
        }

        // Obtener wallet del admin (custody wallet)
        var adminUser = await _db.Users.Find(u => u.Email == "admin@gmail.com").FirstOrDefaultAsync();
        if (adminUser == null)
        {
            throw new KeyNotFoundException("Admin user not found");
        }

        await _adminWalletService.EnsureAdminWalletAsync(adminUser.Id);
        var adminWallet = await _adminWalletService.GetAdminWalletAsync(adminUser.Id);
        if (adminWallet == null)
        {
            throw new KeyNotFoundException("Admin wallet not found");
        }

        try
        {
            // Transferir USDT del usuario a la wallet del admin
            var userPk = _cryptoService.Decrypt(userWallet.PkEncrypted);
            var (ok, txid, error) = await _tronService.SendUsdtAsync(
                userWallet.Address,
                userPk,
                adminWallet.Address,
                amountUsdt
            );

            if (!ok || string.IsNullOrEmpty(txid))
            {
                throw new InvalidOperationException($"Failed to transfer USDT to admin wallet: {error}");
            }

            // Crear stake en MongoDB
            var now = Helpers.DateTimeHelper.UtcNow;
            var stake = new Stake
            {
                UserId = userId,
                WalletId = userWallet.Id,
                PrincipalUsdt = amountUsdt,
                AccruedUsdt = 0,
                DailyRateBp = _stakingSettings.DefaultDailyRateBp, // Usar tasa del backend
                Status = "active",
                StartAt = now,
                LastAccrualAt = now,
                TransactionId = txid,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _db.Stakes.InsertOneAsync(stake);

            _logger.LogInformation(
                "Stake created: {StakeId} for user {UserId}. Transferred {AmountUsdt} USDT to admin wallet. TX: {Txid}",
                stake.Id, userId, amountUsdt, txid);

            return new StakeDto
            {
                Id = stake.Id,
                UserId = stake.UserId,
                WalletId = stake.WalletId,
                PrincipalUsdt = stake.PrincipalUsdt,
                AccruedUsdt = stake.AccruedUsdt,
                AccruedUsdtLive = stake.AccruedUsdt,
                DailyRateBp = stake.DailyRateBp,
                Status = stake.Status,
                StartAt = stake.StartAt,
                LastAccrualAt = stake.LastAccrualAt,
                TransactionId = stake.TransactionId,
                CreatedAt = stake.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stake for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Cierra un stake y transfiere principal + recompensas de vuelta al usuario desde la wallet del admin
    /// </summary>
    public async Task<(decimal PrincipalUsdt, decimal RewardsUsdt)> CloseStakeAsync(string userId, string stakeId)
    {
        var stake = await _db.Stakes.Find(s => s.Id == stakeId && s.UserId == userId && s.Status == "active")
            .FirstOrDefaultAsync();
        
        if (stake == null)
        {
            throw new KeyNotFoundException("Stake not found or already closed");
        }

        // Calcular recompensas acumuladas
        await AccrueStakeAsync(stake);

        // Obtener stake actualizado
        stake = await _db.Stakes.Find(s => s.Id == stakeId).FirstOrDefaultAsync();
        if (stake == null)
        {
            throw new KeyNotFoundException("Stake not found after accrual");
        }
        
        // Validar que los valores en la base de datos sean razonables
        if (stake.PrincipalUsdt <= 0 || stake.PrincipalUsdt > 1_000_000m)
        {
            _logger.LogError("Stake {StakeId} has invalid PrincipalUsdt: {Principal}", stakeId, stake.PrincipalUsdt);
            throw new InvalidOperationException($"Invalid principal amount: {stake.PrincipalUsdt} USDT");
        }
        
        if (stake.AccruedUsdt < 0 || stake.AccruedUsdt > stake.PrincipalUsdt * 10m) // Máximo 10x el principal como recompensas
        {
            _logger.LogError("Stake {StakeId} has invalid AccruedUsdt: {Accrued} (Principal: {Principal})", 
                stakeId, stake.AccruedUsdt, stake.PrincipalUsdt);
            throw new InvalidOperationException($"Invalid accrued amount: {stake.AccruedUsdt} USDT. This may indicate a data corruption.");
        }

        // Obtener wallets
        var userWallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        if (userWallet == null)
        {
            throw new KeyNotFoundException("User wallet not found");
        }

        var adminUser = await _db.Users.Find(u => u.Email == "admin@gmail.com").FirstOrDefaultAsync();
        if (adminUser == null)
        {
            throw new KeyNotFoundException("Admin user not found");
        }

        var adminWallet = await _adminWalletService.GetAdminWalletAsync(adminUser.Id);
        if (adminWallet == null)
        {
            throw new KeyNotFoundException("Admin wallet not found");
        }

        // Calcular total a transferir (principal + recompensas)
        var totalToTransfer = stake.PrincipalUsdt + stake.AccruedUsdt;
        
        // Validar y redondear el valor a 6 decimales (USDT tiene 6 decimales)
        totalToTransfer = Math.Round(totalToTransfer, 6, MidpointRounding.AwayFromZero);
        
        // Validar que el valor no sea negativo o cero
        if (totalToTransfer <= 0)
        {
            throw new InvalidOperationException($"Invalid transfer amount: {totalToTransfer}");
        }
        
        // Validar que el valor no sea excesivamente grande (máximo 1 millón de USDT)
        if (totalToTransfer > 1_000_000m)
        {
            _logger.LogError("Stake {StakeId} has an invalid total amount: {Total}. Principal: {Principal}, Accrued: {Accrued}",
                stakeId, totalToTransfer, stake.PrincipalUsdt, stake.AccruedUsdt);
            throw new InvalidOperationException($"Transfer amount too large: {totalToTransfer} USDT. This may indicate a calculation error.");
        }

        _logger.LogInformation("Closing stake {StakeId}: Principal={Principal}, Accrued={Accrued}, Total={Total}",
            stakeId, stake.PrincipalUsdt, stake.AccruedUsdt, totalToTransfer);

        try
        {
            // Transferir desde la wallet del admin de vuelta al usuario
            var adminPk = _cryptoService.Decrypt(adminWallet.PkEncrypted);
            var (ok, txid, error) = await _tronService.SendUsdtAsync(
                adminWallet.Address,
                adminPk,
                userWallet.Address,
                totalToTransfer
            );

            if (!ok || string.IsNullOrEmpty(txid))
            {
                throw new InvalidOperationException($"Failed to transfer USDT from admin wallet: {error}");
            }

            // Cerrar stake
            await _db.Stakes.UpdateOneAsync(
                Builders<Stake>.Filter.Eq(s => s.Id, stakeId),
                Builders<Stake>.Update
                    .Set(s => s.Status, "closed")
                    .Set(s => s.ClosedAt, Helpers.DateTimeHelper.UtcNow)
                    .Set(s => s.UpdatedAt, Helpers.DateTimeHelper.UtcNow));

            _logger.LogInformation(
                "Stake closed: {StakeId} for user {UserId}. Transferred {Total} USDT (Principal: {Principal}, Rewards: {Rewards}) from admin wallet. TX: {Txid}",
                stakeId, userId, totalToTransfer, stake.PrincipalUsdt, stake.AccruedUsdt, txid);

            return (stake.PrincipalUsdt, stake.AccruedUsdt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing stake {StakeId} for user {UserId}", stakeId, userId);
            throw;
        }
    }

    /// <summary>
    /// Calcula y actualiza las recompensas acumuladas de un stake
    /// </summary>
    private async Task AccrueStakeAsync(Stake stake)
    {
        var now = Helpers.DateTimeHelper.UtcNow;
        var last = stake.LastAccrualAt ?? stake.StartAt;
        var days = Math.Max(0, (now - last).TotalDays);
        
        if (days <= 0) return;

        // Validar que no haya pasado demasiado tiempo (máximo 365 días sin actualizar)
        if (days > 365)
        {
            _logger.LogWarning("Stake {StakeId} has not been updated for {Days} days. Limiting to 365 days.", stake.Id, days);
            days = 365;
        }

        var dailyRate = stake.DailyRateBp / 10000m;
        var add = stake.PrincipalUsdt * dailyRate * (decimal)days;
        
        // Redondear las recompensas a 6 decimales
        add = Math.Round(add, 6, MidpointRounding.AwayFromZero);
        
        var newAccrued = stake.AccruedUsdt + add;
        
        // Asegurar que las recompensas acumuladas no sean negativas
        if (newAccrued < 0)
        {
            _logger.LogWarning("Stake {StakeId} accrued amount would be negative: {NewAccrued}. Resetting to 0.", stake.Id, newAccrued);
            newAccrued = 0;
        }
        
        // Redondear el total acumulado a 6 decimales
        newAccrued = Math.Round(newAccrued, 6, MidpointRounding.AwayFromZero);

        _logger.LogDebug("Accruing stake {StakeId}: Days={Days}, DailyRate={DailyRate}, Add={Add}, NewAccrued={NewAccrued}",
            stake.Id, days, dailyRate, add, newAccrued);

        await _db.Stakes.UpdateOneAsync(
            Builders<Stake>.Filter.Eq(s => s.Id, stake.Id),
            Builders<Stake>.Update
                .Set(s => s.AccruedUsdt, newAccrued)
                .Set(s => s.LastAccrualAt, now)
                .Set(s => s.UpdatedAt, now));
    }
}

public class StakeDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string WalletId { get; set; } = string.Empty;
    public decimal PrincipalUsdt { get; set; }
    public decimal AccruedUsdt { get; set; }
    public decimal AccruedUsdtLive { get; set; }
    public int DailyRateBp { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime? LastAccrualAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
}

