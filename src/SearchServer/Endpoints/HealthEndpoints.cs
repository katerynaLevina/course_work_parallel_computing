using SearchContracts;
using SearchEngine;
using SearchEngine.Llm;

namespace SearchServer.Endpoints;

public static class HealthEndpoints
{
    public static void Map(WebApplication app) =>
        app.MapGet(ApiRoutes.Health, async (IOllamaHealth ollama, CancellationToken ct) =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Defaults.HealthCheckTimeout);
            return new HealthResponse(ApiRoutes.HealthStatusOk, await ollama.IsAvailableAsync(cts.Token));
        });
}
