using System.Diagnostics;
using SearchEngine.Concurrency;

namespace SearchEngine.Tests;

public class BlockingQueueTests
{
    private const int DelayMs = 100;
    private const int ToleratedDelayMs = 90;
    private const int GenerousWaitMs = 10_000;
    private const int StillBlockedProbeMs = 200;

    private const int Producers = 4;
    private const int Consumers = 4;
    private const int ItemsPerProducer = 50_000;
    private const int StressCapacity = 64;

    [Fact]
    public void Fifo_order_single_thread()
    {
        var queue = new BlockingQueue<int>(3);
        queue.Add(1);
        queue.Add(2);
        queue.Add(3);

        Assert.Equal(3, queue.Count);
        var taken = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            Assert.True(queue.TryTake(out var item));
            taken.Add(item);
        }

        Assert.Equal(new[] { 1, 2, 3 }, taken);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Take_blocks_until_item_added()
    {
        var queue = new BlockingQueue<int>(2);
        const int value = 42;
        var got = 0;
        var elapsedMs = 0L;
        var taker = new Thread(() =>
        {
            var watch = Stopwatch.StartNew();
            queue.TryTake(out got);
            elapsedMs = watch.ElapsedMilliseconds;
        });

        taker.Start();
        Thread.Sleep(DelayMs);
        queue.Add(value);

        Assert.True(taker.Join(GenerousWaitMs));
        Assert.Equal(value, got);
        Assert.True(elapsedMs >= ToleratedDelayMs, $"taker returned after only {elapsedMs} ms");
    }

    [Fact]
    public void Add_blocks_when_full()
    {
        var queue = new BlockingQueue<int>(2);
        queue.Add(1);
        queue.Add(2);
        var thirdAdded = false;
        var producer = new Thread(() =>
        {
            queue.Add(3);
            thirdAdded = true;
        });

        producer.Start();
        Assert.False(producer.Join(StillBlockedProbeMs));
        Assert.False(Volatile.Read(ref thirdAdded));

        Assert.True(queue.TryTake(out var first));
        Assert.Equal(1, first);
        Assert.True(producer.Join(GenerousWaitMs));
        Assert.True(thirdAdded);
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void TryTake_returns_false_after_complete_and_drained()
    {
        var queue = new BlockingQueue<int>(2);
        queue.Add(7);
        queue.CompleteAdding();

        Assert.True(queue.TryTake(out var item));
        Assert.Equal(7, item);
        Assert.False(queue.TryTake(out _));
    }

    [Fact]
    public void CompleteAdding_wakes_blocked_taker()
    {
        var queue = new BlockingQueue<int>(2);
        var result = true;
        var taker = new Thread(() => result = queue.TryTake(out _));

        taker.Start();
        Thread.Sleep(DelayMs);
        queue.CompleteAdding();

        Assert.True(taker.Join(GenerousWaitMs));
        Assert.False(result);
    }

    [Fact]
    public void Add_after_complete_throws()
    {
        var queue = new BlockingQueue<int>(2);
        queue.CompleteAdding();

        Assert.Throws<InvalidOperationException>(() => queue.Add(1));
    }

    [Fact]
    public void Many_producers_many_consumers_lose_nothing()
    {
        var queue = new BlockingQueue<int>(StressCapacity);
        long addedSum = 0, takenSum = 0, takenCount = 0;

        var producers = Enumerable.Range(0, Producers).Select(p => new Thread(() =>
        {
            for (var i = 0; i < ItemsPerProducer; i++)
            {
                var value = p * ItemsPerProducer + i;
                queue.Add(value);
                Interlocked.Add(ref addedSum, value);
            }
        })).ToArray();

        var consumers = Enumerable.Range(0, Consumers).Select(_ => new Thread(() =>
        {
            while (queue.TryTake(out var item))
            {
                Interlocked.Add(ref takenSum, item);
                Interlocked.Increment(ref takenCount);
            }
        })).ToArray();

        foreach (var t in producers.Concat(consumers)) t.Start();
        foreach (var t in producers) Assert.True(t.Join(GenerousWaitMs * 3));
        queue.CompleteAdding();
        foreach (var t in consumers) Assert.True(t.Join(GenerousWaitMs * 3));

        Assert.Equal(Producers * ItemsPerProducer, takenCount);
        Assert.Equal(addedSum, takenSum);
    }
}
