namespace SearchEngine.Index;

public sealed record IndexBuildOptions(
    string DataDir,
    string Glob = Defaults.DataGlob,
    int Threads = Defaults.IndexBuildThreads,
    LockMode Mode = LockMode.Striped,
    int Segments = Defaults.IndexSegments,
    int BatchSize = Defaults.IndexBatchSize);
