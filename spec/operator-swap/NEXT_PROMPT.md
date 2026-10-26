# Next prompt: implement operator swap

Implement `spec/operator-swap/`. The game's `operator_swap` button trades the display and the goal, then sends. When the puzzle has it, the solver solves S → G as today, then G → S with `swap` as the first press (cost + 1), at most one `swap` per line, and ranks both sets of lines together.

## Read first

1. `spec/operator-swap/requirements.md` and `design.md`.
2. `CalculatorPuzzle/README.md` ("Search order and truncation", "Files").
3. `CalculatorPuzzle/EngineeringCalculator.cs` (`Key`, `Press`, `Buttons.Parse`, `Buttons.Token`, `Run`, `Sent`, `Fits`, `Drained`, `Spare`).
4. `CalculatorPuzzle/MainForm.cs` (`StartDivisionSearch`, `OnSearchTick`, `OnModuloDone`, `DoneStatus`), `RequiredPlan.cs`, `GoalStepSearch.cs`, `ResultLine.cs`, `GameLink.cs` (`MapButton`).
5. `GameHook.MelonLoader/ButtonSymbols.cs` and `CalculatorPuzzle.Tests/ButtonSymbolsTests.cs`.
6. Optional, to check a game detail: `ilspycmd -t MainCalc "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"` (`Swap`, `Send`, `Count`) and `-t CountUses` (`SubUse`).

## Steps

1. `Key.Swap`, the `swap` token in `Buttons.Parse` / `Token`, `Press` printing, `Run` rejecting `swap`, `Buttons.Without`. Tests.
2. `SwapSearch.cs`: `Leg`, `Legs`, `FlippedStart`, `Check`, `Wrap`. Tests in `SwapTests.cs`.
3. `ResultLine`: `Swapped`, `Reaches` comment.
4. `MainForm`: legs inside each phase with a shared cap (flipped cap − 1), the modulo task and rule 14 `stepTask` per leg, the 1-press `[swap]` line when F = S, `swap line(s)` in the status.
5. `GameLink.MapButton` and `ButtonSymbols`: `operator_swap` → `swap`; update `ButtonSymbolsTests`. `dotnet build GameHook.MelonLoader -c Release`.
6. Update the docs listed in the design's "Docs to update when implemented", and the `spec/README.md` line.
7. Add Test W as `user-verification/test-w.md` plus a row in `user-verification/README.md` (T, U and V are planned by `achievement-modes/`, `near-one-passive/` and `near-median-sup-money-equal-mw/`; if W is taken by then, take the next free letter). Steps:
   - W1: own `operator_swap`; `puzzle.json` lists it, the app shows `swap:n` in Buttons, the overlay shows `swap`.
   - W2: press swap at the start of a round. Expected: the display shows the old goal, the goal shows the old start, 1 charge is used. After an operator and a digit key, swap can't be pressed.
   - W3: play a `swap …` line from the Lines box. Expected: the round is won.
   - W4: after pressing swap, the mod publishes the swapped puzzle (G → S) and the app's new list contains the rest of the line.
   - W5 (if met): rule 12, swap shows G + 1; rule 14, swap shows G + 1 (the goal rose before the swap); rule 13, swap re-rolls the goal.
   - W6: press swap after an `=` mid-line. Expected: it works (this is the mid-line follow-up; the solver doesn't use it).

## Check

- `dotnet build` and `dotnet test`: all tests pass (607+ before this spec).
- Release timing for 41 → 483 with every button, with and without `swap:1`. Write both in the design's "As built" section.
