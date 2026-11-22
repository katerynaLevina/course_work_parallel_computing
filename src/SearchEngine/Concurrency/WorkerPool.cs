namespace SearchEngine.Concurrency;

/// <summary>
/// Fixed set of worker threads that take jobs from one shared BlockingQueue.
/// Submit returns a Task so callers can wait for the result or the exception.
/// </summary>
public sealed class WorkerPool : IDisposable
{
    private readonly BlockingQueue<Action> _jobs;
    private readonly Thread[] _threads;

    public WorkerPool(int threadCount, int queueCapacity = Defaults.WorkerQueueCapacity, string name = Defaults.WorkerName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threadCount, 1);

        _jobs = new BlockingQueue<Action>(queueCapacity);
        _threads = new Thread[threadCount];
        for (var i = 0; i < threadCount; i++)
        {
            _threads[i] = new Thread(WorkerLoop) { IsBackground = true, Name = $"{name}-{i}" };
            _threads[i].Start();
        }
    }

    public int ThreadCount => _threads.Length;

    public Task Submit(Action work) => Submit<object?>(() =>
    {
        work();
        return null;
    });

    public Task<T> Submit<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The job never throws: a failure is stored in the task, so the worker thread survives it.
        _jobs.Add(() =>
        {
            try
            {
                completion.SetResult(work());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        return completion.Task;
    }

    /// <summary>
    /// Stops accepting work, lets the workers finish everything already queued, then waits for them to exit.
    /// Safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        _jobs.CompleteAdding();
        foreach (var thread in _threads)
        {
            thread.Join();
        }
    }

    // Each worker blocks in TryTake until a job arrives; it returns false only after
    // CompleteAdding was called and the queue is empty, which ends the loop and the thread.
    private void WorkerLoop()
    {
        while (_jobs.TryTake(out var job))
        {
            job();
        }
    }
}
