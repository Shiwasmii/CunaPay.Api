namespace CunaPay.Api.Patterns.Behavioral;

/// <summary>
/// Observer Pattern - Implementación en memoria del bus de eventos
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly Dictionary<Type, List<object>> _handlers = new();
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
    }

    public void Subscribe<T>(IEventHandler<T> handler) where T : IDomainEvent
    {
        var eventType = typeof(T);
        
        if (!_handlers.ContainsKey(eventType))
        {
            _handlers[eventType] = new List<object>();
        }

        _handlers[eventType].Add(handler);
        _logger.LogInformation("Subscribed handler {HandlerType} to event {EventType}", 
            handler.GetType().Name, eventType.Name);
    }

    public async Task PublishAsync<T>(T @event) where T : IDomainEvent
    {
        var eventType = typeof(T);
        
        if (!_handlers.ContainsKey(eventType))
        {
            _logger.LogDebug("No handlers registered for event {EventType}", eventType.Name);
            return;
        }

        var handlers = _handlers[eventType].Cast<IEventHandler<T>>().ToList();
        
        _logger.LogInformation("Publishing event {EventType} to {HandlerCount} handlers", 
            eventType.Name, handlers.Count);

        var tasks = handlers.Select(handler => 
            Task.Run(async () =>
            {
                try
                {
                    await handler.HandleAsync(@event);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event {EventType} in handler {HandlerType}", 
                        eventType.Name, handler.GetType().Name);
                }
            }));

        await Task.WhenAll(tasks);
    }
}

