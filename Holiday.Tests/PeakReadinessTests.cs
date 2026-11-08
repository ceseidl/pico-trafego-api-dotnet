using System.Diagnostics;
using System.Net;
using Holiday.Core;

namespace Holiday.Tests;

public class PeakReadinessTests
{
    [Fact]
    public async Task Warm_up_fills_cache_first()
    {
        await using var app = await HolidayApp.StartAsync(Profile.Ready);
        Assert.True(app.Db.Reads >= Catalog.BestSellers);
        var readsBefore = app.Db.Reads;

        using var http = new HttpClient { BaseAddress = new Uri(app.Url) };
        var response = await http.GetAsync("/products/7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(readsBefore, app.Db.Reads);   // served from the cache
    }

    [Fact]
    public async Task Slow_recs_do_not_slow_the_page()
    {
        await using var app = await HolidayApp.StartAsync(Profile.Ready);
        app.Recs.ExtraMs = 3000;   // the other service is struggling

        using var http = new HttpClient { BaseAddress = new Uri(app.Url) };
        var clock = Stopwatch.StartNew();
        var response = await http.GetAsync("/products/7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(clock.ElapsedMilliseconds < 1000,
            $"took {clock.ElapsedMilliseconds} ms");
        Assert.Contains("recommendations",
            response.Headers.GetValues("X-Degraded"));
    }

    [Fact]
    public async Task Full_queue_says_503_with_Retry_After()
    {
        // One slow worker, so the queue really fills up.
        var profile = Profile.Ready with { OrderWorkers = 1, OrderMs = 300 };
        await using var app = await HolidayApp.StartAsync(profile);
        using var http = new HttpClient { BaseAddress = new Uri(app.Url) };

        var calls = Enumerable.Range(0, 200)
            .Select(_ => http.PostAsync("/orders", null));
        var responses = await Task.WhenAll(calls);

        var accepted = responses.Count(
            r => r.StatusCode == HttpStatusCode.Accepted);
        var rejected = responses.Where(
            r => r.StatusCode == HttpStatusCode.ServiceUnavailable);

        Assert.InRange(accepted, 1, 60);
        Assert.NotEmpty(rejected);
        Assert.All(rejected, r => Assert.NotNull(r.Headers.RetryAfter));
    }
}
