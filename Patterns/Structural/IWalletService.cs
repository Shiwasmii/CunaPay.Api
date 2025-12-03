using CunaPay.Api.Services;

namespace CunaPay.Api.Patterns.Structural;

/// <summary>
/// Interfaz para WalletService (necesaria para Decorator)
/// </summary>
public interface IWalletService
{
    Task<WalletDto> GetMyWalletAsync(string userId);
    Task<BalanceDto> GetBalancesAsync(string userId);
    Task<List<TransactionDto>> ListTransactionsAsync(string userId, int? limit = null, string? status = null);
    Task<(bool Ok, string? Txid, string Status)> SendFromCustodyAsync(
        string userId, string toAddress, string amountUsdt);
    Task<OnChainTransactionsDto> ListOnChainTransactionsAsync(
        string userId, int? limit = null, string? direction = null, string? fingerprint = null);
}

