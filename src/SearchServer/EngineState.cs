using SearchEngine.Index;

namespace SearchServer;

/// <summary>Holds the currently published index and the single-build-at-a-time flag.</summary>
public sealed class EngineState
{
    private SearchIndex? _current;
    private int _building;

    /// <summary>
    /// Volatile.Read pairs with the Volatile.Write in <see cref="Publish"/>: that release/acquire handoff
    /// guarantees a reader that sees the new reference also sees the fully built index, which is why
    /// the frozen index can then be read without locks.
    /// </summary>
    public SearchIndex? Current => Volatile.Read(ref _current);

    public void Publish(SearchIndex index) => Volatile.Write(ref _current, index);

    public bool TryBeginBuild() => Interlocked.CompareExchange(ref _building, 1, 0) == 0;

    public void EndBuild() => Volatile.Write(ref _building, 0);
}
