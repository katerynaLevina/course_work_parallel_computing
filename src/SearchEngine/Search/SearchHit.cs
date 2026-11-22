namespace SearchEngine.Search;

public sealed record SearchHit(string DocId, string Title, double Score, string Snippet);
