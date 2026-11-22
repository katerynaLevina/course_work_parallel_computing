using System.Text.Json.Serialization;

namespace SearchContracts;

public record HealthResponse(string Status, bool Ollama);

public record IndexRequest(int? Threads, string? LockMode, int? Segments);

public record IndexResponse(string Status, int DocumentsIndexed, long TimeMs, int ThreadsUsed, string LockMode);

public record StatsResponse(int Documents, long IndexBuildTimeMs, int ThreadsUsed, int Terms, long Postings, string LockMode);

public record ErrorResponse(string Error);

public record SearchRequest(string? Query, string? Mode, bool? Expand, int? TopK);

public record SearchResultDto(string DocId, double Score, string Snippet, string Title);

public record SearchResponse(IReadOnlyList<SearchResultDto> Results, long TimeMs, IReadOnlyList<string> ExpandedTerms);

public record DocResponse(string DocId, string Title, string Text);

public record WsRequest(string? Query, int? TopK);

public record StageMessage(string Stage, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Data);

public record ExpansionStageData(
    string Original, IReadOnlyList<string> ExpandedTerms, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error);

public record KeywordStageData(IReadOnlyList<SearchResultDto> Results, long TimeMs);

public record ErrorStageData(string Message);
