using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using SearchContracts;

namespace SearchBench;

public static class ThroughputRun
{
    public static async Task RunAsync(Uri server, int[] clientCounts, int durationSeconds, bool expand)
    {
        var append = File.Exists(BenchConstants.ThroughputCsvPath);
        if (!append)
        {
            File.WriteAllLines(BenchConstants.ThroughputCsvPath, [BenchConstants.ThroughputCsvHeader]);
        }

        foreach (var clients in clientCounts)
        {
            var line = await MeasureAsync(server, clients, durationSeconds, expand);
            Console.WriteLine(line);
            File.AppendAllLines(BenchConstants.ThroughputCsvPath, [line]);
        }
    }

    private static async Task<string> MeasureAsync(Uri server, int clients, int durationSeconds, bool expand)
    {
        using var handler = new SocketsHttpHandler { MaxConnectionsPerServer = clients };
        using var http = new HttpClient(handler) { BaseAddress = server, Timeout = BenchConstants.SearchTimeout };
        var clock = Stopwatch.StartNew();
        var warmUpEnd = TimeSpan.FromSeconds(BenchConstants.WarmUpSeconds);
        var runEnd = warmUpEnd + TimeSpan.FromSeconds(durationSeconds);

        var workers = Enumerable.Range(0, clients).Select(id => ClientLoopAsync(http, id, expand, clock, warmUpEnd, runEnd));
        var samples = await Task.WhenAll(workers);

        var latencies = samples.SelectMany(s => s.Latencies).Order().ToArray();
        var errors = samples.Sum(s => s.Errors);
        var rps = latencies.Length / (double)durationSeconds;
        return string.Create(CultureInfo.InvariantCulture,
            $"{clients},{expand.ToString().ToLowerInvariant()},{latencies.Length},{errors},{rps:F2},{Percentile(latencies, BenchConstants.P50):F2},{Percentile(latencies, BenchConstants.P95):F2}");
    }

    private static async Task<(List<double> Latencies, int Errors)> ClientLoopAsync(
        HttpClient http, int id, bool expand, Stopwatch clock, TimeSpan warmUpEnd, TimeSpan runEnd)
    {
        var latencies = new List<double>();
        var errors = 0;
        var next = id;
        while (clock.Elapsed < runEnd)
        {
            var query = BenchConstants.Queries[next++ % BenchConstants.Queries.Length];
            var request = new SearchRequest(query, ApiRoutes.SearchModeKeyword, expand, null);
            var start = clock.Elapsed;
            try
            {
                using var response = await http.PostAsJsonAsync(ApiRoutes.Search, request, ApiRoutes.Json);
                response.EnsureSuccessStatusCode();
                await response.Content.ReadAsByteArrayAsync();
                if (start >= warmUpEnd)
                {
                    latencies.Add((clock.Elapsed - start).TotalMilliseconds);
                }
            }
            catch (HttpRequestException)
            {
                if (start >= warmUpEnd)
                {
                    errors++;
                }
            }
        }

        return (latencies, errors);
    }

    private static double Percentile(double[] sorted, double fraction) =>
        sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)(fraction * sorted.Length))];
}
