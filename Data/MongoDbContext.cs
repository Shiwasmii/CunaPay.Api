using MongoDB.Driver;
using CunaPay.Api.Models;
using CunaPay.Api.Configuration;

namespace CunaPay.Api.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var mongoSettings = configuration.GetSection("Mongo").Get<MongoSettings>() 
            ?? new MongoSettings { Uri = "mongodb://127.0.0.1:27017/cunapay", DbName = "cunapay" };
        
        var client = new MongoClient(mongoSettings.Uri);
        _database = client.GetDatabase(mongoSettings.DbName);
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Wallet> Wallets => _database.GetCollection<Wallet>("wallets");
    public IMongoCollection<Stake> Stakes => _database.GetCollection<Stake>("stakes");
    public IMongoCollection<Transaction> Transactions => _database.GetCollection<Transaction>("transactions");
    public IMongoCollection<News> News => _database.GetCollection<News>("news");
    public IMongoCollection<Purchase> Purchases => _database.GetCollection<Purchase>("purchases");
    public IMongoCollection<Withdrawal> Withdrawals => _database.GetCollection<Withdrawal>("withdrawals");
}
