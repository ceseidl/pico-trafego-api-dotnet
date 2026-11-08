using System.Globalization;
using Holiday.Core;

// Usage: Gotchas cold before|after
//        Gotchas queue before|after
//        Gotchas timeout before|after
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var trap = args[0];
var fixedVersion = args[1] == "after";

switch (trap)
{
    case "cold": await ColdCache(); break;
    case "queue": await Orders(); break;
    case "timeout": await Deadline(); break;
}

async Task ColdCache()
{
    // A new instance joins at the start of the peak.
    var profile = Profile.Plain with { Cache = true, WarmUp = fixedVersion };
    await using var app = await HolidayApp.StartAsync(profile);
    await HttpWarmUp(app);
    var before = app.Db.Reads;

    var result = await LoadRunner.RunAsync(
        app.Url, 1000, TimeSpan.FromSeconds(6),
        LoadRunner.Product, TimeSpan.FromSeconds(3));

    Console.WriteLine(fixedVersion ? "cache: warmed before the peak"
                                   : "cache: empty when the peak starts");
    Console.WriteLine(result.Counts);
    Console.WriteLine(result.Latency);
    Console.WriteLine($"database reads in the peak: {app.Db.Reads - before}");
}

async Task Orders()
{
    var profile = Profile.Plain with
    {
        OrderQueueSize = fixedVersion ? 100 : null
    };
    await using var app = await HolidayApp.StartAsync(profile);
    await HttpWarmUp(app);

    var result = await LoadRunner.RunAsync(
        app.Url, 300, TimeSpan.FromSeconds(5),
        LoadRunner.Order, TimeSpan.FromSeconds(3));
    var waiting = app.Orders.Depth;

    // Let the workers finish everything that was accepted.
    while (app.Stats.Processed < result.Ok)
        await Task.Delay(200);

    Console.WriteLine(fixedVersion ? "queue: bounded to 100 orders"
                                   : "queue: unbounded");
    Console.WriteLine($"accepted (202): {result.Ok}");
    Console.WriteLine($"rejected (503): {result.Shed}");
    Console.WriteLine($"waiting when the peak ended: {waiting}");
    var slowest = app.Stats.MaxWaitMs / 1000;
    Console.WriteLine($"slowest order waited: {slowest:F1} s");
}

async Task Deadline()
{
    var limit = TimeSpan.FromMilliseconds(150);
    var profile = Profile.Plain with
    {
        Cache = true, WarmUp = true,
        RecsTimeout = fixedVersion ? limit : null
    };
    await using var app = await HolidayApp.StartAsync(profile);
    await HttpWarmUp(app);
    app.Recs.ExtraMs = 3000;   // the recommendation service is struggling

    var result = await LoadRunner.RunAsync(
        app.Url, 100, TimeSpan.FromSeconds(5),
        LoadRunner.Product, TimeSpan.FromSeconds(10));

    Console.WriteLine(fixedVersion ? "recommendations: 150 ms deadline"
                                   : "recommendations: no deadline");
    Console.WriteLine(result.Counts);
    Console.WriteLine(result.Latency);
    Console.WriteLine($"served without suggestions: {result.Degraded}");
    Console.WriteLine($"max requests in flight: {app.Stats.MaxInFlight}");
}

// Warm the HTTP stack only (404s), never the product cache.
static async Task HttpWarmUp(HolidayApp app)
{
    using var http = new HttpClient { BaseAddress = new Uri(app.Url) };
    for (var i = 0; i < 50; i++)
        await http.GetAsync("/missing");
}
