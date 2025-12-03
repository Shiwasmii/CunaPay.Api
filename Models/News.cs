using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CunaPay.Api.Models;

public class News
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("link")]
    public string Link { get; set; } = string.Empty;

    [BsonElement("image")]
    public string Image { get; set; } = string.Empty;

    [BsonElement("economicImpact")]
    public string EconomicImpact { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;
}

