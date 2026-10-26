# Next prompt: Modulo lines in the results list

**Status:** Complete (2026-10-22).

Add Modulo Shortcut lines to the solver's Lines box. After the Division Shortcut search finishes, search Modulo Shortcut formulas over every factor pair, A = 1..99 and k, turn each valid one into presses with a cost, keep up to 100 with cost ≤ (cheapest division cost + 1), and merge them with the division lines ranked by cost, then drained (buttons the line would leave at 0 charges, fewest first), then spare (sum of the charges of the distinct buttons used). Every button in the Buttons box must now have a charge count. Last, remove the manual Modulo Shortcut rows (A, k, dropdown, Solution 1 and 2) from the UI. The repo is C#: .NET 9 WinForms + xUnit. Not a git repo; don't run `git init`.

## Read first

1. `CLAUDE.md`: project rules and the Engineering Calculator button table.
2. `spec/modulo-in-results/requirements.md` and `design.md`.
3. `spec/cost-per-line/requirements.md`: how a modulo line becomes presses.
4. `CalculatorPuzzle/ModuloShortcut.cs` (`Solve`, `Formula`, `FactorPair`), `EngineeringCalculator.cs` (`Key`, `Press`, `Buttons`, `Run`), `LeftEdits.cs` (`Plan`), `DivisionShortcut.cs` (`TypeOperand`, `Ranking`, `Line`), `MainForm.cs` (`StartDivisionSearch`, `OnSearchTick`, `Drain`, `StopDivisionSearch`).
5. `CalculatorPuzzle.Tests/` for the test style.

## Steps

1. Run `dotnet build` and `dotnet test` and note that everything passes.
2. Simulator: add `Key.Mod` and the `op` field in `EngineeringCalculator.Run` (design §1). Add the `mod` tests; all `÷` tests must still pass.
3. Charges: make `Buttons.Parse` require `:n` and add `Buttons.Spare` (design §2). Update the tests that parse buttons without counts, and turn the two "unlimited button" tests into tests for the new error. Add `Buttons.Drained` and the `Spare` and `Drained` tests.
4. Add `ModuloSearch.cs` (`Presses`, `Find`) and `ModuloSearchTests` (design §3).
5. Add `ResultLine.cs` with `Ranking`, and `ResultLineTests` (design §4).
6. `MainForm`: merged `found` list, the modulo phase after the division task, Esc and restart handling, the new row text and status, and the default Buttons text with counts (design §5).
7. Run the manual checks from the design's test plan with `dotnet run --project CalculatorPuzzle`.
8. Only once steps 2–7 work: remove the manual Modulo Shortcut rows from `MainForm` (design §6). Keep `ModuloShortcut.cs` and its tests. Build, test and run again.
9. Update the Status lines in all three spec files, add an "As built" section to the design if anything changed, and update `CLAUDE.md` (Status list: new files, the spec folder, charges now required).

## Check the work

- `dotnet build` with no warnings.
- `dotnet test`: all tests pass, old (updated for required charges) and new.
- Manual: division lines stream, then the modulo phase runs, rows show `(n presses, spare s[, empties b])` in cost, drained, spare order, Esc works in both phases, and a button without a count gives the new error.
- After step 8: no A, k, dropdown or Solution rows; the game feed still fills S, G and Buttons and starts the search.
