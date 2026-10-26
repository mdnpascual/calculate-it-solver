# Design: Parallel search

**Status:** Complete. See "As built" at the end. Assumes [streaming-results](../streaming-results/design.md) is built: `DivisionShortcut.Search(start, goal, buttons, maxPresses, onLine, cancel)` exists and `Solve` wraps it.

## Where code goes

- `CalculatorPuzzle/WorkerPool.cs` (new): dedicated pinned worker threads and the affinity mask math.
- `CalculatorPuzzle/DivisionShortcut.cs`: split the search into work items, run them on `WorkerPool`, make the shared state thread-safe.
- `CalculatorPuzzle.Tests/WorkerPoolTests.cs` (new) and `DivisionShortcutTests.cs`: tests.
- `MainForm.cs`: the benchmark double run (see "UI: timing comparison" below). The queue and timer stay as they are.

## Why dedicated threads, not `Parallel.ForEach`

`Parallel.ForEach` runs on thread-pool threads. Pinning a pool thread would leak the pin to whatever later runs on that thread (UI awaits, timers, other tasks). So `WorkerPool` starts its own `Thread`s, pins each one at start, and lets them end when the work runs out. Starting 8 threads costs well under a millisecond against a search of several seconds.

## `WorkerPool`

```csharp
public static class WorkerPool
{
    /// Half the logical processors, rounded down, at least 1.
    public static int DefaultWorkers { get; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// Processor index for worker i of n: the last n of processorCount, in order.
    /// ProcessorFor(16, 8, 0) = 8, ProcessorFor(16, 8, 7) = 15, ProcessorFor(1, 1, 0) = 0.
    public static int ProcessorFor(int processorCount, int workers, int index);

    /// Runs work(i) for i in [0, count) on `workers` threads. Items are taken in order from a
    /// shared counter (Interlocked.Increment), so a free thread takes the next item.
    /// Each thread pins itself (best effort) before taking items. Stops taking items once
    /// cancel is set. Waits for all threads. If a work item throws, cancels the rest and
    /// rethrows the first exception after all threads have ended.
    public static void Run(int count, Action<int> work, CancellationToken cancel,
                           int workers = 0, bool pin = true);   // workers 0 = DefaultWorkers
}
```

- Threads: `new Thread(...) { IsBackground = true, Name = $"Division search {i}" }`, so a stuck worker can't keep the process alive after the window closes.
- Never start more threads than `count`.
- Pinning, inside the thread before its first item:

  ```csharp
  [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
  [DllImport("kernel32.dll")] static extern UIntPtr SetThreadAffinityMask(IntPtr thread, UIntPtr mask);
  ```

  Skip it when `pin` is false, `Environment.ProcessorCount > 64`, or not on Windows. Mask is `1UL << ProcessorFor(...)`. A zero return (failure) is ignored. Call `Thread.BeginThreadAffinity()` first so the managed thread stays on its OS thread (a no-op on CoreCLR today, but it states the intent). Call `Thread.EndThreadAffinity()` when the thread ends.
- The cancel token is also linked to an internal source that a throwing item cancels, so the other threads stop taking items.

## `DivisionShortcut.Search` work items

Today's loops become a flat list of independent work items, built before any thread starts:

- Path A: one item per `(r, qText)`. Build the divisor list first (`10`, `1010`, … up to the existing overflow `break` and the `Math.Min(maxPresses - 1, 9)` cap), then pair each with each quotient.
- Path B (when digit keys exist): one item per `qText`.

Each item runs the body of today's inner loops unchanged (the `core`, `t`, `a` loops), checking `cancel` at the top of its `core` loop so a stop is quick.

Item size is uneven (long quotients have more prefix candidates). The shared counter in `WorkerPool.Run` handles this. Put Path B items first, since they tend to be the heaviest, so they don't all land at the end.

## Shared state

Everything read-only stays as is: `s`, `g`, `buttons` (the `HashSet`s inside are never written after `Parse`), `cores`, `quotients` (convert to a list before the threads start), `suffixes`, `prefixDigits`, `prefixes`.

| State | Today | Parallel |
|-------|-------|----------|
| `planCache` | `Dictionary` | `ConcurrentDictionary<(string, string), IReadOnlyList<Press>?>` with `GetOrAdd`. `LeftEdits.Plan` is pure, so running it twice for the same key is harmless. |
| `divisorCache` | `Dictionary` | `ConcurrentDictionary`, `GetOrAdd`. `CheapestDivisor` is pure. |
| dedupe | `HashSet<string>` of press text | `ConcurrentDictionary<string, byte> claimed`. See `AddLine` below. |
| `onLine` | called on one thread | called under one lock, so it's never called on two threads at once. |

`AddLine`:

