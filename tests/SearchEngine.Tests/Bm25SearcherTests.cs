using SearchEngine.Index;
using SearchEngine.Search;

namespace SearchEngine.Tests;

public class Bm25SearcherTests
{
    private static async Task<SearchIndex> BuildAsync(TempCorpus corpus, params (string Name, string Text)[] files)
    {
        foreach (var (name, text) in files)
        {
            corpus.Write(name + ".txt", text);
        }

        return await IndexBuilder.BuildAsync(new IndexBuildOptions(corpus.Path));
    }

    [Fact]
    public async Task Doc_with_more_occurrences_ranks_higher()
    {
        using var corpus = new TempCorpus();
        var index = await BuildAsync(corpus, ("few", "ocean desert"), ("many", "ocean ocean ocean"), ("none", "forest"));

        var hits = Bm25Searcher.Search(index, ["ocean"], [], Defaults.DefaultTopK);

        Assert.Equal(["many", "few"], hits.Select(h => h.DocId).ToArray());
    }

    [Fact]
    public async Task Rare_term_outweighs_common_term()
    {
        using var corpus = new TempCorpus();
        var index = await BuildAsync(corpus,
            ("a", "ocean"), ("b", "ocean"), ("c", "ocean"), ("d", "ocean"), ("rare", "zebra"));

        var hits = Bm25Searcher.Search(index, ["ocean", "zebra"], [], Defaults.DefaultTopK);

        Assert.Equal("rare", hits[0].DocId);
    }

    [Fact]
    public async Task Expanded_terms_count_half()
    {
        using var corpus = new TempCorpus();
        var index = await BuildAsync(corpus, ("orig", "ocean"), ("exp", "sea"));

        var hits = Bm25Searcher.Search(index, ["ocean"], ["sea"], Defaults.DefaultTopK);

        Assert.Equal(["orig", "exp"], hits.Select(h => h.DocId).ToArray());
        Assert.Equal(Defaults.ExpandedTermWeight, hits[1].Score, Defaults.ScoreDecimals);
    }

    [Fact]
    public async Task Top_score_is_one_and_scores_descending()
    {
        using var corpus = new TempCorpus();
        var index = await BuildAsync(corpus, ("a", "ocean"), ("b", "ocean ocean"), ("c", "ocean wave wave wave"));

        var hits = Bm25Searcher.Search(index, ["ocean"], [], Defaults.DefaultTopK);

        Assert.Equal(1.0, hits[0].Score);
        Assert.Equal(hits.OrderByDescending(h => h.Score).ToArray(), hits);
        Assert.All(hits, h => Assert.Equal(Math.Round(h.Score, Defaults.ScoreDecimals), h.Score));
    }

    [Fact]
    public async Task Respects_top_k()
    {
        using var corpus = new TempCorpus();
        var index = await BuildAsync(corpus, ("a", "ocean"), ("b", "ocean"), ("c", "ocean"));

        var hits = Bm25Searcher.Search(index, ["ocean"], [], 2);

        Assert.Equal(["a", "b"], hits.Select(h => h.DocId).ToArray());
    }

    [Fact]
    public async Task Hit_carries_title_and_snippet()
    {
        using var corpus = new TempCorpus();
        var index = await BuildAsync(corpus, ("a", "My Title\nthe ocean is wide"));

        var hit = Assert.Single(Bm25Searcher.Search(index, ["ocean"], [], Defaults.DefaultTopK));

        Assert.Equal("My Title", hit.Title);
        Assert.Contains("ocean", hit.Snippet);
    }

    [Fact]
    public async Task Empty_terms_return_empty()
    {
        using var corpus = new TempCorpus();
        var index = await BuildAsync(corpus, ("a", "ocean"));

        Assert.Empty(Bm25Searcher.Search(index, [], [], Defaults.DefaultTopK));
    }

    [Fact]
    public async Task Unknown_terms_return_empty()
    {
        using var corpus = new TempCorpus();
        var index = await BuildAsync(corpus, ("a", "ocean"));

        Assert.Empty(Bm25Searcher.Search(index, ["volcano"], ["lava"], Defaults.DefaultTopK));
    }
}
