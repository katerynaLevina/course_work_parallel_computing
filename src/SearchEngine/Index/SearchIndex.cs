namespace SearchEngine.Index;

/// <summary>
/// A finished, frozen index. Nothing changes after construction, so readers need no locking;
/// a rebuilt index is swapped in as a whole by replacing the reference to it.
/// </summary>
public sealed class SearchIndex
{
    private readonly Dictionary<string, DocInfo> _byDocId;

    public SearchIndex(ConcurrentTermIndex terms, DocInfo[] docs, double avgDocLength, long buildTimeMs, int threadsUsed)
    {
        Terms = terms;
        Docs = docs;
        AvgDocLength = avgDocLength;
        BuildTimeMs = buildTimeMs;
        ThreadsUsed = threadsUsed;
        _byDocId = docs.ToDictionary(d => d.DocId);
    }

    public ConcurrentTermIndex Terms { get; }

    /// <summary>Position in this array is the numeric doc id stored in postings.</summary>
    public DocInfo[] Docs { get; }

    public double AvgDocLength { get; }

    public long BuildTimeMs { get; }

    public int ThreadsUsed { get; }

    public DocInfo? FindDoc(string docId) => _byDocId.GetValueOrDefault(docId);
}
