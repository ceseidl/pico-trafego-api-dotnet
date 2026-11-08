using System.Diagnostics;

namespace Holiday.Core;

public sealed class LoadResult
{
    public int Sent, Ok, Shed, Timeouts, Other, Degraded;
    public double P50, P95, P99;

    public string Counts =>
        $"sent {Sent}  ok {Ok}  shed {Shed}  timeout {Timeouts}" +
        (Other > 0 ? $"  failed {Other}" : "");

    public string Latency =>
        $"p50 {P50:F0} ms  p95 {P95:F0} ms  p99 {P99:F0} ms";
}

// Our own small load generator. Open loop: requests keep arriving at a
// fixed rate even when the server is slow, like real customers do.
public static class LoadRunner
{
    public static async Task<LoadResult> RunAsync(
        string baseUrl, int perSecond, TimeSpan duration,
        Func<Random, HttpRequestMessage> next, TimeSpan clientTimeout)
    {
        using var http = new HttpClient(new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 5000
        })
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = Timeout.InfiniteTimeSpan
        };

        var result = new LoadResult();
        var millis = new List<double>();
        var tasks = new List<Task>();
        var rng = new Random(42);
        var clock = Stopwatch.StartNew();
        var sent = 0;

        while (clock.Elapsed < duration)
        {
            var due = (int)(clock.Elapsed.TotalSeconds * perSecond) - sent;
            for (; due > 0; due--, sent++)
                tasks.Add(OneAsync(next(rng)));
            await Task.Delay(1);
        }

        await Task.WhenAll(tasks);
        millis.Sort();
        result.Sent = sent;
        result.P50 = Percentile(millis, 0.50);
        result.P95 = Percentile(millis, 0.95);
        result.P99 = Percentile(millis, 0.99);
        return result;

        async Task OneAsync(HttpRequestMessage request)
        {
            var start = Stopwatch.GetTimestamp();
            using var cts = new CancellationTokenSource(clientTimeout);
            int kind;   // 0 ok, 1 shed, 2 timeout, 3 other
            var degraded = false;
            try
            {
                using var response = await http.SendAsync(request, cts.Token);
                degraded = response.Headers.Contains("X-Degraded");
                kind = (int)response.StatusCode switch
                {
                    200 or 202 => 0,
                    503 or 429 => 1,
                    _ => 3
                };
            }
            catch (OperationCanceledException) { kind = 2; }
            catch (HttpRequestException) { kind = 3; }

            var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            lock (result)
            {
                millis.Add(ms);
                if (degraded) result.Degraded++;
                if (kind == 0) result.Ok++;
                else if (kind == 1) result.Shed++;
                else if (kind == 2) result.Timeouts++;
                else result.Other++;
            }
        }
    }

    private static double Percentile(List<double> sorted, double p) =>
        sorted.Count == 0 ? 0
            : sorted[(int)Math.Min(sorted.Count - 1, sorted.Count * p)];

    public static HttpRequestMessage Product(Random rng) =>
        new(HttpMethod.Get, $"/products/{Catalog.RandomId(rng)}");

    public static HttpRequestMessage Order(Random rng) =>
        new(HttpMethod.Post, "/orders");
}
