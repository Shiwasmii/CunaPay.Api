using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CunaPay.Api.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [BsonElement("lastName")]
    public string LastName { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = "User"; // "User" o "Admin"

    [BsonElement("bankAccountNumber")]
    public string? BankAccountNumber { get; set; }

    [BsonElement("bankEntity")]
    public string? BankEntity { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;
}

