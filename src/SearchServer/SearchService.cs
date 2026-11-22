using Microsoft.Extensions.DependencyInjection;
using SearchContracts;
using SearchEngine.Concurrency;
using SearchEngine.Llm;
using SearchEngine.Search;
using SearchEngine.Text;

namespace SearchServer;

public sealed class IndexNotBuiltException() : Exception(ApiRoutes.ErrorIndexNotBuilt);

public record KeywordResult(IReadOnlyList<SearchHit> Hits, long TimeMs);

/// <summary>
/// Hybrid threading: Kestrel runs the request asynchronously, and the LLM expansion is awaited as plain
/// async HTTP on that request (it only waits on the network, so it must not occupy a worker thread).
/// The CPU-bound BM25 ranking runs on our own long-lived "search" <see cref="WorkerPool"/>.
/// </summary>
public sealed class SearchService(EngineState state, IQueryExpander expander, [FromKeyedServices(SearchService.PoolKey)] WorkerPool pool)
{
    public const string PoolKey = "search";

    public Task<ExpansionResult> ExpandAsync(string query, CancellationToken ct) => expander.ExpandAsync(query, ct);

    public async Task<KeywordResult> KeywordSearchAsync(string query, IReadOnlyList<string> expanded, int topK)
    {
        var index = state.Current ?? throw new IndexNotBuiltException();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var hits = await pool.Submit(() => Bm25Searcher.Search(index, Tokenizer.Tokenize(query), expanded, topK));
        return new KeywordResult(hits, (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
}
