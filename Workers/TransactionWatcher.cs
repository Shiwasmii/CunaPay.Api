using MongoDB.Driver;
using MongoDB.Bson;
using CunaPay.Api.Data;
using CunaPay.Api.Models;
using CunaPay.Api.Services;

namespace CunaPay.Api.Workers;

public class TransactionWatcher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TransactionWatcher> _logger;
    private readonly int _intervalMs;

    public TransactionWatcher(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<TransactionWatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _intervalMs = configuration.GetValue<int>("Workers:TxWatcherIntervalMs", 8000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TransactionWatcher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckPendingTransactionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TransactionWatcher tick");
            }

            await Task.Delay(_intervalMs, stoppingToken);
        }
    }

    private async Task CheckPendingTransactionsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var tronService = scope.ServiceProvider.GetRequiredService<TronService>();

        var filter = Builders<Transaction>.Filter.And(
            Builders<Transaction>.Filter.Eq(t => t.Status, "broadcasted"),
            Builders<Transaction>.Filter.Ne(t => t.Txid, (string?)null)
        );

        var pending = await db.Transactions
            .Find(filter)
            .SortBy(t => t.CreatedAt)
            .Limit(25)
            .ToListAsync();

        foreach (var tx in pending)
        {
            if (string.IsNullOrEmpty(tx.Txid)) continue;

            try
            {
                var info = await tronService.GetTransactionInfoAsync(tx.Txid);
                if (info.HasValue && 
                    info.Value.TryGetProperty("receipt", out var receipt) &&
                    receipt.TryGetProperty("result", out var result) &&
                    result.GetString() == "SUCCESS")
                {
                    // Convert JsonElement to BsonDocument
                    var bsonDoc = BsonDocument.Parse(info.Value.GetRawText());
                    
                    await db.Transactions.UpdateOneAsync(
                        Builders<Transaction>.Filter.Eq(t => t.Id, tx.Id),
                        Builders<Transaction>.Update
                            .Set(t => t.Status, "confirmed")
                            .Set(t => t.ChainReceipt, bsonDoc)
                            .Set(t => t.UpdatedAt, Helpers.DateTimeHelper.UtcNow));

                    _logger.LogInformation("Transaction {Txid} confirmed", tx.Txid);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking transaction {Txid}", tx.Txid);
            }
        }
    }
}

