using System.Runtime.InteropServices;
using System.Globalization;
using Holiday.Core;

// Usage: LoadTest peak plain|ready
//        LoadTest ramp plain|ready
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var scenario = args[0];
var profile = args[1] == "ready" ? Profile.Ready : Profile.Plain;
var timeout = TimeSpan.FromSeconds(3);

Console.WriteLine($"this machine: {Environment.ProcessorCount} cores, " +
                  RuntimeInformation.FrameworkDescription);
Console.WriteLine($"profile: {args[1]}");

await using var app = await HolidayApp.StartAsync(profile);

// A short, gentle run first, so JIT compilation does not count.
await LoadRunner.RunAsync(
    app.Url, 100, TimeSpan.FromSeconds(2), LoadRunner.Product, timeout);
var readsBefore = app.Db.Reads;

if (scenario == "peak")
{
    var result = await LoadRunner.RunAsync(
        app.Url, 600, TimeSpan.FromSeconds(8), LoadRunner.Product, timeout);
    Console.WriteLine("peak: 600 requests/s for 8 s");
    Console.WriteLine(result.Counts);
    Console.WriteLine(result.Latency);
    Console.WriteLine($"database reads: {app.Db.Reads - readsBefore}");
    Console.WriteLine($"max in flight: {app.Stats.MaxInFlight}");
}
else
{
    Console.WriteLine("step      p95      ok    shed timeout  failed");
    foreach (var rate in new[] { 200, 400, 800, 1600 })
    {
        var r = await LoadRunner.RunAsync(
            app.Url, rate, TimeSpan.FromSeconds(4), LoadRunner.Product,
            timeout);
        Console.WriteLine(
            $"{rate,4}/s {r.P95,6:F0} ms {r.Ok * 100 / r.Sent,5}% " +
            $"{r.Shed * 100 / r.Sent,5}% {r.Timeouts * 100 / r.Sent,6}% " +
            $"{r.Other * 100 / r.Sent,6}%");
    }
}
