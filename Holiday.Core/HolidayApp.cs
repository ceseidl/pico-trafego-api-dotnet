using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace Holiday.Core;

public sealed class HolidayApp : IAsyncDisposable
{
    private readonly WebApplication _app;

    private HolidayApp(WebApplication app) => _app = app;

    public string Url => _app.Urls.First();
    public FakeDatabase Db =>
        _app.Services.GetRequiredService<FakeDatabase>();
    public Recommendations Recs =>
        _app.Services.GetRequiredService<Recommendations>();
    public OrderQueue Orders =>
        _app.Services.GetRequiredService<OrderQueue>();
    public Stats Stats => _app.Services.GetRequiredService<Stats>();

    public static async Task<HolidayApp> StartAsync(Profile profile)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var s = builder.Services;
        s.AddSingleton(profile);
        s.AddSingleton(new FakeDatabase(profile.QueryMs));
        s.AddSingleton(new Recommendations(profile.RecsMs));
        s.AddSingleton(new OrderQueue(profile.OrderQueueSize));
        s.AddSingleton<Stats>();
        s.AddMemoryCache();
        s.AddSingleton(sp => new ProductCache(
            sp.GetRequiredService<FakeDatabase>(),
            sp.GetRequiredService<IMemoryCache>(), profile.Cache));
        s.AddHostedService<OrderProcessor>();

        if (profile.MaxConcurrent is { } max)
            s.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = 503;
                o.OnRejected = (ctx, _) =>
                {
                    ctx.HttpContext.Response.Headers.RetryAfter = "1";
                    return ValueTask.CompletedTask;
                };
                o.AddConcurrencyLimiter("browse", c =>
                {
                    c.PermitLimit = max;
                    c.QueueLimit = profile.ShedQueue;
                    c.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                });
            });

        var app = builder.Build();
        app.Use(async (ctx, next) =>
        {
            var stats = ctx.RequestServices.GetRequiredService<Stats>();
            stats.Enter();
            try { await next(); } finally { stats.Leave(); }
        });
        if (profile.MaxConcurrent is not null)
            app.UseRateLimiter();
        app.MapHoliday(profile);

        // The instance only starts listening after the cache is warm.
        if (profile.WarmUp)
            await app.Services.GetRequiredService<ProductCache>()
                .WarmUpAsync(Catalog.BestSellers);

        await app.StartAsync();
        return new HolidayApp(app);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
