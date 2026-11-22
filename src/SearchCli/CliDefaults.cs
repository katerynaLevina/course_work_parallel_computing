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
          --no-expand     skip query expansion (uses REST)
          --top N         number of results (default 10)
          --server URL    server address (default $SEARCH_SERVER or http://localhost:8080)
          --help          show this help
        """;
}
