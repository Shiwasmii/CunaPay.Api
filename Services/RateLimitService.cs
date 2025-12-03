using System.Collections.Concurrent;

namespace CunaPay.Api.Services;

public interface IRateLimitService
{
    bool TryConsume(string key, int maxRequests, TimeSpan window);
}

public class RateLimitService : IRateLimitService
{
    private class Counter
    {
        public int Count;
        public DateTime WindowStart;
    }

    private readonly ConcurrentDictionary<string, Counter> _counters = new();

    public bool TryConsume(string key, int maxRequests, TimeSpan window)
    {
        var now = Helpers.DateTimeHelper.UtcNow;
        var counter = _counters.GetOrAdd(key, _ => new Counter { Count = 0, WindowStart = now });

        lock (counter)
        {
            if (now - counter.WindowStart > window)
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }

            if (counter.Count >= maxRequests)
            {
                return false;
            }

            counter.Count++;
            return true;
        }
    }
}

