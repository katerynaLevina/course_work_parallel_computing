using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SearchEngine.Llm;

namespace SearchServer.Tests;

public sealed class FakeOllamaHealth : IOllamaHealth
{
    public bool Available { get; set; }

    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(Available);
}

public sealed class FakeExpander : IQueryExpander
{
    private int _calls;

    public IReadOnlyList<string> Terms { get; set; } = [];

    public TimeSpan Delay { get; set; }

    /// <summary>Completed when the first expansion begins.</summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completed when a delayed expansion is cancelled.</summary>
    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Calls => Volatile.Read(ref _calls);

    public async Task<ExpansionResult> ExpandAsync(string query, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        Started.TrySetResult();
        if (Delay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(Delay, ct);
            }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult();
                throw;
            }
        }

        return new ExpansionResult(Terms, null);
    }
}

public sealed class ServerFactory : WebApplicationFactory<Program>
{
    public const int CorpusSize = 20;

    public ServerFactory()
    {
        DataDir = Directory.CreateTempSubdirectory("search-corpus").FullName;
        for (var i = 0; i < CorpusSize; i++)
        {
            File.WriteAllText(Path.Combine(DataDir, $"doc{i:D2}.txt"), $"Title {i}\nalpha beta gamma term{i}");
        }
    }

    public FakeOllamaHealth FakeOllama { get; } = new();

    public FakeExpander Expander { get; } = new();

    /// <summary>Corpus directory the server indexes; set before the first request to point it elsewhere.</summary>
    public string DataDir { get; set; }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.ConfigureServices(s =>
        {
            s.AddSingleton<IOllamaHealth>(FakeOllama);
            s.AddSingleton<IQueryExpander>(Expander);
            s.AddSingleton(ServerOptions.FromEnvironment() with { DataDir = DataDir });
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(DataDir))
        {
            Directory.Delete(DataDir, recursive: true);
        }
    }
}
