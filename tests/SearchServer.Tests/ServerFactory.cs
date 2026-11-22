using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SearchEngine.Llm;

namespace SearchServer.Tests;

public sealed class FakeOllamaHealth : IOllamaHealth
{
    public bool Available { get; set; }

    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(Available);
}

public sealed class ServerFactory : WebApplicationFactory<Program>
{
    public FakeOllamaHealth FakeOllama { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.ConfigureServices(s => s.AddSingleton<IOllamaHealth>(FakeOllama));
}
