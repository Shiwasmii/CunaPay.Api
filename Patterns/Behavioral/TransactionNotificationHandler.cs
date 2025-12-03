namespace CunaPay.Api.Patterns.Behavioral;

/// <summary>
/// Observer Pattern - Handler que envía notificaciones cuando ocurren eventos de transacciones
/// </summary>
public class TransactionNotificationHandler : 
    IEventHandler<TransactionBroadcastedEvent>,
    IEventHandler<TransactionConfirmedEvent>,
    IEventHandler<TransactionFailedEvent>
{
    private readonly ILogger<TransactionNotificationHandler> _logger;

    public TransactionNotificationHandler(ILogger<TransactionNotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(TransactionBroadcastedEvent @event)
    {
        _logger.LogInformation(
            "📤 Transaction {TransactionId} broadcasted. Txid: {Txid}. User: {UserId}. " +
            "Would send notification: 'Your transaction has been sent and is pending confirmation'",
            @event.TransactionId, @event.Txid, @event.UserId);
        
        // Aquí se podría integrar con un servicio de notificaciones (email, push, SMS, etc.)
        // await _notificationService.SendAsync(@event.UserId, "Transaction sent", ...);
        
        return Task.CompletedTask;
    }

    public Task HandleAsync(TransactionConfirmedEvent @event)
    {
        _logger.LogInformation(
            "✅ Transaction {TransactionId} confirmed. Txid: {Txid}. User: {UserId}. " +
            "Would send notification: 'Your transaction has been confirmed'",
            @event.TransactionId, @event.Txid, @event.UserId);
        
        // await _notificationService.SendAsync(@event.UserId, "Transaction confirmed", ...);
        
        return Task.CompletedTask;
    }

    public Task HandleAsync(TransactionFailedEvent @event)
    {
        _logger.LogWarning(
            "❌ Transaction {TransactionId} failed. User: {UserId}. Error: {Error}. " +
            "Would send notification: 'Your transaction failed: {Error}'",
            @event.TransactionId, @event.UserId, @event.Error, @event.Error);
        
        // await _notificationService.SendAsync(@event.UserId, "Transaction failed", ...);
        
        return Task.CompletedTask;
    }
}

