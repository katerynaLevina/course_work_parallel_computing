using SearchEngine.Concurrency;

namespace SearchEngine.Index;

/// <summary>
/// Inverted index term -> PostingList, written by many threads at once.
/// The hash map is split into segments; each segment is a small hash table with its own lock.
/// Two threads block each other only when their terms land in the same segment, so more
/// segments means less contention. Global mode uses a single segment (one lock for everything)
/// and is the baseline the striped mode is benchmarked against.
/// </summary>
public sealed class ConcurrentTermIndex
{
    private readonly Segment[] _segments;

    public ConcurrentTermIndex(LockMode mode, int segments = Defaults.IndexSegments)
    {
        if (segments < 1 || (segments & (segments - 1)) != 0)
        {
            throw new ArgumentException("Segment count must be a power of two.", nameof(segments));
        }

        Mode = mode;
        _segments = new Segment[mode == LockMode.Global ? 1 : segments];
        for (var i = 0; i < _segments.Length; i++)
        {
            _segments[i] = new Segment();
        }
    }

    public LockMode Mode { get; }

    public int SegmentCount => _segments.Length;

    /// <summary>Valid only after Freeze().</summary>
    public int TermCount => _segments.Sum(s => s.Count);

    /// <summary>Valid only after Freeze().</summary>
    public long PostingCount => Entries().Sum(e => (long)e.List.Count);

    public void Add(string term, int docId, int tf)
    {
        var hash = HashOf(term);
        var segment = SegmentFor(hash);

        // Lookup, optional insert, optional resize and the posting append all happen under this
        // segment's lock, so no other thread can touch this segment's buckets or lists meanwhile.
        lock (segment)
        {
            segment.GetOrAdd(term, hash).Add(docId, tf);
        }
    }

    /// <summary>
    /// Lock-free read. Only valid after Freeze(): nobody writes any more, so the structure is
    /// immutable and any number of threads can read it without synchronization.
    /// </summary>
    public PostingList? Get(string term)
    {
        var hash = HashOf(term);
        return SegmentFor(hash).Find(term, hash);
    }

    /// <summary>
    /// Sorts every posting list by doc id. With a pool, one sort task per segment runs in parallel
    /// (segments are independent). Call it from outside the pool, after all Add calls have finished.
    /// </summary>
    public void Freeze(WorkerPool? pool = null)
    {
        if (pool is null)
        {
            foreach (var segment in _segments)
            {
                segment.SortAll();
            }

            return;
        }

        Task.WaitAll(_segments.Select(segment => pool.Submit(segment.SortAll)).ToArray());
    }

    /// <summary>Valid only after Freeze(): walks the buckets without locking.</summary>
    public IEnumerable<(string Term, PostingList List)> Entries()
    {
        foreach (var segment in _segments)
        {
            foreach (var entry in segment.Items())
            {
                yield return (entry.Key, entry.List);
            }
        }
    }

    // Non-negative hash. Segment uses the upper bits and the bucket uses the low bits,
    // so the two choices are independent and terms spread evenly inside each segment.
    private static int HashOf(string term) => term.GetHashCode(StringComparison.Ordinal) & int.MaxValue;

    private Segment SegmentFor(int hash) => _segments[(hash >>> Defaults.SegmentHashShift) & (_segments.Length - 1)];

    private sealed class Entry(string key, int hash, Entry? next)
    {
        public readonly string Key = key;
        public readonly int Hash = hash;
        public readonly PostingList List = new();
        public Entry? Next = next;
    }

    /// <summary>One chained hash table. Mutated only under lock(this) while indexing.</summary>
    private sealed class Segment
    {
        private Entry?[] _buckets = new Entry?[Defaults.SegmentInitialBuckets];

        public int Count { get; private set; }

        public PostingList GetOrAdd(string term, int hash)
        {
            var found = Find(term, hash);
            if (found is not null)
            {
                return found;
            }

            // Resize happens here, under the same segment lock that guards the buckets: only this
            // segment's table is rebuilt, other segments keep working, and no one else can see a half-moved table.
            if (Count + 1 > _buckets.Length * Defaults.IndexLoadFactor)
            {
                Resize();
            }

            var index = hash & (_buckets.Length - 1);
            var entry = new Entry(term, hash, _buckets[index]);
            _buckets[index] = entry;
            Count++;
            return entry.List;
        }

        public PostingList? Find(string term, int hash)
        {
            for (var e = _buckets[hash & (_buckets.Length - 1)]; e is not null; e = e.Next)
            {
                if (e.Hash == hash && e.Key == term)
                {
                    return e.List;
                }
            }

            return null;
        }

        // Lock-free reads are safe once the frozen index is handed to readers through a synchronizing
        // handoff (Task completion / WaitAll, then publishing the index reference); the lock here only
        // keeps the sort from overlapping any straggling Add.
        public void SortAll()
        {
            lock (this)
            {
                foreach (var entry in Items())
                {
                    entry.List.SortByDocId();
                }
            }
        }

        public IEnumerable<Entry> Items()
        {
            foreach (var head in _buckets)
            {
                for (var e = head; e is not null; e = e.Next)
                {
                    yield return e;
                }
            }
        }

        private void Resize()
        {
            var bigger = new Entry?[_buckets.Length * 2];
            foreach (var head in _buckets)
            {
                var e = head;
                while (e is not null)
                {
                    var next = e.Next;
                    var index = e.Hash & (bigger.Length - 1);
                    e.Next = bigger[index];
                    bigger[index] = e;
                    e = next;
                }
            }

            _buckets = bigger;
        }
    }
}
