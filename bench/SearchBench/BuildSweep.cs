using System.Net.Http.Json;
using SearchContracts;

namespace SearchBench;

public static class BuildSweep
{
    public static async Task RunAsync(Uri server, int[] threadCounts, string[] modes, int runs)
    {
        using var http = new HttpClient { BaseAddress = server, Timeout = BenchConstants.BuildTimeout };
        var lines = new List<string> { BenchConstants.BuildCsvHeader };
        foreach (var mode in modes)
        {
            foreach (var threads in threadCounts)
            {
                for (var run = 1; run <= runs; run++)
                {
                    var response = await http.PostAsJsonAsync(ApiRoutes.Index, new IndexRequest(threads, mode, null), ApiRoutes.Json);
                    response.EnsureSuccessStatusCode();
                    var result = (await response.Content.ReadFromJsonAsync<IndexResponse>(ApiRoutes.Json))!;
                    lines.Add($"{mode},{threads},{run},{result.TimeMs},{result.DocumentsIndexed}");
                    Console.WriteLine(lines[^1]);
                    File.WriteAllLines(BenchConstants.BuildCsvPath, lines);
                }
            }
        }
    }
}
