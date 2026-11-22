using SearchEngine.Index;

namespace SearchEngine.Tests;

public class ConcurrentTermIndexTests
{
    private const int ManyTerms = 100_000;
    private const int WriterThreads = 8;
    private const int Docs = 20_000;
    private const int TermsPerDoc = 50;
    private const int Vocabulary = 2_000;
    private const int Seed = 12345;
    private const int HotThreads = 16;
    private const int HotAddsPerThread = 10_000;
    private const string HotTerm = "the";

    [Theory]
    [InlineData(LockMode.Striped)]
    [InlineData(LockMode.Global)]
    public void Add_and_get_single_thread(LockMode mode)
    {
        var index = new ConcurrentTermIndex(mode);
        index.Add("ocean", 1, 3);
        index.Add("ocean", 2, 1);
        index.Freeze();

        Assert.Equal([new Posting(1, 3), new Posting(2, 1)], index.Get("ocean")!.AsSpan().ToArray());
        Assert.Null(index.Get("x"));
    }

    [Fact]
    public void Freeze_sorts_postings_by_doc_id()
    {
        var index = new ConcurrentTermIndex(LockMode.Striped);
        index.Add("a", 5, 1);
        index.Add("a", 2, 1);
        index.Add("a", 9, 1);
        index.Add("a", 1, 1);
        index.Add("a", 7, 1);
        index.Freeze();

        Assert.Equal([1, 2, 5, 7, 9], index.Get("a")!.AsSpan().ToArray().Select(p => p.DocId));
    }

    [Fact]
    public void Freeze_with_pool_matches_sequential_freeze()
    {
        var sequential = BuildSequential(LockMode.Striped);
        var parallel = BuildSequential(LockMode.Striped, freeze: false);
        using (var pool = new SearchEngine.Concurrency.WorkerPool(4))
        {
            parallel.Freeze(pool);
        }

        AssertSameIndex(sequential, parallel);
    }

    [Fact]
    public void Grows_past_resize_threshold()
    {
        var index = new ConcurrentTermIndex(LockMode.Striped);
        for (var i = 0; i < ManyTerms; i++)
        {
            index.Add($"term{i}", i, 1);
        }

        index.Freeze();

        Assert.Equal(ManyTerms, index.TermCount);
        for (var i = 0; i < ManyTerms; i++)
        {
            Assert.NotNull(index.Get($"term{i}"));
        }
    }

    [Theory]
    [InlineData(LockMode.Striped)]
    [InlineData(LockMode.Global)]
    public void Parallel_inserts_equal_sequential(LockMode mode)
    {
        var sequential = BuildSequential(mode);
        var parallel = new ConcurrentTermIndex(mode);
        RunThreads(WriterThreads, t =>
        {
            for (var d = t; d < Docs; d += WriterThreads)
            {
                foreach (var term in TermsOf(d))
                {
                    parallel.Add(term, d, 1);
                }
            }
        });
        parallel.Freeze();

        AssertSameIndex(sequential, parallel);
    }

    [Fact]
    public void Hot_term_contention_loses_no_postings()
    {
        var index = new ConcurrentTermIndex(LockMode.Striped);
        RunThreads(HotThreads, t =>
        {
            for (var i = 0; i < HotAddsPerThread; i++)
            {
                index.Add(HotTerm, (t * HotAddsPerThread) + i, 1);
            }
        });
        index.Freeze();

        Assert.Equal(HotThreads * HotAddsPerThread, index.Get(HotTerm)!.Count);
    }

    [Fact]
    public void Global_mode_has_one_segment()
    {
        Assert.Equal(1, new ConcurrentTermIndex(LockMode.Global).SegmentCount);
    }

    [Fact]
    public void Rejects_non_power_of_two_segments()
    {
        Assert.Throws<ArgumentException>(() => new ConcurrentTermIndex(LockMode.Striped, 100));
    }

    // Deterministic terms per document, so the same data can be built on one or many threads.
    private static IEnumerable<string> TermsOf(int docId)
    {
        var random = new Random(Seed + docId);
        for (var i = 0; i < TermsPerDoc; i++)
        {
            yield return "w" + random.Next(Vocabulary);
        }
    }

    private static ConcurrentTermIndex BuildSequential(LockMode mode, bool freeze = true)
    {
        var index = new ConcurrentTermIndex(mode);
        for (var d = 0; d < Docs; d++)
        {
            foreach (var term in TermsOf(d))
            {
                index.Add(term, d, 1);
            }
        }

        if (freeze)
        {
            index.Freeze();
        }

        return index;
    }

    private static void AssertSameIndex(ConcurrentTermIndex expected, ConcurrentTermIndex actual)
    {
        Assert.Equal(expected.TermCount, actual.TermCount);
        Assert.Equal(expected.PostingCount, actual.PostingCount);
        foreach (var (term, list) in expected.Entries())
        {
            Assert.Equal(list.AsSpan().ToArray(), actual.Get(term)!.AsSpan().ToArray());
        }
    }

    private static void RunThreads(int count, Action<int> body)
    {
        var threads = Enumerable.Range(0, count)
            .Select(t => new Thread(() => body(t)) { IsBackground = true })
            .ToArray();
        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }
    }
}
