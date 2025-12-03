namespace CunaPay.Api.Patterns.Behavioral;

/// <summary>
/// Observer Pattern - Eventos relacionados con compras de USDT
/// </summary>
public class PurchaseCreatedEvent : IDomainEvent
{
    public string PurchaseId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal AmountUsdt { get; set; }
    public decimal AmountBs { get; set; }
    public DateTime OccurredOn { get; } = Helpers.DateTimeHelper.UtcNow;
    public string EventType => "PurchaseCreated";
}

public class PurchaseReceiptUploadedEvent : IDomainEvent
{
    public string PurchaseId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime OccurredOn { get; } = Helpers.DateTimeHelper.UtcNow;
    public string EventType => "PurchaseReceiptUploaded";
}

public class PurchaseCompletedEvent : IDomainEvent
{
    public string PurchaseId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal AmountUsdt { get; set; }
    public DateTime OccurredOn { get; } = Helpers.DateTimeHelper.UtcNow;
    public string EventType => "PurchaseCompleted";
}

public class PurchaseRejectedEvent : IDomainEvent
{
    public string PurchaseId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime OccurredOn { get; } = Helpers.DateTimeHelper.UtcNow;
    public string EventType => "PurchaseRejected";
}

