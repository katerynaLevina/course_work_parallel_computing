using SearchEngine.Index;

namespace SearchEngine.Tests;

public class IndexBuilderTests
{
    private const int GeneratedDocs = 500;
    private const int WordsPerDoc = 40;
    private const int Vocabulary = 300;
    private const int Seed = 7;
    private const int ManyThreads = 8;
    private const int CustomThreads = 3;

    [Fact]
    public async Task Indexes_all_files_recursively()
    {
        using var corpus = new TempCorpus();
        corpus.Write("a.txt", "alpha");
        corpus.Write("sub/b.txt", "beta");
        corpus.Write("sub/deep/c.txt", "gamma");

        var index = await IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path));

        Assert.Equal(3, index.Docs.Length);
        Assert.Equal(["a", "sub/b", "sub/deep/c"], index.Docs.Select(d => d.DocId).ToArray());
        Assert.NotNull(index.FindDoc("sub/deep/c"));
        Assert.Null(index.FindDoc("missing"));
    }

    [Fact]
    public async Task Term_frequencies_are_correct()
    {
        using var corpus = new TempCorpus();
        corpus.Write("a.txt", "ocean ocean wave");

        var index = await IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path));

        var postings = index.Terms.Get("ocean")!.AsSpan();
        Assert.Equal(1, postings.Length);
        Assert.Equal(new Posting(0, 2), postings[0]);
        Assert.Equal(3, index.Docs[0].Length);
    }

    [Fact]
    public async Task Title_is_first_line()
    {
        using var corpus = new TempCorpus();
        corpus.Write("a.txt", "\n  The Title  \nbody text");
        corpus.Write("b.txt", "   \n\n");
        corpus.Write("c.txt", new string('x', Defaults.TitleMaxLength + 50));

        var index = await IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path));

        Assert.Equal("The Title", index.FindDoc("a")!.Title);
        Assert.Equal("b.txt", index.FindDoc("b")!.Title);
        Assert.Equal(Defaults.TitleMaxLength, index.FindDoc("c")!.Title.Length);
    }

    [Fact]
    public async Task Same_result_for_1_and_8_threads()
    {
        using var corpus = new TempCorpus();
        var random = new Random(Seed);
        for (var i = 0; i < GeneratedDocs; i++)
        {
            var words = Enumerable.Range(0, WordsPerDoc).Select(_ => $"word{random.Next(Vocabulary)}");
            corpus.Write($"d{i % 5}/doc{i}.txt", string.Join(' ', words));
        }

        var one = await IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path, Threads: 1));
        var many = await IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path, Threads: ManyThreads));

        Assert.Equal(one.Terms.TermCount, many.Terms.TermCount);
        Assert.Equal(one.Terms.PostingCount, many.Terms.PostingCount);
        foreach (var (term, list) in one.Terms.Entries())
        {
            Assert.Equal(list.AsSpan().ToArray(), many.Terms.Get(term)!.AsSpan().ToArray());
        }
    }

    [Fact]
    public async Task Empty_and_invalid_utf8_files_do_not_crash()
    {
        using var corpus = new TempCorpus();
        corpus.Write("empty.txt", "");
        File.WriteAllBytes(Path.Combine(corpus.Path, "bad.txt"), [0xFF, 0xFE, 0x41, 0x62, 0x63]);

        var index = await IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path));

        Assert.Equal(0, index.FindDoc("empty")!.Length);
        Assert.True(index.FindDoc("bad")!.Length > 0);
    }

    [Fact]
    public async Task Missing_directory_throws()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => IndexBuilder.BuildAsync(new IndexBuildOptions(missing)));
    }

    [Fact]
    public async Task Respects_glob()
    {
        using var corpus = new TempCorpus();
        corpus.Write("a.txt", "alpha");
        corpus.Write("b.md", "beta");

        var index = await IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path));

        Assert.Single(index.Docs);
    }

    [Fact]
    public async Task Records_threads_and_time()
    {
        using var corpus = new TempCorpus();
        corpus.Write("a.txt", "alpha");

        var index = await IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path, Threads: CustomThreads));

        Assert.Equal(CustomThreads, index.ThreadsUsed);
        Assert.True(index.BuildTimeMs >= 0);
    }

    [Fact]
    public async Task Cancelled_token_throws_cancellation()
    {
        using var corpus = new TempCorpus();
        corpus.Write("a.txt", "alpha");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path), cts.Token));
    }

    [Fact]
    public async Task Unreadable_file_surfaces_original_exception()
    {
        using var corpus = new TempCorpus();
        corpus.Write("a.txt", "alpha");
        // A link to a missing file is listed by the enumeration but cannot be read.
        File.CreateSymbolicLink(Path.Combine(corpus.Path, "broken.txt"), Path.Combine(corpus.Path, "nowhere"));

        await Assert.ThrowsAsync<FileNotFoundException>(() => IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path)));
    }

    [Fact]
    public async Task Batch_size_below_one_is_rejected()
    {
        using var corpus = new TempCorpus();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path, BatchSize: 0)));
    }
}
