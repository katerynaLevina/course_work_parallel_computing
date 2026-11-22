namespace SearchEngine.Index;

/// <summary>Metadata of one indexed document. DocId is the path relative to the data dir, without extension.</summary>
public sealed record DocInfo(string DocId, string Path, string Title, int Length);
