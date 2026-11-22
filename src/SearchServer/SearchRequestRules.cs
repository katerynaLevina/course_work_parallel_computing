using SearchContracts;
using SearchEngine;
using SearchEngine.Search;

namespace SearchServer;

/// <summary>Request rules shared by the REST and WebSocket search endpoints.</summary>
public static class SearchRequestRules
{
    /// <summary>The error message for an unusable query, or null when it is fine.</summary>
    public static string? ValidateQuery(string? query) => query switch
    {
        null => ApiRoutes.ErrorQueryRequired,
        { Length: > ApiRoutes.MaxQueryLength } => ApiRoutes.ErrorQueryTooLong,
        _ => null,
    };

    public static int ClampTopK(int? topK) => Math.Clamp(topK ?? Defaults.DefaultTopK, ApiRoutes.MinTopK, ApiRoutes.MaxTopK);

    public static SearchResultDto ToDto(SearchHit h) => new(h.DocId, h.Score, h.Snippet, h.Title);
}
