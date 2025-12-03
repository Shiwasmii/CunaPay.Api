using Microsoft.Extensions.Caching.Memory;
using CunaPay.Api.Services;

namespace CunaPay.Api.Patterns.Structural;

/// <summary>
/// Decorator Pattern - Agrega funcionalidad de cache a WalletService
/// </summary>
public class CachedWalletService : IWalletService
{
    private readonly IWalletService _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedWalletService> _logger;

    public CachedWalletService(
        IWalletService inner,
        IMemoryCache cache,
        ILogger<CachedWalletService> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<WalletDto> GetMyWalletAsync(string userId)
    {
        var cacheKey = $"wallet:{userId}";
        
        if (_cache.TryGetValue(cacheKey, out WalletDto? cached))
        {
            _logger.LogDebug("Cache hit for wallet {UserId}", userId);
            return cached!;
        }

        var result = await _inner.GetMyWalletAsync(userId);
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        
        return result;
    }

    public async Task<BalanceDto> GetBalancesAsync(string userId)
    {
        var cacheKey = $"balance:{userId}";
        
        if (_cache.TryGetValue(cacheKey, out BalanceDto? cached))
        {
            _logger.LogDebug("Cache hit for balance {UserId}", userId);
            return cached!;
        }

        var result = await _inner.GetBalancesAsync(userId);
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30)); // Balance cambia frecuentemente
        
        return result;
    }

    public Task<List<TransactionDto>> ListTransactionsAsync(string userId, int? limit = null, string? status = null)
    {
        // No cacheamos transacciones ya que cambian frecuentemente
        return _inner.ListTransactionsAsync(userId, limit, status);
    }

    public Task<(bool Ok, string? Txid, string Status)> SendFromCustodyAsync(
        string userId, string toAddress, string amountUsdt)
    {
        // Invalidar cache después de enviar
        var result = _inner.SendFromCustodyAsync(userId, toAddress, amountUsdt);
        
        // Invalidar cache de balance después de enviar
        result.ContinueWith(_ =>
        {
            _cache.Remove($"balance:{userId}");
            _cache.Remove($"wallet:{userId}");
        }, TaskContinuationOptions.OnlyOnRanToCompletion);
        
        return result;
    }

    public Task<OnChainTransactionsDto> ListOnChainTransactionsAsync(
        string userId, int? limit = null, string? direction = null, string? fingerprint = null)
    {
        return _inner.ListOnChainTransactionsAsync(userId, limit, direction, fingerprint);
    }
}

