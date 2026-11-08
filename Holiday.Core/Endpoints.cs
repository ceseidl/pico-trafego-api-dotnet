using System.Diagnostics;
using Microsoft.AspNetCore.Builder;

namespace Holiday.Core;

public static class Endpoints
{
    public static void MapHoliday(this WebApplication app, Profile profile)
    {
        var product = app.MapGet("/products/{id:int}", async (
            int id, HttpContext http,
            ProductCache cache, Recommendations recs) =>
        {
            var ct = http.RequestAborted;
            var item = await cache.GetAsync(id, ct);
            string[] related;

            if (profile.RecsTimeout is { } limit)
            {
                using var deadline =
                    CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(limit);
                try
                {
                    related = await recs.GetAsync(id, deadline.Token);
                }
                catch (OperationCanceledException)
                    when (!ct.IsCancellationRequested)
                {
                    // Degrade: no suggestions, but the page still works.
                    related = [];
                    http.Response.Headers["X-Degraded"] = "recommendations";
                }
            }
            else
            {
                related = await recs.GetAsync(id, ct);
            }

            return Results.Ok(new { item, related });
        });

        if (profile.MaxConcurrent is not null)
            product.RequireRateLimiting("browse");

        app.MapPost("/orders", (
            HttpContext http, OrderQueue queue, Stats stats) =>
        {
            var order = new Order(
                stats.NextOrderId(), Stopwatch.GetTimestamp());
            if (!queue.TryEnqueue(order))
            {
                http.Response.Headers.RetryAfter = "2";
                return Results.StatusCode(503);
            }

            return Results.Accepted($"/orders/{order.Id}");
        });
    }
}
