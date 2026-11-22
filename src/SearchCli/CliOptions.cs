using SearchContracts;

namespace SearchCli;

public record CliOptions(string Command, string? Query, bool Rest, bool Expand, int TopK, string Server, int? Threads)
{
    public static CliOptions Parse(string[] args)
    {
        var command = CliDefaults.CommandSearch;
        string? query = null;
        var rest = false;
        var expand = ApiRoutes.DefaultExpand;
        var topK = CliDefaults.DefaultTopK;
        var server = Environment.GetEnvironmentVariable(CliDefaults.ServerEnvVar) ?? CliDefaults.DefaultServer;
        int? threads = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--rest":
                    rest = true;
                    break;
                case "--no-expand":
                    expand = false;
                    break;
                case "--top":
                    topK = IntValue(args, ++i, "--top", ApiRoutes.MinTopK, ApiRoutes.MaxTopK);
                    break;
                case "--server":
                    server = Value(args, ++i, "--server");
                    break;
                case "--index":
                    command = CliDefaults.CommandIndex;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out _))
                    {
                        threads = IntValue(args, ++i, "--index", ApiRoutes.MinThreads, ApiRoutes.MaxThreads);
                    }

                    break;
                case "--stats":
                    command = CliDefaults.CommandStats;
                    break;
                case "--health":
                    command = CliDefaults.CommandHealth;
                    break;
                case "--help":
                    return new CliOptions(CliDefaults.CommandHelp, null, rest, expand, topK, server, threads);
                case var flag when flag.StartsWith("--"):
                    throw Usage($"unknown option {flag}");
                default:
                    query = args[i];
                    break;
            }
        }

        if (command == CliDefaults.CommandSearch && string.IsNullOrWhiteSpace(query))
        {
            throw Usage("a query is required");
        }

        return new CliOptions(command, query, rest, expand, topK, server, threads);
    }

    private static string Value(string[] args, int index, string flag) =>
        index < args.Length ? args[index] : throw Usage($"{flag} needs a value");

    private static int IntValue(string[] args, int index, string flag, int min, int max) =>
        int.TryParse(Value(args, index, flag), out var value) && value >= min && value <= max
            ? value
            : throw Usage($"{flag} must be a number from {min} to {max}");

    private static ArgumentException Usage(string problem) => new($"{problem}\n\n{CliDefaults.Usage}");
}
