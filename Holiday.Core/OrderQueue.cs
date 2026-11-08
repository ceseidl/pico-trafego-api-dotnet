using System.Threading.Channels;

namespace Holiday.Core;

public sealed class OrderQueue
{
    private readonly Channel<Order> _channel;

    public OrderQueue(int? size) =>
        _channel = size is { } n
            ? Channel.CreateBounded<Order>(n)
            : Channel.CreateUnbounded<Order>();

    public ChannelReader<Order> Reader => _channel.Reader;

    public int Depth => _channel.Reader.Count;

    // False means "full": the caller must say no, not wait.
    public bool TryEnqueue(Order order) => _channel.Writer.TryWrite(order);
}

public sealed class OrderProcessor(
    OrderQueue queue, Stats stats, Profile profile) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stop) =>
        Task.WhenAll(Enumerable.Range(0, profile.OrderWorkers)
            .Select(_ => Task.Run(() => WorkAsync(stop), stop)));

    private async Task WorkAsync(CancellationToken stop)
    {
        await foreach (var order in queue.Reader.ReadAllAsync(stop))
        {
            await Task.Delay(profile.OrderMs, stop);   // payment, stock...
            stats.Done(order);
        }
    }
}
