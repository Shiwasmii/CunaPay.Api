using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CunaPay.Api.Models;

/// <summary>
/// Staking centralizado: el usuario deposita USDT que va a la wallet del admin
/// </summary>
public class Stake
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("walletId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string WalletId { get; set; } = string.Empty;

    [BsonElement("principalUsdt")]
    public decimal PrincipalUsdt { get; set; }

    [BsonElement("accruedUsdt")]
    public decimal AccruedUsdt { get; set; }

    [BsonElement("dailyRateBp")]
    public int DailyRateBp { get; set; } // Tasa diaria en basis points (25 = 0.25% diario)

    [BsonElement("status")]
    public string Status { get; set; } = "active"; // "active" or "closed"

    [BsonElement("startAt")]
    public DateTime StartAt { get; set; } = Helpers.DateTimeHelper.UtcNow;

    [BsonElement("lastAccrualAt")]
    public DateTime? LastAccrualAt { get; set; }

    [BsonElement("closedAt")]
    public DateTime? ClosedAt { get; set; }

    [BsonElement("transactionId")]
    public string? TransactionId { get; set; } // TXID cuando se transfirió a la wallet del admin

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;
}
