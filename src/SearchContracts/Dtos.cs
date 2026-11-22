namespace SearchContracts;

public record HealthResponse(string Status, bool Ollama);

public record IndexRequest(int? Threads, string? LockMode, int? Segments);

public record IndexResponse(string Status, int DocumentsIndexed, long TimeMs, int ThreadsUsed, string LockMode);

public record StatsResponse(int Documents, long IndexBuildTimeMs, int ThreadsUsed, int Terms, long Postings, string LockMode);

public record ErrorResponse(string Error);
