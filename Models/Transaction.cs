using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CunaPay.Api.Models;

[BsonIgnoreExtraElements]
public class Transaction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("wallet")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string WalletId { get; set; } = string.Empty;

    [BsonElement("txid")]
    public string? Txid { get; set; }

    [BsonElement("toAddress")]
    public string ToAddress { get; set; } = string.Empty;

    [BsonElement("amountUsdt")]
    public decimal AmountUsdt { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "pending"; // "pending", "broadcasted", "confirmed", "failed"

    [BsonElement("chainReceipt")]
    public BsonDocument? ChainReceipt { get; set; }

    [BsonElement("failCode")]
    public string? FailCode { get; set; }

    [BsonElement("failReason")]
    public string? FailReason { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;
}
