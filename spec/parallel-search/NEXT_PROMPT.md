# Next prompt: Parallel search

**Status:** Done. Kept for reference.

Make the Division Shortcut search in this repo (C#, .NET 9, WinForms, xUnit) run on half the logical processors, with each worker thread pinned to one of the last N processors, without changing its results.

## Read first

1. `CLAUDE.md`: project rules.
2. `spec/parallel-search/requirements.md` and `design.md`.
3. `spec/streaming-results/design.md` (the `Search` / `onLine` / cancel contract this builds on).
4. `CalculatorPuzzle/DivisionShortcut.cs`, `CalculatorPuzzle/LeftEdits.cs`, `CalculatorPuzzle/EngineeringCalculator.cs` (confirm `Plan`, `Run` and `CheapestDivisor` have no shared mutable state) and `CalculatorPuzzle.Tests/DivisionShortcutTests.cs`.

## Steps

1. Run `dotnet test` first and note that everything passes.
2. Add `CalculatorPuzzle/WorkerPool.cs` (`DefaultWorkers`, `ProcessorFor`, `Run` with per-thread pinning) and `CalculatorPuzzle.Tests/WorkerPoolTests.cs`.
3. `DivisionShortcut.cs`: build the flat work-item list (Path B items first, then Path A `(r, qText)`), switch the caches and dedupe to the concurrent versions, add the lock around `onLine`, add the `workers` parameter to `Search` and `Solve`.
4. Add the `DivisionShortcutTests` from the design's test plan (1 worker vs 8 workers gives identical ranked lists; no duplicates; cancel inside `onLine`).
5. `MainForm.cs`: add the benchmark double run from the design's "UI: timing comparison". Enter runs the single-thread search, then the parallel search, and shows both times together at the end (or a MISMATCH). The goal is to see whether threading really helps.
6. Do the timing check from the design in a Release build (`dotnet run -c Release --project CalculatorPuzzle`). Report the numbers (CPU model, 1 thread vs N threads, a few runs) to the user.
7. Update the Status section of `CLAUDE.md` and the Status lines in all three spec files. Add an "As built" section to the design with the timings and anything that differs.

## Check the work

- `dotnet build` with no warnings.
- `dotnet test` with all tests passing, including every existing test, unchanged.
- `dotnet run --project CalculatorPuzzle`, S = 41, G = 483, all buttons, Enter:
  - Task Manager (Performance → CPU → logical processors) shows load on the last half of the processors, not the first.
  - The form stays responsive and lines stream in during the single-thread run. When it ends, the status says `Searching with multiple threads…` (no time), the parallel run starts, and only then does the final status show both times: `N results. single - X.XX s multi - Y.YY s`, with no MISMATCH.
  - Esc stops the search: CPU load drops right away and the status says `Stopped. N line(s) found.`
  - Closing the window during a search ends the process.
