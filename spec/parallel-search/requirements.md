# Requirements: Parallel search

**Status:** Complete. The timing comparison in the UI was removed after the numbers were in; Enter runs the parallel search only. Builds on [streaming-results](../streaming-results/requirements.md), which must be done first.

## Goal

The Division Shortcut search runs on one thread today (41 → 483 takes about 14 s in Debug with all buttons). Split its work across several worker threads so it finishes sooner:

- Use **half of the logical processors**, rounded down, and at least 1 (16 logical processors → 8 workers).
- **Pin the workers to the last N logical processors** where possible (16 logical processors, 8 workers → processors 8–15). The first half stays free for the UI and the rest of the machine. This is on purpose, and the search may run slower for it:
  - Intel hybrid CPUs (12th gen and later): Windows numbers the P-cores first and the E-cores last, so the search runs on the E-cores and leaves the P-cores free.
  - AMD dual-CCD X3D CPUs (for example 7950X3D, 9950X3D): the first CCD has the 3D V-Cache and the second one doesn't, so the search runs on the non-3D CCD and leaves the V-Cache CCD free.
  - Other CPUs: the last half of the logical processors, whatever they are.
- The results don't change: the same set of lines, ranked the same way.

## Behavior

- Each worker gets its own processor from that last-N set (one processor each).
- Work is handed out a piece at a time, so a worker that finishes early takes the next piece instead of sitting idle.
- Streaming works as specified in [streaming-results](../streaming-results/requirements.md): lines show up as any worker finds them, are deduplicated, and the displayed list is re-sorted every 100 ms.
- Esc, a new Enter, and closing the window stop **all** workers. Once a stop has been seen, no further line is reported.
- The UI thread is never pinned or slowed by this change. The process-wide affinity stays as it is.
- `Solve` (used by the tests) uses the same parallel search and returns the same ranked list as before.

## First implementation: timing comparison in the UI

The first implementation exists to answer one question: does threading really help? So Enter runs the search twice, back to back, and shows both times in the status line (the one that today says `N line(s)`).

One Enter, one result line. The times are **not** shown after the first run alone: nothing is shown until both runs are done, and then both times appear together.

1. **Single-thread run** (the current implementation: `workers: 1`, no pinning). Lines stream in as in [streaming-results](../streaming-results/requirements.md). While it runs, the status says `Searching… N line(s) so far. Esc to stop.` When it finishes, the status says `Searching with multiple threads… Esc to stop.` (no time yet). The timer covers only the search itself (start of `Search` to its return), not UI updates after it.
2. **Parallel run** (`WorkerPool.DefaultWorkers` threads, pinned as described below). It starts right after step 1 finishes, with no key press. Its lines are **not** added to the list (the list from step 1 stays on screen); they are only counted. When it finishes, the status says `N results. single - 14.23 s multi - 3.10 s`.
3. If the two runs find a different number of lines (or a different set of press texts), the status says so: `MISMATCH: 1 thread found N, 8 threads found M.` This is the check that threading didn't change the results.
4. Esc, a new Enter and closing the window stop whichever run is active and skip the other. After Esc the status says `Stopped. N line(s) found.` and no comparison is shown (a stopped run has no meaningful time).
5. Times are shown in seconds with two decimals (`0.00 s`), measured with `Stopwatch`. No speed-up ratio is shown; the two times are enough to compare.
6. Run the comparison in a Release build to judge the numbers. Debug is slower overall and not a fair measure.

This double run is a temporary benchmark. Once the question is answered, a later change drops step 1 (or puts it behind a "Compare with 1 thread" checkbox) so a normal Enter runs the parallel search only. Record the final numbers and the CPU model in the design's "As built" section.

## Pinning rules

- Pinning is best effort. If the machine has more than 64 logical processors (Windows processor groups), or the OS call fails, the workers run unpinned and the search still runs normally. No error is shown.
- Pinning is per worker thread, not per process, so it can't affect the UI thread or other threads.

## Edge cases

- 1 logical processor: 1 worker, pinned to processor 0 (the only one).
- 2 or 3 logical processors: 1 worker, pinned to the last one.
- A worker that throws stops the others, and the exception comes out of `Search` / `Solve` (same as today, where an exception would come out of `Solve`).
- Lines found by two workers at once with the same presses: shown once.
- The position rule ("last N") is the whole rule. The app doesn't detect core types, CCDs or cache sizes. On the CPUs named above, Windows' numbering already puts the cores we want at the end.
- On a hybrid CPU where half the logical processors is more than the E-core count (for example 8 P-cores with HT = 16, plus 16 E-cores = 32 logical, 16 workers), the last N are all E-cores. With fewer E-cores, the extra workers land on the last P-core threads. That's fine; no special case.
- AMD's 3D V-Cache driver may park the non-3D CCD while a game runs. A thread with a hard affinity still runs there (Windows unparks the core for it). Don't try to work around parking.

## Open questions

- Should the worker count be adjustable in the UI? Not for now; it's a constant in code.
- Should the benchmark double run stay as an option after the first implementation? Decide once the numbers are in.

## Out of scope

- Changing what the search looks for or the order it finds lines in.
- Parallelizing `LeftEdits.Plan`, `CheapestDivisor` or the Modulo Shortcut on their own.
- Processor groups (more than 64 logical processors) beyond falling back to unpinned.
- Linux/macOS pinning (the app is WinForms, Windows only).
