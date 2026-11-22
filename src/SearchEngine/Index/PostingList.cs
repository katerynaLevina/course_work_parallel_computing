namespace SearchEngine.Index;

/// <summary>
/// Growable array of postings. Not thread-safe by itself: while indexing, the owning segment's lock
/// guards every Add; after Freeze it is only read.
/// </summary>
public sealed class PostingList
{
    private Posting[] _items = new Posting[Defaults.PostingListInitialCapacity];

    public int Count { get; private set; }

    public void Add(int docId, int tf)
    {
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * 2);
        }

        _items[Count++] = new Posting(docId, tf);
    }

    public ReadOnlySpan<Posting> AsSpan() => _items.AsSpan(0, Count);

    public void SortByDocId() => _items.AsSpan(0, Count).Sort((a, b) => a.DocId.CompareTo(b.DocId));
}
