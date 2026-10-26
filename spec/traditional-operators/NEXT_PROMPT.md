# Next prompt: Traditional operators (+, −, ×)

**Status:** Done (2026-10-22). Kept for reference.

Add the game's `+`, `-` and `×` buttons to the calculator puzzle solver. The simulator learns the three operators (one operation per `=`, `-` clamps at 0). A new `ArithmeticSearch` finds one-operator lines (edit S into X, operator, operand D, `=`, edit the result Y into G, with D = Y − X, X − Y or Y / X) and edits-only lines (S → G with left edits alone, such as `flip` for 36 → 63). It runs before the Division Shortcut, and its lines join the merged Lines box. The repo is C#: .NET 9 WinForms + xUnit. Not a git repo; don't run `git init`.

## Read first

1. `CLAUDE.md`: project rules and the Engineering Calculator button table.
2. `spec/traditional-operators/requirements.md` and `design.md`.
3. `CalculatorPuzzle/EngineeringCalculator.cs` (`Key`, `Press`, `Buttons`, `Parse`, `Run`), `GameLink.cs` (`MapButton`), `DivisionShortcut.cs` (`SearchCore`, `TypeOperand`), `LeftEdits.cs` (`Plan`), `ModuloSearch.cs` (the pattern to follow), `ResultLine.cs`, `MainForm.cs` (`StartDivisionSearch`, `OnSearchTick`, `Drain`, `DoneStatus`).
4. `CalculatorPuzzle.Tests/` for the test style (`TestButtons.Parse` adds `:9` to tokens without a count).

## Steps

1. Run `dotnet build` and `dotnet test` and note that everything passes.
2. Keys, `Buttons` (optional `Add`, `Subtract`, `Multiply`, `Has`), `Parse` tokens and the simulator (design §1–2). Add the simulator and parse tests.
3. `MapButton` (design §3) and its tests.
4. Move the candidate builders out of `DivisionShortcut.SearchCore` (design §4). All division tests must still pass.
5. Add `ArithmeticSearch.cs` and `ArithmeticSearchTests` (design §5).
6. `ResultLine`: third kind and ranking (design §6), with tests.
7. `MainForm`: arithmetic before division in the same task, `ResultLine` queue, counts by kind, status, default Buttons text (design §7).
8. Run the manual checks from the design's test plan with `dotnet run --project CalculatorPuzzle`.
9. Update the Status lines in all three spec files, add an "As built" section to the design if anything changed, and update `CLAUDE.md` (button table: `+`, `-`, `×`; Status list: `ArithmeticSearch.cs`, this spec folder).

## Check the work

- `dotnet build` with no warnings.
- `dotnet test`: all tests pass, old and new.
- Manual: 55 → 77 on a plain calculator shows `+ 2 2 =` first; 36 → 63 with `flip` shows `flip`; division and modulo phases still run after the arithmetic lines; Esc still stops the search.
