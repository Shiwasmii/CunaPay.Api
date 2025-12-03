using MongoDB.Driver;
using System.Text.Json;
using System.Numerics;
using CunaPay.Api.Data;
using CunaPay.Api.Models;
using CunaPay.Api.Patterns.Structural;

namespace CunaPay.Api.Services
{
    public class WalletService : IWalletService
    {
        private readonly MongoDbContext _db;
        private readonly TronService _tronService;
        private readonly CryptoService _cryptoService;
        private readonly ILogger<WalletService> _logger;

        public WalletService(
            MongoDbContext db,
            TronService tronService,
            CryptoService cryptoService,
            ILogger<WalletService> logger)
        {
            _db = db;
            _tronService = tronService;
            _cryptoService = cryptoService;
            _logger = logger;
        }

        // --------------------------------------------------------------------
        // GET MY WALLET
        // --------------------------------------------------------------------
        public async Task<WalletDto> GetMyWalletAsync(string userId)
        {
            var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
            if (wallet == null)
                throw new KeyNotFoundException("Wallet not found");

            return new WalletDto
            {
                Id = wallet.Id,
                Address = wallet.Address
            };
        }

        // --------------------------------------------------------------------
        // GET BALANCES
        // --------------------------------------------------------------------
        public async Task<BalanceDto> GetBalancesAsync(string userId)
        {
            var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
            if (wallet == null)
                throw new KeyNotFoundException("Wallet not found");

            var trx = await _tronService.GetTrxBalanceAsync(wallet.Address);
            var usdt = await _tronService.GetUsdtBalanceAsync(wallet.Address);

            // Calcular USDT bloqueado en staking activo
            var activeStakes = await _db.Stakes
                .Find(s => s.UserId == userId && s.Status == "active")
                .ToListAsync();

            var locked = activeStakes.Sum(s => s.PrincipalUsdt);
            var available = Math.Max(0, usdt - locked);

            return new BalanceDto
            {
                WalletId = wallet.Id,
                Address = wallet.Address,
                Trx = trx,
                Usdt = usdt,
                LockedInStaking = locked,
                Available = available
            };
        }

