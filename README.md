English | [Português](README.pt-BR.md)

# Holiday traffic peak: preparing a .NET API before the date

A small shop API (`GET /products/{id}`, `POST /orders`) run twice under
the same load: once as written ("plain") and once prepared for the peak
("ready": cache warmed before the peak, load shedding with 503 and
`Retry-After`, a timeout on the slow recommendations call and a
bounded order queue). The load generator runs in the same process, so
the numbers are comparable on one machine, not a benchmark of your
server.

Article: https://ceseidl.com.br/en/artigos/pico-trafego-api-dotnet/

## Layout

- `Holiday.Core`: the API, the two profiles, the cache, the order queue
  and the load runner.
- `Holiday.Tests`: 3 xUnit tests (warm-up fills the cache first, slow
  recommendations do not slow the page, a full queue answers 503 with
  `Retry-After`).
- `LoadTest`: `peak` (600 requests/s for 8 s) and `ramp`, each with
  `plain` or `ready`.
- `Gotchas`: three traps with "before" and "after": cold cache on a new
  instance, unbounded order queue, missing deadline.
- `outputs`: the real outputs used in the article (12 cores, .NET 10.0.13).

## Run

```bash
dotnet test
dotnet run -c Release --project LoadTest -- peak plain
dotnet run -c Release --project LoadTest -- peak ready
dotnet run -c Release --project LoadTest -- ramp ready
dotnet run -c Release --project Gotchas -- cold before
dotnet run -c Release --project Gotchas -- cold after
```

Other traps: `queue before|after`, `timeout before|after`.
