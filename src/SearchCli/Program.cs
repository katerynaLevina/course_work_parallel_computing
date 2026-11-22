using System.Net.WebSockets;
using SearchCli;

CliOptions options;
try
{
    options = CliOptions.Parse(args);
}
catch (ArgumentException e)
{
    Console.Error.WriteLine(e.Message);
    return CliDefaults.ExitUsage;
}

if (options.Command == CliDefaults.CommandHelp)
{
    Console.WriteLine(CliDefaults.Usage);
    return CliDefaults.ExitOk;
}

var useColor = !Console.IsOutputRedirected && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(CliDefaults.NoColorEnvVar));
var renderer = new ConsoleRenderer(Console.Out, useColor);

try
{
    using var client = new ApiClient(options.Server);
    switch (options.Command)
    {
        case CliDefaults.CommandIndex:
            var indexed = await client.IndexAsync(options.Threads, default);
            Console.WriteLine($"Indexed {indexed.DocumentsIndexed} documents in {indexed.TimeMs} ms using {indexed.ThreadsUsed} threads ({indexed.LockMode} locks)");
            break;
        case CliDefaults.CommandStats:
            var stats = await client.StatsAsync(default);
            Console.WriteLine($"Documents: {stats.Documents}");
            Console.WriteLine($"Terms: {stats.Terms}");
            Console.WriteLine($"Postings: {stats.Postings}");
            Console.WriteLine($"Index build time: {stats.IndexBuildTimeMs} ms");
            Console.WriteLine($"Threads used: {stats.ThreadsUsed} ({stats.LockMode} locks)");
            break;
        case CliDefaults.CommandHealth:
            var health = await client.HealthAsync(default);
            Console.WriteLine($"Status: {health.Status}");
            Console.WriteLine($"Ollama: {(health.Ollama ? "available" : "unavailable")}");
            break;
        case CliDefaults.CommandSearch when options.Rest || !options.Expand:
            var response = await client.SearchAsync(options.Query!, options.TopK, options.Expand, default);
            if (options.Expand)
            {
                renderer.Expansion(response.ExpandedTerms);
            }

            renderer.Results(response.Results, response.TimeMs, options.Query!);
            break;
        default:
            await client.StreamSearchAsync(options.Query!, options.TopK, renderer, default);
            break;
    }

    return CliDefaults.ExitOk;
}
catch (Exception e) when (e is HttpRequestException or WebSocketException)
{
    var unreachable = e is WebSocketException || e.InnerException is System.Net.Sockets.SocketException;
    Console.Error.WriteLine(unreachable ? $"server not reachable at {options.Server}" : e.Message);
    return CliDefaults.ExitServerError;
}
