using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CunaPay.Api.Models;

/// <summary>
/// Modelo para solicitudes de compra de USDT con BS
/// </summary>
public class Purchase
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("user")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("wallet")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string WalletId { get; set; } = string.Empty;

    [BsonElement("amountUsdt")]
    public decimal AmountUsdt { get; set; }

    [BsonElement("amountBs")]
    public decimal AmountBs { get; set; }

    [BsonElement("pricePerUsdt")]
    public decimal PricePerUsdt { get; set; } // Precio en BS por USDT (cotización + 0.10 BS)

    [BsonElement("status")]
    public string Status { get; set; } = "pending"; // pending, accepted, rejected

    [BsonElement("receiptImageUrl")]
    public string? ReceiptImageUrl { get; set; } // URL de la imagen del comprobante

    [BsonElement("receiptFileName")]
    public string? ReceiptFileName { get; set; }

    [BsonElement("rejectionReason")]
    public string? RejectionReason { get; set; }

    [BsonElement("adminNotes")]
    public string? AdminNotes { get; set; }

    [BsonElement("processedBy")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ProcessedBy { get; set; } // ID del admin que procesó

    [BsonElement("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = Helpers.DateTimeHelper.UtcNow;
}

