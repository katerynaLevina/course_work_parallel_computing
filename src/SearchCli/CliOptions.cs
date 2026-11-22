using SearchContracts;

namespace SearchCli;

public record CliOptions(string Command, string? Query, bool Rest, bool Expand, int TopK, string Server, int? Threads)
{
    public static CliOptions Parse(string[] args)
    {
        var command = CliDefaults.CommandSearch;
        var words = new List<string>();
        var rest = false;
        var expand = ApiRoutes.DefaultExpand;
        var topK = CliDefaults.DefaultTopK;
        var server = Environment.GetEnvironmentVariable(CliDefaults.ServerEnvVar) ?? CliDefaults.DefaultServer;
        int? threads = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case CliDefaults.FlagRest:
                    rest = true;
                    break;
                case CliDefaults.FlagNoExpand:
                    expand = false;
                    break;
                case CliDefaults.FlagTop:
                    topK = IntValue(args, ++i, CliDefaults.FlagTop, ApiRoutes.MinTopK, ApiRoutes.MaxTopK);
                    break;
                case CliDefaults.FlagServer:
                    server = Value(args, ++i, CliDefaults.FlagServer);
                    break;
                case CliDefaults.FlagIndex:
                    command = CliDefaults.CommandIndex;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out _))
                    {
                        threads = IntValue(args, ++i, CliDefaults.FlagIndex, ApiRoutes.MinThreads, ApiRoutes.MaxThreads);
                    }

                    break;
                case CliDefaults.FlagStats:
                    command = CliDefaults.CommandStats;
                    break;
                case CliDefaults.FlagHealth:
                    command = CliDefaults.CommandHealth;
                    break;
                case CliDefaults.FlagHelp:
                    return new CliOptions(CliDefaults.CommandHelp, null, rest, expand, topK, server, threads);
                case var flag when flag.StartsWith(CliDefaults.FlagPrefix):
                    throw Usage($"unknown option {flag}");
                default:
                    words.Add(args[i]);
                    break;
            }
        }

        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw Usage($"{CliDefaults.FlagServer} must be an http(s) URL");
        }

        var query = words.Count > 0 ? string.Join(' ', words) : null;
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
