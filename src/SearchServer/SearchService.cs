using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SearchContracts;
using SearchEngine;
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
public sealed class SearchService(EngineState state, IQueryExpander expander, [FromKeyedServices(Defaults.SearchPoolKey)] WorkerPool pool)
{
    /// <summary>Fails fast with no index, and skips the LLM call for queries with no searchable terms.</summary>
    public async Task<ExpansionResult> ExpandAsync(string query, CancellationToken ct)
    {
        if (state.Current is null)
        {
            throw new IndexNotBuiltException();
        }

        return Tokenizer.Tokenize(query).Count == 0 ? new ExpansionResult([], null) : await expander.ExpandAsync(query, ct);
    }

    public async Task<KeywordResult> KeywordSearchAsync(string query, IReadOnlyList<string> expanded, int topK)
    {
        var index = state.Current ?? throw new IndexNotBuiltException();
        var started = Stopwatch.GetTimestamp();
        var hits = await pool.Submit(() => Bm25Searcher.Search(index, Tokenizer.Tokenize(query), expanded, topK));
        return new KeywordResult(hits, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
}
