# Design: Streaming results

**Status:** Complete.

## Where code goes

- `CalculatorPuzzle/DivisionShortcut.cs`: a `Search` method that reports each line as it's found and can be cancelled. `Solve` becomes a wrapper around it.
- `CalculatorPuzzle/MainForm.cs`: run the search on a background task, collect lines on a timer, Esc to stop.
- `CalculatorPuzzle.Tests/DivisionShortcutTests.cs`: tests for `Search` and cancellation.

## Solver (`DivisionShortcut`)

```csharp
/// Reports each new, checked line through onLine as soon as it's found (no duplicates, any order).
/// Returns an input error (same messages as Solve's validation) or null. Stops early when the
/// token is cancelled and returns null. Doesn't throw on cancel.
public static string? Search(long start, long goal, Buttons buttons, int maxPresses,
                             Action<Line> onLine, CancellationToken cancel = default);

/// The ranking order used by Solve and the UI.
public static IComparer<Line> Ranking { get; }

/// Unchanged signature and results: runs Search, collects, sorts by Ranking,
/// and returns "No division line fits…" when nothing was found.
public static Result Solve(long start, long goal, Buttons buttons, int maxPresses);
```

- Move the body of `Solve` into `Search`. Where it now does `lines[key] = new Line(...)`, it also calls `onLine(line)`. The dedupe dictionary stays inside `Search` (a `HashSet<string>` of press text is enough).
- Check `cancel.IsCancellationRequested` and return right after each `onLine` call and at the top of the quotient loop (and the prefix loop, if [digit-keys](../digit-keys/design.md) Path B exists). This makes cancel quick and testable: cancel inside `onLine` means no more lines.
- `Ranking`: the existing `OrderBy(Cost).ThenBy(non-flip presses).ThenBy(Divisor).ThenBy(press text, Ordinal)`, as one comparer, so `Solve` and the UI sort the same way.
- The "no line fits" error stays in `Solve`. `Search` can't know it until the end, so the UI adds it when a finished search found nothing.

## UI (`MainForm`)

State:

```csharp
CancellationTokenSource? searchCancel;
ConcurrentQueue<DivisionShortcut.Line>? searchQueue;  // lines from the running search
Task? searchTask;
readonly List<DivisionShortcut.Line> found = [];
readonly System.Windows.Forms.Timer searchTimer = new() { Interval = 100 };
```

`StartDivisionSearch()` (replaces `RecalculateDivision`, runs on Enter in S, G or Buttons):

1. `StopDivisionSearch(keepLines: false)`.
2. Parse S, G and Buttons. On error, show it and return.
3. Make a new `CancellationTokenSource` and a new `ConcurrentQueue`. Capture both in locals, so the task writes only to its own queue; a stopped search's late lines land in a queue nobody reads.
4. `searchTask = Task.Run(() => DivisionShortcut.Search(start, goal, buttons, MaxPresses, queue.Enqueue, token))`.
5. Start `searchTimer`. Status: `Searching… 0 line(s) so far. Esc to stop.`

`searchTimer.Tick` (UI thread):

1. Drain `searchQueue` into `found`. If anything was added, sort `found` with `DivisionShortcut.Ranking` and redraw the Lines box with the top 10. Skip the redraw if the top 10 didn't change, to avoid flicker.
2. If `searchTask` has completed: drain once more, stop the timer, and set the status. Use the task's returned error if there is one; `No division line fits the available buttons.` if `found` is empty; otherwise `N line(s)`.
3. Otherwise update `Searching… N line(s) so far. Esc to stop.`

`StopDivisionSearch(bool keepLines)`:

- Cancel and dispose the token source, set `searchQueue`, `searchTask` and `searchCancel` to null, and stop the timer. Don't wait for the task; it exits on its own at the next check.
- If `keepLines`, drain the queue into `found` first and redraw. Otherwise clear `found` and the Lines box.

Esc: set `KeyPreview = true` on the form and handle `KeyDown`. If a search is running, call `StopDivisionSearch(keepLines: true)`, set the status to `Stopped. N line(s) found.`, and mark the key handled (`e.SuppressKeyPress = true`). If nothing is running, do nothing.

Closing: in `OnFormClosing`, call `StopDivisionSearch(keepLines: false)`.

`Search` must not touch any control. It only calls `onLine`, which only enqueues.

## Test plan

`DivisionShortcutTests`:

- `Search` reports the same set of lines as `Solve` for each existing example (compare press text as sets).
- `Search` never reports the same press sequence twice.
- Cancelling inside `onLine` on the first line: exactly 1 line is reported, and `Search` returns null.
- A token cancelled before the call: no lines, returns null.
- Input errors come back from `Search` as the same messages as `Solve`.
- Sorting `Search`'s lines with `Ranking` gives the same order as `Solve`.
- All existing `Solve` tests still pass unchanged.

The UI isn't unit tested. Check it by running the app (see [NEXT_PROMPT.md](NEXT_PROMPT.md)).

## As built

- Matches the design. `Search` stops on cancel by throwing an internal `OperationCanceledException` from `AddLine` and the quotient loops (checked right after each `onLine` call and at the top of both quotient loops) and catching it inside `Search`, so callers never see it.
- `Solve` still returns `Result(null, [])` for S = G; the "no line fits" error is added in `Solve` and in the UI's finish step (skipped when S = G).
- The UI keeps the last drawn text and only assigns the Lines box when it changed, so the 100 ms timer doesn't flicker it.
- Tests added: 4 `Search` tests (100 total, all passing). The app (Debug and Release builds) was tried by hand and accepted.
