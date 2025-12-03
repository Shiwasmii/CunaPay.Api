using MongoDB.Driver;
using CunaPay.Api.Data;
using CunaPay.Api.Models;
using CunaPay.Api.Services;

namespace CunaPay.Api.Services;

/// <summary>
/// Servicio para gestionar wallets de administradores
/// </summary>
public class AdminWalletService
{
    private readonly MongoDbContext _db;
    private readonly TronService _tronService;
    private readonly CryptoService _cryptoService;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminWalletService> _logger;

    public AdminWalletService(
        MongoDbContext db,
        TronService tronService,
        CryptoService cryptoService,
        IConfiguration config,
        ILogger<AdminWalletService> logger)
    {
        _db = db;
        _tronService = tronService;
        _cryptoService = cryptoService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Asegura que un admin tenga una wallet. Si no existe, usa la wallet de custodia de la API.
    /// </summary>
    public async Task<Wallet> EnsureAdminWalletAsync(string adminId)
    {
        var existingWallet = await _db.Wallets.Find(w => w.UserId == adminId).FirstOrDefaultAsync();
        if (existingWallet != null)
        {
            return existingWallet;
        }

        // Usar la wallet de custodia de la API (la misma que usa tron.api)
        var custodyPrivateKey = _config["Tron:CustodyPrivateKey"] 
            ?? throw new InvalidOperationException("CustodyPrivateKey not configured");
        
        // Obtener la dirección de la wallet de custodia desde la private key
        var custodyAddress = await _tronService.GetAddressFromPrivateKeyAsync(custodyPrivateKey);
        var pkEncrypted = _cryptoService.Encrypt(custodyPrivateKey);

        var wallet = new Wallet
        {
            UserId = adminId,
            Address = custodyAddress,
            PkEncrypted = pkEncrypted,
            CreatedAt = Helpers.DateTimeHelper.UtcNow,
            UpdatedAt = Helpers.DateTimeHelper.UtcNow
        };

        await _db.Wallets.InsertOneAsync(wallet);

        _logger.LogInformation("Admin wallet configured with custody wallet {AdminId}: {Address}", adminId, custodyAddress);

        return wallet;
    }

    /// <summary>
    /// Obtiene la wallet de un admin
    /// </summary>
    public async Task<Wallet?> GetAdminWalletAsync(string adminId)
    {
        return await _db.Wallets.Find(w => w.UserId == adminId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Obtiene el balance de un admin
    /// </summary>
    public async Task<decimal> GetAdminBalanceAsync(string adminId)
    {
        var wallet = await GetAdminWalletAsync(adminId);
        if (wallet == null)
            return 0;

        return await _tronService.GetUsdtBalanceAsync(wallet.Address);
    }

    /// <summary>
    /// Obtiene el balance completo de un admin (TRX y USDT, sin staking)
    /// </summary>
    public async Task<(decimal Trx, decimal Usdt)> GetAdminBalanceFullAsync(string adminId)
    {
        var wallet = await GetAdminWalletAsync(adminId);
        if (wallet == null)
            return (0, 0);

        var trx = await _tronService.GetTrxBalanceAsync(wallet.Address);
        var usdt = await _tronService.GetUsdtBalanceAsync(wallet.Address);

        return (trx, usdt);
    }
}

