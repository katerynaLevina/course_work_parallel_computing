namespace SearchCli;

public static class CliDefaults
{
    public const string DefaultServer = "http://localhost:8080";

    public const string ServerEnvVar = "SEARCH_SERVER";

    public const string NoColorEnvVar = "NO_COLOR";

    public const int DefaultTopK = 10;

    public const string CommandSearch = "search";

    public const string CommandIndex = "index";

    public const string CommandStats = "stats";

    public const string CommandHealth = "health";

    public const string CommandHelp = "help";

    public const string FlagRest = "--rest";

    public const string FlagNoExpand = "--no-expand";

    public const string FlagTop = "--top";

    public const string FlagServer = "--server";

    public const string FlagIndex = "--index";

    public const string FlagStats = "--stats";

    public const string FlagHealth = "--health";

    public const string FlagHelp = "--help";

    public const string FlagPrefix = "--";

    public const string ScoreFormat = "F4";

    public const string OllamaAvailable = "available";

    public const string OllamaUnavailable = "unavailable";

    /// <summary>Above the server's default Ollama timeout (60 s) so a slow expansion is not cut off by the client.</summary>
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(120);

    /// <summary>A build can take minutes; the server finishes it even if the client stops waiting.</summary>
    public static readonly TimeSpan IndexTimeout = Timeout.InfiniteTimeSpan;

    public const string MessageUnreachable = "server not reachable at {0}";

    public const string MessageTimeout = "server did not respond in time";

    public const string MessageBadResponse = "unexpected response from server";

    public const string MessageStatus = "server returned {0}";

    public const int ExitOk = 0;

    public const int ExitServerError = 1;

    public const int ExitUsage = 2;

    public const string AnsiReset = "\u001b[0m";

    public const string AnsiBoldYellow = "\u001b[1;33m";

    public const string AnsiDim = "\u001b[2m";

    public const string Usage = """
        Usage:
          search [options] "query"       search the corpus
          search --index [threads]       (re)build the index
          search --stats                 show index statistics
          search --health                check server and Ollama status

        Options:
          --rest          use POST /api/search instead of the WebSocket stream
          --no-expand     skip query expansion
          --top N         number of results (default 10)
          --server URL    server address (default $SEARCH_SERVER or http://localhost:8080)
          --help          show this help
        """;
}
