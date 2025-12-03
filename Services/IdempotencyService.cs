using System.Collections.Concurrent;

namespace CunaPay.Api.Services;

public record IdempotencyEntry(int StatusCode, object? Body, DateTime CreatedAt);

public interface IIdempotencyService
{
    bool TryGet(string key, out IdempotencyEntry? entry);
    void Store(string key, IdempotencyEntry entry);
}

public class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _store = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(10);

    public bool TryGet(string key, out IdempotencyEntry? entry)
    {
        if (_store.TryGetValue(key, out var existing))
        {
            if (Helpers.DateTimeHelper.UtcNow - existing.CreatedAt <= _ttl)
            {
                entry = existing;
                return true;
            }

            _store.TryRemove(key, out _);
        }

        entry = null;
        return false;
    }

    public void Store(string key, IdempotencyEntry entry)
    {
        _store[key] = entry;
    }
}

