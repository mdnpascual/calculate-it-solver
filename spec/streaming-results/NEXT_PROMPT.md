# Next prompt: Streaming results

**Status:** Complete. Can be done before or after [digit-keys](../digit-keys/NEXT_PROMPT.md). If digit-keys is already done, also add the cancel check to its Path B loop.

Make the Division Shortcut search in this repo (C#, .NET 9, WinForms, xUnit) run in the background, show lines as they're found, and stop on Esc or a new Enter.

## Read first

1. `CLAUDE.md`: project rules.
2. `spec/streaming-results/requirements.md` and `design.md`.
3. `CalculatorPuzzle/DivisionShortcut.cs`, `CalculatorPuzzle/MainForm.cs` and `CalculatorPuzzle.Tests/DivisionShortcutTests.cs`.

## Steps

1. Run `dotnet test` first and note that everything passes.
2. `DivisionShortcut.cs`: split `Solve` into `Search` (callback + cancellation token) and a `Solve` wrapper, and expose `Ranking`. Keep `Solve`'s results identical.
3. Add the `Search` tests from the design's test plan.
4. `MainForm.cs`: background task, the queue and timer, Esc handling, Enter restarting the search, and stopping on close.
5. Update the Status section of `CLAUDE.md`, and set the Status lines in all three spec files (add an "As built" section to the design if anything differs).

## Check the work

- `dotnet build` with no warnings.
- `dotnet test` with all tests passing, including every existing test, unchanged.
- `dotnet run --project CalculatorPuzzle`:
  - Press Enter with a slow input (for example S = 531, G = 11 with the default buttons). The form stays responsive, lines appear while the status says `Searching…`, and the final list matches what the app showed before this change.
  - Press Esc while it runs. The status says `Stopped. N line(s) found.` and the lines stay.
  - Press Enter twice quickly. Only the second search's lines show, with no duplicates and no leftovers.
  - Close the window during a search. The process exits.
- If anything in the design doesn't hold up (for example, the timer redraw flickers), fix it and note it in "As built". Don't drop a requirement.