1. Build presses, cost check (as now).
2. `if (!claimed.TryAdd(key, 0)) return;`. Claiming before the check is safe: the check depends only on the presses, so a key that fails will always fail.
3. `EngineeringCalculator.Run` check (outside any lock; it's the expensive part).
4. `lock (gate) { if (cancel.IsCancellationRequested) return; onLine(line); }`

Step 4 keeps the streaming spec's guarantees on many threads: lines are serialized, and a cancel made inside `onLine` means no line after it. So the streaming test "cancel inside `onLine` on the first line → exactly 1 line" still holds as written. Callers don't need a thread-safe `onLine`. (The UI's `queue.Enqueue` is thread-safe anyway.) Lines are rare compared to the work, so the lock costs almost nothing.

Signature: add `int workers = 0` (0 = `WorkerPool.DefaultWorkers`) and `bool pin = true` as the last parameters of `Search`, and `workers` as the last parameter of `Solve`. Tests use it to compare 1 worker against many.

`Solve` collects into a `List<Line>` from `onLine` (already serialized), then sorts with `Ranking`. Because the final list is sorted, thread timing can't change `Solve`'s output.

## UI: timing comparison

Requirements: "First implementation: timing comparison in the UI". `MainForm` changes:

- The background task becomes two phases in one task, so Esc and a new Enter cancel both with the one existing token:
  1. `sw = Stopwatch.StartNew(); DivisionShortcut.Search(s, g, buttons, max, onLine: queue.Enqueue, cancel, workers: 1, pin: false); singleTime = sw.Elapsed;` Lines go to the queue as today.
  2. Set a `phase` field to `Parallel` (volatile, so the 100 ms timer sees it). `sw.Restart(); DivisionShortcut.Search(..., onLine: line => parallelKeys.Add(line.Text), cancel, workers: 0, pin: true); parallelTime = sw.Elapsed;`. `parallelKeys` is filled under `Search`'s `onLine` lock, so a plain `List<string>` is safe. These lines never reach the queue, so the list on screen is the single-thread list.
- `Search` needs a `bool pin = true` parameter after `workers`, passed through to `WorkerPool.Run`. `Solve` keeps the default. With `workers: 1` and `pin: false` the run is the old single-thread path.
- The task returns a small record `(TimeSpan Single, TimeSpan Parallel, int ParallelCount, bool Match)`. `Match` compares `parallelKeys` as a set with the press texts of the single-thread lines (`found`).
- Timer (`searchTimer`) status while running: phase 1 uses the streaming text; phase 2 says `Searching with multiple threads… Esc to stop.` and shows no time. On completion, both times appear at once: `N results. single - {single:0.00} s multi - {parallel:0.00} s`, or the `MISMATCH` text. If the task was cancelled, keep `Stopped. N line(s) found.`
- No speed-up ratio is computed.
- When the temporary benchmark is dropped later, delete phase 1 and the record; nothing else here is needed.

## Test plan

`WorkerPoolTests`:

- `ProcessorFor`: (16, 8, 0) = 8, (16, 8, 7) = 15, (1, 1, 0) = 0, (3, 1, 0) = 2, (2, 1, 0) = 1.
- `DefaultWorkers` is `max(1, ProcessorCount / 2)`.
- `Run(100, …)` runs every index exactly once (count with `Interlocked`), with workers 1, 4 and 16.
- A throwing item: `Run` rethrows that exception, and every thread has ended when it returns.
- A token cancelled before the call: no items run.
- `pin: true` doesn't throw on this machine (pinning failure is silent).

`DivisionShortcutTests`:

- For each existing example, `Solve(..., workers: 1)` and `Solve(..., workers: 8)` return identical ranked lists (compare press text in order).
- `Search` with 8 workers never reports the same press text twice.
- `Search` with 8 workers: cancelling inside `onLine` on the first line gives exactly 1 line and returns null.
- All existing tests pass unchanged.

## Timing check (manual)

Release build, 41 → 483 with all buttons: the app's own status line now shows the `workers: 1` and the default (pinned) times after one Enter. Run it a few times (the first run includes JIT warm-up) and note the numbers. Also try the default unpinned once (temporarily pass `pin: false`). Write the numbers in "As built", with the CPU model. Pinned may be slower than unpinned on a hybrid Intel CPU (E-cores) or a dual-CCD X3D (non-3D CCD). That's expected and accepted (see requirements); record it but don't change the pinning.

## As built

Built as designed: `WorkerPool.cs`, flat work-item list (Path B items first, then Path A), concurrent caches and dedupe, one lock around `onLine`, `workers` / `pin` parameters on `Search` (`workers` on `Solve`), and the benchmark double run in `MainForm` (`RunBenchmark`, `SearchOutcome`).

Differences:

- The lock in `AddLine` also checks `cancel` after `onLine`, so a cancel made inside `onLine` stops that worker at once.
- `WorkerPool.Run` treats an `OperationCanceledException` from an item as a quiet stop for that thread (items throw it from `cancel.ThrowIfCancellationRequested()`).
- The benchmark's single-thread run also records press texts, to compare as a set with the parallel run.

Timings (Release, 41 → 483, all buttons, 214,678 lines; AMD Ryzen 9 9950X3D, 32 logical processors, 16 workers; measured with a throwaway console harness calling `Search` directly, two runs each):

| Run | Seconds |
|-----|---------|
| 1 thread, unpinned | 8.60, 8.10 |
| 16 workers, pinned to processors 16–31 | 5.13, 5.19 |
| 16 workers, unpinned | 5.03, 4.97 |

Same line count in every run. Threading helps, but only about 1.6×, far below 16×. Pinning to the non-V-Cache CCD costs about 3%. Likely causes (not investigated): uneven work items (a few heavy quotients), allocation/GC pressure from the many lines, and the shared lock with 214k lines. Worth a profile before tuning. The Task Manager and UI checks (load on the last half, responsiveness, Esc, closing) were not done by Claude; they need a manual look.

**Benchmark removed.** After the numbers above (and a real-life run: 92.7k results, 3.34 s single vs 2.48 s multi), the single-thread phase was dropped. `MainForm` runs `Search` with the default workers and pinning, `searchTask` is `Task<string?>` again, and the status is `Searching… N line(s) so far. Esc to stop.` then `N line(s)`. `RunBenchmark`, `SearchOutcome` and the MISMATCH check are gone; the tests still compare 1 worker against 8. The `Line` construction was also moved out of the lock in `AddLine` (no measurable change).
