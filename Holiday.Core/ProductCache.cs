using Microsoft.Extensions.Caching.Memory;

namespace Holiday.Core;

public sealed class ProductCache(
    FakeDatabase db, IMemoryCache memory, bool enabled)
{
    public async Task<Product> GetAsync(int id, CancellationToken ct)
    {
        if (!enabled)
            return await db.GetProductAsync(id, ct);

        if (memory.TryGetValue(id, out Product? hit))
            return hit!;

        var product = await db.GetProductAsync(id, ct);
        memory.Set(id, product, TimeSpan.FromMinutes(10));
        return product;
    }

    // Fill the cache before the first customer arrives.
    public Task WarmUpAsync(int count) =>
        Parallel.ForEachAsync(
            Enumerable.Range(0, count),
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (id, ct) => await GetAsync(id, ct));
}
