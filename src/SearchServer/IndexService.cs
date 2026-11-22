using SearchEngine.Index;

namespace SearchServer;

public sealed class BuildInProgressException() : Exception("an index build is already in progress");

public sealed class IndexService(EngineState state, ServerOptions options)
{
    public async Task<SearchIndex> BuildAsync(int threads, LockMode mode, int segments, CancellationToken ct)
    {
        if (!state.TryBeginBuild())
        {
            throw new BuildInProgressException();
        }

        try
        {
            var index = await IndexBuilder.BuildAsync(
                new IndexBuildOptions(options.DataDir, options.DataGlob, threads, mode, segments), ct);
            state.Publish(index);
            return index;
        }
        finally
        {
            state.EndBuild();
        }
    }
}
