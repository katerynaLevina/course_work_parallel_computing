using SearchEngine.Index;

namespace SearchEngine.Search;

public static class Bm25Searcher
{
    public static IReadOnlyList<SearchHit> Search(
        SearchIndex index, IReadOnlyList<string> originalTerms, IReadOnlyList<string> expandedTerms, int topK)
    {
        var originals = originalTerms.ToHashSet();
        var expanded = expandedTerms.Where(t => !originals.Contains(t)).ToHashSet();
        var docCount = index.Docs.Length;
        if (docCount == 0 || originals.Count + expanded.Count == 0 || topK <= 0)
        {
            return [];
        }

        var scores = new double[docCount];
        AddScores(index, originals, Defaults.OriginalTermWeight, scores);
        AddScores(index, expanded, Defaults.ExpandedTermWeight, scores);

        var top = TopDocs(scores, topK);
        if (top.Count == 0)
        {
            return [];
        }

        var allTerms = originals.Concat(expanded).ToList();
        var best = top[0].Score;
        return top.Select(t =>
        {
            var doc = index.Docs[t.DocId];
            return new SearchHit(doc.DocId, doc.Title, Math.Round(t.Score / best, Defaults.ScoreDecimals), SnippetOf(doc, allTerms));
        }).ToList();
    }

    /// <summary>The file is read at query time, so it may have been removed since indexing: then no snippet.</summary>
    private static string SnippetOf(DocInfo doc, List<string> terms)
    {
        try
        {
            return SnippetBuilder.Build(File.ReadAllText(doc.Path), terms);
        }
        catch (IOException)
        {
            return "";
        }
    }

    private static void AddScores(SearchIndex index, HashSet<string> terms, double weight, double[] scores)
    {
        var docCount = index.Docs.Length;
        foreach (var term in terms)
        {
            var list = index.Terms.Get(term);
            if (list is null)
            {
                continue;
            }

            var df = list.Count;
            var idf = Math.Log(1 + (docCount - df + 0.5) / (df + 0.5));
            foreach (var posting in list.AsSpan())
            {
                var tf = posting.Tf;
                var lengthNorm = 1 - Defaults.Bm25B + Defaults.Bm25B * index.Docs[posting.DocId].Length / index.AvgDocLength;
                scores[posting.DocId] += weight * idf * tf * (Defaults.Bm25K1 + 1) / (tf + Defaults.Bm25K1 * lengthNorm);
            }
        }
    }

    /// <summary>Best <paramref name="topK"/> docs by score, ties to the lower doc id, best first.</summary>
    private static List<(int DocId, double Score)> TopDocs(double[] scores, int topK)
    {
        var heap = new PriorityQueue<int, double>();
        for (var docId = 0; docId < scores.Length; docId++)
        {
            if (scores[docId] <= 0)
            {
                continue;
            }

            if (heap.Count < topK)
            {
                heap.Enqueue(docId, scores[docId]);
            }
            else if (heap.TryPeek(out _, out var worst) && scores[docId] > worst)
            {
                heap.DequeueEnqueue(docId, scores[docId]);
            }
        }

        var result = new List<(int DocId, double Score)>(heap.Count);
        while (heap.TryDequeue(out var docId, out var score))
        {
            result.Add((docId, score));
        }

        return result.OrderByDescending(r => r.Score).ThenBy(r => r.DocId).ToList();
    }
}
