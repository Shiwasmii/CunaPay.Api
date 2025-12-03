using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CunaPay.Api.Models;

public class Wallet
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("user")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("address")]
    public string Address { get; set; } = string.Empty;

    [BsonElement("pkEncrypted")]
    public string PkEncrypted { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;
}

