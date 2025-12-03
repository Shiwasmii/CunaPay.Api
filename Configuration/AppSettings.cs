namespace CunaPay.Api.Configuration;

public class AppSettings
{
    public string Environment { get; set; } = "Development";
    public int Port { get; set; } = 4000;

    public JwtSettings Jwt { get; set; } = new();
    public CryptoSettings Crypto { get; set; } = new();
    public MongoSettings Mongo { get; set; } = new();
    public TronSettings Tron { get; set; } = new();
    public WorkerSettings Workers { get; set; } = new();
    public StakingSettings Staking { get; set; } = new();
}

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string ExpiresIn { get; set; } = "24h";
}

public class CryptoSettings
{
    public string MasterKeyHex { get; set; } = string.Empty;
}

public class MongoSettings
{
    public string Uri { get; set; } = "mongodb://127.0.0.1:27017/cunapay";
    public string DbName { get; set; } = "cunapay";
}

public class TronSettings
{
    public string FullNode { get; set; } = "https://api.nileex.io";
    public string SolidityNode { get; set; } = string.Empty;
    public string? EventServer { get; set; }
    public string UsdtContract { get; set; } = "TXYZopYRdj2D9XRtbG411XZZ3kM5VkAeBf";
    public string CustodyPrivateKey { get; set; } = string.Empty;
    public string TronGridBase { get; set; } = "https://nile.trongrid.io";
    public string? TronGridApiKey { get; set; }
}

public class WorkerSettings
{
    public int TxWatcherIntervalMs { get; set; } = 8000;
}

public class StakingSettings
{
    public int DefaultDailyRateBp { get; set; } = 10; // 0.1% diario (10 basis points = 0.1%)
    public decimal MinAmountUsdt { get; set; } = 10;
    public decimal MaxAmountUsdt { get; set; } = 10000;
}

