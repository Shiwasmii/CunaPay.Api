using CunaPay.Api.Services;
using System.Diagnostics;

namespace CunaPay.Api.Patterns.Structural;

/// <summary>
/// Decorator Pattern - Agrega logging detallado a WalletService
/// </summary>
public class LoggedWalletService : IWalletService
{
    private readonly IWalletService _inner;
    private readonly ILogger<LoggedWalletService> _logger;

    public LoggedWalletService(
        IWalletService inner,
        ILogger<LoggedWalletService> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<WalletDto> GetMyWalletAsync(string userId)
    {
        _logger.LogInformation("Getting wallet for user {UserId}", userId);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await _inner.GetMyWalletAsync(userId);
            stopwatch.Stop();
            _logger.LogInformation("Wallet retrieved for user {UserId} in {ElapsedMs}ms", 
                userId, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error getting wallet for user {UserId} after {ElapsedMs}ms", 
                userId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<BalanceDto> GetBalancesAsync(string userId)
    {
        _logger.LogInformation("Getting balance for user {UserId}", userId);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await _inner.GetBalancesAsync(userId);
            stopwatch.Stop();
            _logger.LogInformation("Balance retrieved for user {UserId}: USDT={Usdt}, Available={Available} in {ElapsedMs}ms", 
                userId, result.Usdt, result.Available, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error getting balance for user {UserId} after {ElapsedMs}ms", 
                userId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public Task<List<TransactionDto>> ListTransactionsAsync(string userId, int? limit = null, string? status = null)
    {
        _logger.LogInformation("Listing transactions for user {UserId}, limit={Limit}, status={Status}", 
            userId, limit, status);
        return _inner.ListTransactionsAsync(userId, limit, status);
    }

    public async Task<(bool Ok, string? Txid, string Status)> SendFromCustodyAsync(
        string userId, string toAddress, string amountUsdt)
    {
        _logger.LogInformation("Sending {Amount} USDT from user {UserId} to {ToAddress}", 
            amountUsdt, userId, toAddress);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await _inner.SendFromCustodyAsync(userId, toAddress, amountUsdt);
            stopwatch.Stop();
            
            if (result.Ok)
            {
                _logger.LogInformation("Transaction sent successfully. Txid={Txid}, Status={Status} in {ElapsedMs}ms", 
                    result.Txid, result.Status, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("Transaction failed. Status={Status} in {ElapsedMs}ms", 
                    result.Status, stopwatch.ElapsedMilliseconds);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error sending transaction for user {UserId} after {ElapsedMs}ms", 
                userId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public Task<OnChainTransactionsDto> ListOnChainTransactionsAsync(
        string userId, int? limit = null, string? direction = null, string? fingerprint = null)
    {
        _logger.LogInformation("Listing on-chain transactions for user {UserId}", userId);
        return _inner.ListOnChainTransactionsAsync(userId, limit, direction, fingerprint);
    }
}

