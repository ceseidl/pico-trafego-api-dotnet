[English](README.md) | Português

# Pico de tráfego nas festas: como preparar sua API .NET antes da data

Uma API pequena de loja (`GET /products/{id}`, `POST /orders`) rodada
duas vezes com a mesma carga: como foi escrita ("plain") e preparada
para o pico ("ready": cache aquecido antes do pico, descarte de carga
com 503 e `Retry-After`, timeout na chamada lenta de recomendações e
fila de pedidos com limite). O gerador de carga roda no mesmo processo:
os números servem para comparar numa máquina, não para medir o seu
servidor.

Artigo: https://ceseidl.com.br/artigos/pico-trafego-api-dotnet/

## Estrutura

- `Holiday.Core`: a API, os dois perfis, o cache, a fila e o gerador de carga.
- `Holiday.Tests`: 3 testes xUnit.
- `LoadTest`: `peak` e `ramp`, cada um com `plain` ou `ready`.
- `Gotchas`: três armadilhas com "before" e "after": `cold`, `queue`, `timeout`.
- `outputs`: as saídas reais usadas no artigo.

## Rodar

```bash
dotnet test
dotnet run -c Release --project LoadTest -- peak plain
dotnet run -c Release --project LoadTest -- peak ready
dotnet run -c Release --project Gotchas -- cold before
```
