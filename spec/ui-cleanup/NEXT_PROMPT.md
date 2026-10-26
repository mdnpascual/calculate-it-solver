# Next prompt: UI cleanup

**Status:** Done (2026-10-26).

Clean up the solver app's window, line format and status messages in this repo (C#, .NET 9, WinForms, xUnit). No solver change.

## Read first

1. `CLAUDE.md`: project rules.
2. `spec/ui-cleanup/requirements.md`: changes 1–16 and the lists of what stays.
3. `spec/ui-cleanup/design.md`: where each change goes and the tests.
4. `CalculatorPuzzle/README.md` ("Files"): the notes for `MainForm.cs`, `ResultLine.cs`, `ButtonLabels.cs`.
5. `CalculatorPuzzle/MainForm.cs` (field initializers, the constructor's layout, `StartDivisionSearch`, the tick handlers, `OnModuloDone`, `DoneStatus`), `ResultLine.cs`, `ButtonLabels.cs` (`Line`), `EngineeringCalculator.cs` (`Buttons.Parse` errors), `CalculatorPuzzle.Tests/ButtonLabelsTests.cs`, `AchievementTests.cs`.

## Steps

1. Run `dotnet test` first and note that everything passes, so later failures are yours.
2. Lines: `ButtonLabels.Line` joins with ` | `; `ResultLine.ToString` uses `press` / `empty` and drops the credit tag.
3. `Buttons.Parse`: the five error texts.
4. `MainForm.cs`: captions; Lines box anchor, width and scrollbars; Boss / Last / Money as 0; remove the notes, prefixes, no-line variants and search error messages (design "Items 11–16").
5. Update and add the tests (design "Test plan").
6. Update `CalculatorPuzzle/README.md` and this spec's line in `spec/README.md`. Set the Status line in all three spec files.

## Check the work

- `dotnet build` with no warnings.
- `dotnet test`, all passing.
- `dotnet run --project CalculatorPuzzle` and the manual checks at the end of the design.
