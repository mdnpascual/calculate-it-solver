namespace CalculatorPuzzle.Tests;

public class WorkerPoolTests
{
    [Theory]
    [InlineData(16, 8, 0, 8)]
    [InlineData(16, 8, 7, 15)]
    [InlineData(1, 1, 0, 0)]
    [InlineData(3, 1, 0, 2)]
    [InlineData(2, 1, 0, 1)]
    public void ProcessorFor_picks_the_last_processors(int count, int workers, int index, int expected) =>
        Assert.Equal(expected, WorkerPool.ProcessorFor(count, workers, index));

    [Fact]
    public void DefaultWorkers_is_half_the_processors_at_least_one() =>
        Assert.Equal(Math.Max(1, Environment.ProcessorCount / 2), WorkerPool.DefaultWorkers);

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void Run_runs_every_index_exactly_once(int workers)
    {
        var hits = new int[100];
        WorkerPool.Run(100, i => Interlocked.Increment(ref hits[i]), default, workers);
        Assert.All(hits, h => Assert.Equal(1, h));
    }

    [Fact]
    public void Run_rethrows_an_item_exception_after_all_threads_end()
    {
        int running = 0;
        var e = Assert.Throws<InvalidOperationException>(() =>
            WorkerPool.Run(50, i =>
            {
                Interlocked.Increment(ref running);
                try
                {
                    if (i == 3)
                        throw new InvalidOperationException("boom");
                    Thread.Sleep(5);
                }
                finally { Interlocked.Decrement(ref running); }
            }, default, 4));
        Assert.Equal("boom", e.Message);
        Assert.Equal(0, running);
    }

    [Fact]
    public void Run_with_a_cancelled_token_runs_nothing()
    {
        int ran = 0;
        WorkerPool.Run(10, _ => Interlocked.Increment(ref ran), new CancellationToken(true), 4);
        Assert.Equal(0, ran);
    }

    [Fact]
    public void Run_pinned_does_not_throw() =>
        WorkerPool.Run(20, _ => { }, default, pin: true);
}
