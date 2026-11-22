namespace SearchEngine;

public static class Defaults
{
    public const string DataDirEnv = "DATA_DIR";
    public const string DataDir = "/data";

    public const string DataGlobEnv = "DATA_GLOB";
    public const string DataGlob = "*.txt";

    public const int MinTokenLength = 2;

    public const int WorkerQueueCapacity = 1024;
    public const string WorkerName = "worker";

    public const string SearchThreadsEnv = "SEARCH_THREADS";

    public const string OllamaUrlEnv = "OLLAMA_URL";
    public const string OllamaUrl = "http://ollama:11434";

    public const string OllamaModelEnv = "OLLAMA_MODEL";
    public const string OllamaModel = "gemma4:e2b";

    public const string OllamaTimeoutMsEnv = "OLLAMA_TIMEOUT_MS";
    public const int OllamaTimeoutMs = 20000;

    public const string OllamaTagsPath = "/api/tags";
    public const string OllamaLatestTag = ":latest";

    public static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(2);
}
