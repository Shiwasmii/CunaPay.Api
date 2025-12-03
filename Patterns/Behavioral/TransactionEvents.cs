namespace CunaPay.Api.Patterns.Behavioral;

/// <summary>
/// Observer Pattern - Eventos relacionados con transacciones
/// </summary>
public class TransactionCreatedEvent : IDomainEvent
{
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OccurredOn { get; } = Helpers.DateTimeHelper.UtcNow;
    public string EventType => "TransactionCreated";
}

public class TransactionBroadcastedEvent : IDomainEvent
{
    public string TransactionId { get; set; } = string.Empty;
    public string Txid { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime OccurredOn { get; } = Helpers.DateTimeHelper.UtcNow;
    public string EventType => "TransactionBroadcasted";
}

public class TransactionConfirmedEvent : IDomainEvent
{
    public string TransactionId { get; set; } = string.Empty;
    public string Txid { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime OccurredOn { get; } = Helpers.DateTimeHelper.UtcNow;
    public string EventType => "TransactionConfirmed";
}

public class TransactionFailedEvent : IDomainEvent
{
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime OccurredOn { get; } = Helpers.DateTimeHelper.UtcNow;
    public string EventType => "TransactionFailed";
}