        // --------------------------------------------------------------------
        // LIST LOCAL TRANSACTIONS (OFF-CHAIN)
        // --------------------------------------------------------------------
        public async Task<List<TransactionDto>> ListTransactionsAsync(
            string userId,
            int? limit = null,
            string? status = null)
        {
            var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
            if (wallet == null)
                throw new KeyNotFoundException("Wallet not found");

            var filterBuilder = Builders<Transaction>.Filter;
            var filter = filterBuilder.Eq(t => t.WalletId, wallet.Id);

            if (!string.IsNullOrEmpty(status))
                filter &= filterBuilder.Eq(t => t.Status, status);

            var cappedLimit = Math.Max(1, Math.Min(limit ?? 25, 100));

            var transactions = await _db.Transactions
                .Find(filter)
                .SortByDescending(t => t.CreatedAt)
                .Limit(cappedLimit)
                .ToListAsync();

            return transactions.Select(t => new TransactionDto
            {
                Id = t.Id,
                Txid = t.Txid,
                To = t.ToAddress,
                AmountUsdt = t.AmountUsdt,
                Status = t.Status,
                FailCode = t.FailCode,
                FailReason = t.FailReason,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();
        }

        // --------------------------------------------------------------------
        // SEND FROM CUSTODY (USDT)
        // --------------------------------------------------------------------
        public async Task<(bool Ok, string? Txid, string Status)> SendFromCustodyAsync(
            string userId,
            string toAddress,
            string amountUsdt)
        {
            if (!decimal.TryParse(amountUsdt, out var amount) || amount <= 0)
                throw new ArgumentException("Amount must be a positive decimal string");

            var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
            if (wallet == null)
                throw new KeyNotFoundException("Wallet not found");

            var balances = await GetBalancesAsync(userId);
            if (balances.Available < amount)
                throw new InvalidOperationException("Insufficient available USDT (staking lock)");

            var privateKey = _cryptoService.Decrypt(wallet.PkEncrypted);

            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                ToAddress = toAddress,
                AmountUsdt = amount,
                Status = "pending",
                CreatedAt = Helpers.DateTimeHelper.UtcNow,
                UpdatedAt = Helpers.DateTimeHelper.UtcNow
            };

            await _db.Transactions.InsertOneAsync(transaction);

            try
            {
                var (ok, txid, error) = await _tronService.SendUsdtAsync(
                    wallet.Address,
                    privateKey,
                    toAddress,
                    amount
                );

                if (!ok)
                {
                    await _db.Transactions.UpdateOneAsync(
                        Builders<Transaction>.Filter.Eq(t => t.Id, transaction.Id),
                        Builders<Transaction>.Update
                            .Set(t => t.Status, "failed")
                            .Set(t => t.FailReason, error));

                    throw new InvalidOperationException(error ?? "Send failed");
                }

                // Update TX as broadcasted
                await _db.Transactions.UpdateOneAsync(
                    Builders<Transaction>.Filter.Eq(t => t.Id, transaction.Id),
                    Builders<Transaction>.Update
                        .Set(t => t.Status, "broadcasted")
                        .Set(t => t.Txid, txid));

                return (true, txid, "broadcasted");
            }
            catch (Exception ex)
            {
                await _db.Transactions.UpdateOneAsync(
                    Builders<Transaction>.Filter.Eq(t => t.Id, transaction.Id),
                    Builders<Transaction>.Update
                        .Set(t => t.Status, "failed")
                        .Set(t => t.FailReason, ex.Message));

                throw;
            }
        }

        // --------------------------------------------------------------------
        // LIST ON-CHAIN TRANSACTIONS (USDT TRC20 + TRX Native)
        // --------------------------------------------------------------------
        public async Task<OnChainTransactionsDto> ListOnChainTransactionsAsync(
            string userId,
            int? limit = null,
            string? direction = null,
            string? fingerprint = null)
        {
            var wallet = await _db.Wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
            if (wallet == null)
                throw new KeyNotFoundException("Wallet not found");

            var items = new List<OnChainTransactionItem>();
            var limitPerType = limit.HasValue ? (limit.Value / 2) + 1 : 25; // Dividir el límite entre USDT y TRX

            // Obtener transacciones USDT (TRC20)
            try
            {
                var usdtResult = await _tronService.GetTrc20TransfersAsync(wallet.Address, limitPerType, fingerprint);

                if (usdtResult.TryGetProperty("data", out var usdtDataArray) &&
                    usdtDataArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in usdtDataArray.EnumerateArray())
                    {
                        var decimals = 6; // USDT tiene 6 decimales por defecto
                        if (item.TryGetProperty("token_info", out var tokenInfo) &&
                            tokenInfo.TryGetProperty("decimals", out var decElem))
                        {
                            decimals = decElem.GetInt32();
                        }

                        // Obtener el valor (puede venir como string hexadecimal o numérico)
                        decimal amountDecimal = 0m;
                        
                        if (!item.TryGetProperty("value", out var valueProp))
                        {
                            _logger.LogWarning("Transaction item missing 'value' property");
                            continue;
                        }
                        
                        try
                        {
                            BigInteger valueBigInt = 0;
                            
                            if (valueProp.ValueKind == JsonValueKind.String)
                            {
                                var valueStr = valueProp.GetString()!;
                                if (string.IsNullOrEmpty(valueStr))
                                {
                                    _logger.LogWarning("Transaction value is empty string");
                                    continue;
                                }

                                var originalValueStr = valueStr;
                                
                                // Remover prefijo 0x si existe
                                bool isHex = valueStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                                if (isHex)
                                {
                                    valueStr = valueStr.Substring(2);
                                }

                                // Si tiene prefijo 0x o contiene letras a-f/A-F, tratar como hexadecimal
                                if (isHex || valueStr.Any(c => (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                                {
                                    // Es hexadecimal
                                    try
                                    {
                                        valueBigInt = BigInteger.Parse(valueStr, System.Globalization.NumberStyles.HexNumber);
                                        _logger.LogDebug("Parsed as hex: {Original} -> {Parsed}", originalValueStr, valueBigInt);
                                    }
                                    catch (Exception hexEx)
                                    {
                                        _logger.LogWarning(hexEx, "Failed to parse value as hex: {ValueStr}, trying as decimal", valueStr);
                                        // Si falla como hex, intentar como decimal
                                        if (!BigInteger.TryParse(valueStr, out valueBigInt))
                                        {
                                            _logger.LogWarning("Failed to parse value as decimal after hex failure: {ValueStr}", valueStr);
                                            continue;
                                        }
                                        _logger.LogDebug("Parsed as decimal after hex failure: {ValueStr} -> {Parsed}", valueStr, valueBigInt);
                                    }
                                }
                                else
                                {
                                    // Solo dígitos, tratar como decimal primero (más común en APIs modernas)
                                    if (BigInteger.TryParse(valueStr, out valueBigInt))
                                    {
                                        _logger.LogDebug("Parsed as decimal: {ValueStr} -> {Parsed}", valueStr, valueBigInt);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Failed to parse value as decimal: {ValueStr}", valueStr);
                                        continue;
                                    }
                                }
                            }
                            else if (valueProp.ValueKind == JsonValueKind.Number)
                            {
                                // Si viene como número directamente, obtener el valor raw
                                var valueStr = valueProp.GetRawText();
                                if (!BigInteger.TryParse(valueStr, out valueBigInt))
                                {
                                    _logger.LogWarning("Failed to parse value as BigInteger from number: {ValueStr}", valueStr);
                                    continue;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Unexpected value type: {ValueKind}", valueProp.ValueKind);
                                continue;
                            }

                            // Convertir a decimal dividiendo por 10^decimals
                            var divider = (decimal)Math.Pow(10, decimals);
                            amountDecimal = (decimal)valueBigInt / divider;
                            
                            // Redondear a 6 decimales para USDT
                            amountDecimal = Math.Round(amountDecimal, 6, MidpointRounding.AwayFromZero);
                            
                            _logger.LogDebug("Parsed USDT amount: {Amount} from value: {Value}, decimals: {Decimals}", 
                                amountDecimal, valueBigInt, decimals);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error parsing USDT transaction value");
                            continue;
                        }

                        var from = item.GetProperty("from").GetString()!;
                        var to = item.GetProperty("to").GetString()!;

                        var dir = from.Equals(wallet.Address, StringComparison.OrdinalIgnoreCase)
                            ? "out"
                            : "in";

                        if (!string.IsNullOrEmpty(direction) && dir != direction)
                            continue;

                        items.Add(new OnChainTransactionItem
                        {
                            Txid = item.GetProperty("transaction_id").GetString()!,
                            From = from,
                            To = to,
                            Currency = "USDT",
                            AmountUsdt = amountDecimal,
                            AmountTrx = 0,
                            Direction = dir,
                            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(
                                    item.GetProperty("block_timestamp").GetInt64())
                                .UtcDateTime,
                            Confirmed = item.TryGetProperty("confirmed", out var confElem) &&
                                        confElem.GetBoolean()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching USDT transactions for user {UserId}", userId);
            }

            // Obtener transacciones TRX nativas
            try
            {
                var trxResult = await _tronService.GetTrxTransactionsAsync(wallet.Address, limitPerType, fingerprint);

                if (trxResult.TryGetProperty("data", out var trxDataArray) &&
                    trxDataArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in trxDataArray.EnumerateArray())
                    {
                        // TRX tiene 6 decimales (sun = 1/1,000,000 TRX)
                        decimal amountTrx = 0m;
                        string from = "";
                        string to = "";
                        string txid = "";
                        DateTime timestamp = Helpers.DateTimeHelper.UtcNow;
                        
                        // Obtener txid
                        if (item.TryGetProperty("txID", out var txidProp))
                            txid = txidProp.GetString() ?? "";
                        else if (item.TryGetProperty("transaction_id", out var txidProp2))
                            txid = txidProp2.GetString() ?? "";

                        // Obtener timestamp
                        if (item.TryGetProperty("block_timestamp", out var tsProp))
                            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(tsProp.GetInt64()).UtcDateTime;

                        // Buscar TransferContract en los contratos
                        if (item.TryGetProperty("raw_data", out var rawData) &&
                            rawData.TryGetProperty("contract", out var contracts) &&
                            contracts.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var contract in contracts.EnumerateArray())
                            {
                                if (contract.TryGetProperty("type", out var typeProp) &&
                                    typeProp.GetString() == "TransferContract")
                                {
                                    if (contract.TryGetProperty("parameter", out var param) &&
                                        param.TryGetProperty("value", out var value))
                                    {
                                        // TRX se almacena en "amount" como sun (1 TRX = 1,000,000 sun)
                                        if (value.TryGetProperty("amount", out var amountProp))
                                        {
                                            var amountSun = amountProp.GetInt64();
                                            amountTrx = amountSun / 1_000_000m; // Convertir sun a TRX
                                        }

                                        from = value.TryGetProperty("owner_address", out var fromProp)
                                            ? fromProp.GetString() ?? ""
                                            : "";
                                        to = value.TryGetProperty("to_address", out var toProp)
                                            ? toProp.GetString() ?? ""
                                            : "";

                                        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || amountTrx == 0)
                                            continue;

                                        var dir = from.Equals(wallet.Address, StringComparison.OrdinalIgnoreCase)
                                            ? "out"
                                            : "in";

                                        if (!string.IsNullOrEmpty(direction) && dir != direction)
                                            continue;

                                        items.Add(new OnChainTransactionItem
                                        {
                                            Txid = txid,
                                            From = from,
                                            To = to,
                                            Currency = "TRX",
                                            AmountUsdt = 0,
                                            AmountTrx = amountTrx,
                                            Direction = dir,
                                            Timestamp = timestamp,
                                            Confirmed = true // Las transacciones en la blockchain están confirmadas
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching TRX transactions for user {UserId}", userId);
            }

            // Ordenar por timestamp descendente y limitar
            items = items
                .OrderByDescending(i => i.Timestamp)
                .Take(limit ?? 50)
                .ToList();

            return new OnChainTransactionsDto
            {
                Address = wallet.Address,
                Contract = null, // Puede haber múltiples contratos (USDT y TRX)
                Fingerprint = fingerprint,
                Items = items
            };
        }
    }

    // ------------------------------------------------------------------------
    // DTOs
    // ------------------------------------------------------------------------

    public class BalanceDto
    {
        public string WalletId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Trx { get; set; }
        public decimal Usdt { get; set; }
        public decimal LockedInStaking { get; set; }
        public decimal Available { get; set; }
    }

    public class TransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Txid { get; set; }
        public string To { get; set; } = string.Empty;
        public decimal AmountUsdt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? FailCode { get; set; }
        public string? FailReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class OnChainTransactionsDto
    {
        public string Address { get; set; } = string.Empty;
        public string? Contract { get; set; }
        public string? Fingerprint { get; set; }
        public List<OnChainTransactionItem> Items { get; set; } = new();
    }

    public class OnChainTransactionItem
    {
        public string Txid { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Currency { get; set; } = "USDT"; // "USDT" o "TRX"
        public decimal AmountUsdt { get; set; }
        public decimal AmountTrx { get; set; }
        public string Direction { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool Confirmed { get; set; }
    }
}