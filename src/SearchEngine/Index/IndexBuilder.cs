using System.Diagnostics;
using System.Text;
using SearchEngine.Concurrency;
using SearchEngine.Text;

namespace SearchEngine.Index;

public static class IndexBuilder
{
    public static async Task<SearchIndex> BuildAsync(IndexBuildOptions o, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(o.BatchSize, 1);
        if (!Directory.Exists(o.DataDir))
        {
            throw new DirectoryNotFoundException($"Data directory not found: {o.DataDir}");
        }

        ct.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();

        // Sequential: list and sort the files so a document's id (its array position) is the same on every run.
        var files = Directory.EnumerateFiles(o.DataDir, o.Glob, SearchOption.AllDirectories).ToList();
        files.Sort(StringComparer.Ordinal);

        // Doc ids are fixed up front, so each task knows which slot to write to and no task has to
        // coordinate with another to hand out ids. Slots are disjoint, so the array needs no lock.
        var docs = new DocInfo[files.Count];
        var terms = new ConcurrentTermIndex(o.Mode, o.Segments);

        using var pool = new WorkerPool(o.Threads);

        // Parallel: one pool job per batch of files (reading, tokenizing and counting are independent per file).
        // Only terms.Add touches shared state, and the index locks per segment.
        var batchSize = Math.Clamp(files.Count / (o.Threads * Defaults.BatchesPerThread), 1, o.BatchSize);
        var batches = new List<Task>();
        for (var first = 0; first < files.Count; first += batchSize)
        {
            var start = first;
            var end = Math.Min(first + batchSize, files.Count);
            batches.Add(pool.Submit(() =>
            {
                ct.ThrowIfCancellationRequested();
                for (var id = start; id < end; id++)
                {
                    docs[id] = IndexFile(files[id], id, o.DataDir, terms);
                }
            }));
        }

        await Task.WhenAll(batches);

        // Parallel again on the same pool: every segment sorts its posting lists independently.
        terms.Freeze(pool);

        var avgLength = docs.Length == 0 ? 0 : docs.Average(d => d.Length);
        return new SearchIndex(terms, docs, avgLength, clock.ElapsedMilliseconds, o.Threads);
    }

    private static DocInfo IndexFile(string path, int id, string dataDir, ConcurrentTermIndex terms)
    {
        // Decode explicitly as UTF-8 (no encoding sniffing); invalid bytes become the replacement character.
        var text = Encoding.UTF8.GetString(File.ReadAllBytes(path));
        var tokens = Tokenizer.Tokenize(text);

        // Count tf locally first, so each term takes its segment lock once per document, not once per occurrence.
        var tf = new Dictionary<string, int>();
        foreach (var token in tokens)
        {
            tf[token] = tf.GetValueOrDefault(token) + 1;
        }

        foreach (var (term, count) in tf)
        {
            terms.Add(term, id, count);
        }

        var relative = Path.GetRelativePath(dataDir, path).Replace('\\', '/');
        var docId = Path.ChangeExtension(relative, null);
        return new DocInfo(docId, path, TitleOf(text, path), tokens.Count);
    }

    private static string TitleOf(string text, string path)
    {
        // Scan line by line instead of splitting the whole document: only the first non-empty line is needed.
        for (var start = 0; start < text.Length;)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline < 0 ? text.Length : newline;
            var line = text.AsSpan(start, end - start).Trim();
            if (line.Length > 0)
            {
                return line.Length > Defaults.TitleMaxLength ? line[..Defaults.TitleMaxLength].ToString() : line.ToString();
            }

            start = end + 1;
        }

        return Path.GetFileName(path);
    }
}
