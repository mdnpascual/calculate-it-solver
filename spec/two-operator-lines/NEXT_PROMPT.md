# Next prompt: Two-operator lines

**Status:** Complete (2026-10-24). Follow-up: after Test M2 passes, remove `MainForm.BossTestMarker`.

The Calculate It solver (C# .NET 9 WinForms + xUnit) only builds lines with one operator. Some puzzles need two, for example boss rule 11 requiring `+` on 59 → 40 with no `dX` / `Xd`: the answer is `- 2 0 = + 1 =`, and today the app shows nothing. Add a general two-operator phase, `TwoOperatorSearch` (meet in the middle on the value after the first `=`), run it after the arithmetic phase in every round (and per k in boss rule 14), and mark every two-operator line with a temporary `TEST M: report back` text so the user can test it in-game.

## Read first

1. `CLAUDE.md` (the calculator model, Specs, user-verification rules).
2. `spec/two-operator-lines/requirements.md` and `design.md`.
3. `CalculatorPuzzle/README.md` ("The tricks", "Search order", "Files").
4. `CalculatorPuzzle/ArithmeticSearch.cs` (the `xs` / `ys` building, `Keep`, cost-level build loop, `RemorphKeys`): the new search follows it and shares the `xs` / `ys` code.
5. `CalculatorPuzzle/RequiredPlan.cs`, `ResultLine.cs`, `GoalStepSearch.cs`, `MainForm.cs` (`StartSearch` task body around `ArithmeticSearch.Find`, `OnSearchTick`, `DoneStatus`), `EngineeringCalculator.cs` (`Run`, operator arithmetic, `Buttons.Has`, `Fits`, `Uses`), `DivisionShortcut.cs` (`TypeOperand`, `CheapestDivisor`, `Pieces`).
6. `CalculatorPuzzle.Tests/RequiredPlanTests.cs` and the `ArithmeticSearch` tests, for test style.

## Steps

1. `dotnet build`, `dotnet test`; note the pass count.
2. Move the `xs` / `ys` building out of `ArithmeticSearch.Find` into shared helpers; tests still pass unchanged.
3. `TwoOperatorSearch` (design "Algorithm"), with `TwoOperatorSearchTests` 1 and 3–9 from the design's test plan.
4. `RequiredPlan` (`TwoOperator`, `RequiredOp`) and its tests.
5. `ResultLine` (field, `From`, kind order, `TwoOperatorMarker`) and its tests.
6. `MainForm` wiring (phase order, division cap, rejected count, `DoneStatus`) and test 2 (end-to-end 59 → 40).
7. `GoalStepSearch` wiring and its test.
8. Speed check: 41 → 483 with every button, Release, before and after (record in design "As built").
9. `CalculatorPuzzle/README.md`: a `TwoOperatorSearch.cs` file note, update the `ResultLine.cs`, `RequiredPlan.cs`, `GoalStepSearch.cs`, `MainForm.cs` notes, a "4. Two operators" section in "The tricks", and the new phase in "Search order".
10. Add Test M as `user-verification/test-m.md` (from the design's "Manual test" section; heading `# Test M: Two-operator lines`, steps M1–M3 each with an "Expected" line) and a row `| M | [Two-operator lines](test-m.md) |` in `user-verification/README.md`. Then stop and ask the user to run it and report back.
11. After the report: remove `TwoOperatorMarker` (and its test), mark Test M done, set the Status lines to complete, write "As built" in the design, update the `spec/README.md` line.

## Check the work

- `dotnet build` with no warnings; `dotnet test` all pass.
- `dotnet run --project CalculatorPuzzle`, Follow game off: S `59`, G `40`, Buttons `1:1, 2:2, 3:2, 4:2, 5:2, 6:2, 7:2, 8:3, 9:2, 0:2, +:3, -:2, ×:2, ÷:2, round:1g, 0->8:2`, Required `+`. Hand-typed Required is Market Demand mode, so `- 1 9 =` (3, `no +`) shows last and 5-press lines with `+` show first, ending in `TEST M: report back`. For boss mode, save the original `puzzle.json` (in `requirements.md`) to `%LOCALAPPDATA%\CalcHook\puzzle.json` with a bumped `seq` and Follow game on: only lines with `+`, including `- 2 0 = + 1 =`.
