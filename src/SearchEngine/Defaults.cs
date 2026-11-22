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

    public const int IndexSegments = 256;
    public const int IndexBatchSize = 64;
    // Smaller batches on small corpora so every build thread gets several of them.
    public const int BatchesPerThread = 4;
    public const int IndexBuildThreads = 4;
    public const int TitleMaxLength = 200;
    public const int SegmentInitialBuckets = 16;
    public const int SegmentHashShift = 16;
    public const double IndexLoadFactor = 0.75;
    public const int PostingListInitialCapacity = 4;

    public const double Bm25K1 = 1.2;
    public const double Bm25B = 0.75;
    public const double OriginalTermWeight = 1.0;
    public const double ExpandedTermWeight = 0.5;
    public const int SnippetRadius = 120;
    public const int DefaultTopK = 10;
    public const int ScoreDecimals = 4;
    public const string Ellipsis = "…";

    public const string SearchPoolKey = "search";

    public const string SearchThreadsEnv = "SEARCH_THREADS";

    public const string OllamaUrlEnv = "OLLAMA_URL";
    public const string OllamaUrl = "http://ollama:11434";

    public const string OllamaModelEnv = "OLLAMA_MODEL";
    public const string OllamaModel = "gemma4:e2b";

    public const string OllamaTimeoutMsEnv = "OLLAMA_TIMEOUT_MS";
    public const int OllamaTimeoutMs = 60000;

    public const string OllamaNumThreadEnv = "OLLAMA_NUM_THREAD";
    // Tuned for the performance cores of the target Mac (efficiency cores stall llama.cpp, which synchronises threads
    // every step); override with OLLAMA_NUM_THREAD.
    public const int OllamaNumThread = 4;

    public const string OllamaTagsPath = "/api/tags";
    public const string OllamaLatestTag = ":latest";
    public const string OllamaGeneratePath = "/api/generate";
    public const string OllamaJsonFormat = "json";
    public const double OllamaTemperature = 0;
    public const int OllamaNumPredict = 64;
    public const int MaxExpandedTerms = 8;
    public static readonly string ExpansionPrompt =
        $"Return JSON {{\"terms\":[...]}} with up to {MaxExpandedTerms} single English words that are synonyms or closely related " +
        "search keywords for the query. No explanations. Query: ";
    public const string ExpansionTimeoutError = "expansion timed out";
    public const string ExpansionHttpError = "ollama request failed";
    public const string ExpansionBadResponseError = "ollama returned an invalid response";

    public static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(2);
}
