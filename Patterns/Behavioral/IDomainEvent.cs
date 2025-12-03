namespace CunaPay.Api.Patterns.Behavioral;

/// <summary>
/// Observer Pattern - Interfaz base para eventos de dominio
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
    string EventType { get; }
}

/// <summary>
/// Interfaz para handlers de eventos
/// </summary>
public interface IEventHandler<in T> where T : IDomainEvent
{
    Task HandleAsync(T @event);
}

/// <summary>
/// Bus de eventos para publicar y suscribirse a eventos
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T @event) where T : IDomainEvent;
    void Subscribe<T>(IEventHandler<T> handler) where T : IDomainEvent;
}

