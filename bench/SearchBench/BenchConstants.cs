namespace SearchBench;

public static class BenchConstants
{
    public const string DefaultServer = "http://localhost:8080";

    public const string BuildCsvPath = "bench/results/build.csv";

    public const string ThroughputCsvPath = "bench/results/throughput.csv";

    public const string BuildCsvHeader = "mode,threads,run,time_ms,documents";

    public const string ThroughputCsvHeader = "clients,expand,requests,errors,rps,p50_ms,p95_ms";

    public const string DefaultBuildThreads = "1,2,4,6,8,10,12,16";

    public const string DefaultBuildModes = "striped,global";

    public const int DefaultBuildRuns = 3;

    public const string DefaultClients = "1,2,4,8,16,32,64";

    public const int DefaultDurationSeconds = 20;

    public const int WarmUpSeconds = 3;

    public const double P50 = 0.50;

    public const double P95 = 0.95;

    public static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan SearchTimeout = TimeSpan.FromMinutes(10);

    public static readonly string[] Queries =
    [
        "history of rome", "quantum mechanics", "world war", "olympic games", "solar system",
        "french revolution", "computer science", "climate change", "football club", "river delta",
        "nobel prize", "ancient greece", "electric power", "mountain range", "united states president",
        "film director", "railway station", "roman empire", "molecular biology", "football league",
        "music album", "island population", "university professor", "chemical element", "city council",
        "national park", "river valley", "military battle", "political party", "novel author",
    ];
}
