namespace SearchEngine.Concurrency;

/// <summary>
/// Bounded FIFO queue for producer/consumer hand-off. Items live in a fixed ring buffer
/// and every access is guarded by one lock object; threads sleep with Monitor.Wait
/// instead of spinning.
/// </summary>
public sealed class BlockingQueue<T>(int capacity)
{
    private readonly object _lock = new();
    private readonly T[] _buffer = new T[capacity];
    private int _head;   // index of the oldest item
    private int _count;
    private bool _completed;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>Blocks while the queue is full. Throws once CompleteAdding was called.</summary>
    public void Add(T item)
    {
        lock (_lock)
        {
            // Wait is in a while loop, not an if: PulseAll wakes every waiter, and by the time
            // this thread re-acquires the lock another producer may already have taken the free
            // slot, so the condition must be re-checked after every wake-up.
            while (_count == _buffer.Length && !_completed)
            {
                Monitor.Wait(_lock);
            }

            if (_completed)
            {
                throw new InvalidOperationException("The queue does not accept new items after CompleteAdding.");
            }

            _buffer[(_head + _count) % _buffer.Length] = item;
            _count++;

            // Producers and consumers wait on the same lock object, so Pulse could wake the wrong
            // kind (e.g. another producer) and leave a consumer asleep forever. PulseAll wakes
            // everyone; each re-checks its own condition and the unfit ones go back to sleep.
            Monitor.PulseAll(_lock);
        }
    }

    /// <summary>
    /// Blocks while the queue is empty. Returns false only when the queue is completed and drained.
    /// </summary>
    public bool TryTake(out T item)
    {
        lock (_lock)
        {
            while (_count == 0 && !_completed)
            {
                Monitor.Wait(_lock);
            }

            if (_count == 0)
            {
                item = default!;
                return false;
            }

            item = _buffer[_head];
            _buffer[_head] = default!; // do not keep the item alive from the buffer
            _head = (_head + 1) % _buffer.Length;
            _count--;

            Monitor.PulseAll(_lock);
            return true;
        }
    }

    /// <summary>No more items will be added; wakes all waiters so consumers can drain and finish.</summary>
    public void CompleteAdding()
    {
        lock (_lock)
        {
            _completed = true;
            Monitor.PulseAll(_lock);
        }
    }
}
