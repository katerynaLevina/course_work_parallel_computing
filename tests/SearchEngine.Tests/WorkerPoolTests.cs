using SearchEngine.Concurrency;

namespace SearchEngine.Tests;

public class WorkerPoolTests
{
    private const int Threads = 4;
    private const int ManyItems = 10_000;
    private const int ThreadProbeItems = 1000;
    private const int QueuedItems = 100;
    private const int Answer = 42;

    [Fact]
    public async Task Runs_all_submitted_work()
    {
        using var pool = new WorkerPool(Threads);
        var n = 0;

        var tasks = Enumerable.Range(0, ManyItems).Select(_ => pool.Submit(() => Interlocked.Increment(ref n)));
        await Task.WhenAll(tasks);

        Assert.Equal(ManyItems, n);
    }

    [Fact]
    public async Task Returns_values()
    {
        using var pool = new WorkerPool(Threads);

        Assert.Equal(Answer, await pool.Submit(() => 6 * 7));
    }

    [Fact]
    public async Task Exception_faults_task_but_pool_keeps_working()
    {
        using var pool = new WorkerPool(1);

        var failing = pool.Submit(() => throw new InvalidOperationException("boom"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing);

        Assert.Equal(Answer, await pool.Submit(() => Answer));
    }

    [Fact]
    public async Task Uses_exactly_thread_count_threads()
    {
        using var pool = new WorkerPool(Threads);
        var ids = new HashSet<int>();

        var tasks = Enumerable.Range(0, ThreadProbeItems).Select(_ => pool.Submit(() =>
        {
            lock (ids)
            {
                ids.Add(Environment.CurrentManagedThreadId);
            }

            Thread.Sleep(1);
        }));
        await Task.WhenAll(tasks);

        Assert.Equal(Threads, pool.ThreadCount);
        Assert.InRange(ids.Count, 2, Threads);
    }

    [Fact]
    public void Dispose_finishes_queued_work_then_stops()
    {
        var pool = new WorkerPool(2);
        var n = 0;
        for (var i = 0; i < QueuedItems; i++)
        {
            pool.Submit(() => Interlocked.Increment(ref n));
        }

        pool.Dispose();
        pool.Dispose(); // idempotent

        Assert.Equal(QueuedItems, n);
        Action submitAfterDispose = () => pool.Submit(() => { });
        Assert.Throws<InvalidOperationException>(submitAfterDispose);
    }

    [Fact]
    public void Zero_threads_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerPool(0));
    }
}
