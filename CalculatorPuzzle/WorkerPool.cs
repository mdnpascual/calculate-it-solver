using System.Runtime.InteropServices;

namespace CalculatorPuzzle;

/// <summary>
/// Runs indexed work on dedicated threads, each pinned (best effort) to one of the last N
/// logical processors. Dedicated threads, not the thread pool, so a pin never leaks to other work.
/// </summary>
public static class WorkerPool
{
    /// <summary>Half the logical processors, rounded down, at least 1.</summary>
    public static int DefaultWorkers { get; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>Processor index for worker `index` of `workers`: the last `workers` of `processorCount`, in order.</summary>
    public static int ProcessorFor(int processorCount, int workers, int index) =>
        Math.Max(0, processorCount - workers) + index;

    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    static extern UIntPtr SetThreadAffinityMask(IntPtr thread, UIntPtr mask);

    /// <summary>
    /// Runs work(i) for i in [0, count) on `workers` threads (0 = DefaultWorkers). Free threads take
    /// the next index from a shared counter. Stops taking items once cancel is set. Waits for all
    /// threads. If an item throws, the rest stop and the first exception is rethrown at the end.
    /// An OperationCanceledException from an item just stops that thread.
    /// </summary>
    public static void Run(int count, Action<int> work, CancellationToken cancel,
                           int workers = 0, bool pin = true)
    {
        if (workers <= 0)
            workers = DefaultWorkers;
        workers = Math.Min(workers, count);
        if (workers <= 0)
            return;

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        var token = stop.Token;
        int next = -1;
        Exception? failure = null;
        int processors = Environment.ProcessorCount;
        bool canPin = pin && OperatingSystem.IsWindows() && processors <= 64;

        void Worker(int index)
        {
            Thread.BeginThreadAffinity();
            try
            {
                if (canPin)
                {
                    int processor = ProcessorFor(processors, workers, index);
                    SetThreadAffinityMask(GetCurrentThread(), (UIntPtr)(1UL << processor));
                }
                while (!token.IsCancellationRequested)
                {
                    int item = Interlocked.Increment(ref next);
                    if (item >= count)
                        break;
                    work(item);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Interlocked.CompareExchange(ref failure, e, null);
                stop.Cancel();
            }
            finally
            {
                Thread.EndThreadAffinity();
            }
        }

        var threads = new Thread[workers];
        for (int i = 0; i < workers; i++)
        {
            int index = i;
            threads[i] = new Thread(() => Worker(index)) { IsBackground = true, Name = $"Division search {i}" };
            threads[i].Start();
        }
        foreach (var thread in threads)
            thread.Join();
        if (failure != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
