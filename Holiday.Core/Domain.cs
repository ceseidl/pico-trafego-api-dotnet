using System.Diagnostics;

namespace Holiday.Core;

public sealed record Product(int Id, string Name, decimal Price);

public sealed record Order(int Id, long EnqueuedAt);

public static class Catalog
{
    public const int Size = 500;
    public const int BestSellers = 200;   // last year's top sellers

    // Most shoppers look at the same few gifts.
    public static int RandomId(Random rng) =>
        (int)(Size * Math.Pow(rng.NextDouble(), 5));
}

// Simulated database: a slow query and a small connection pool.
public sealed class FakeDatabase(int queryMs, int connections = 8)
{
    private readonly SemaphoreSlim _pool = new(connections);
    private int _reads;

    public int Reads => Volatile.Read(ref _reads);

    public async Task<Product> GetProductAsync(int id, CancellationToken ct)
    {
        await _pool.WaitAsync(ct);
        try
        {
            Interlocked.Increment(ref _reads);
            await Task.Delay(queryMs, ct);
            return new Product(id, $"Gift #{id}", 10m + id % 90);
        }
        finally
        {
            _pool.Release();
        }
    }
}

// Simulated "you may also like" service. ExtraMs fakes an incident.
public sealed class Recommendations(int baseMs)
{
    public int ExtraMs { get; set; }

    public async Task<string[]> GetAsync(int id, CancellationToken ct)
    {
        await Task.Delay(baseMs + ExtraMs, ct);
        return [$"Gift #{id + 1}", $"Gift #{id + 2}"];
    }
}

public sealed class Stats
{
    private int _inFlight, _maxInFlight, _processed, _nextOrder;
    private long _maxWaitTicks;

    public int MaxInFlight => _maxInFlight;
    public int Processed => _processed;
    public double MaxWaitMs => _maxWaitTicks * 1000.0 / Stopwatch.Frequency;

    public int NextOrderId() => Interlocked.Increment(ref _nextOrder);

    public void Enter()
    {
        var now = Interlocked.Increment(ref _inFlight);
        int seen;
        do seen = _maxInFlight;
        while (now > seen && Interlocked.CompareExchange(
                   ref _maxInFlight, now, seen) != seen);
    }

    public void Leave() => Interlocked.Decrement(ref _inFlight);

    public void Done(Order order)
    {
        Interlocked.Increment(ref _processed);
        var waited = Stopwatch.GetTimestamp() - order.EnqueuedAt;
        long seen;
        do seen = Volatile.Read(ref _maxWaitTicks);
        while (waited > seen && Interlocked.CompareExchange(
                   ref _maxWaitTicks, waited, seen) != seen);
    }
}
