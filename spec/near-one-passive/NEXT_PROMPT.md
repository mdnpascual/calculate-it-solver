# Next prompt: implement the near-one passive

Implement `spec/near-one-passive/`. With the game's `passive_near1` passive, a sent number within 1 of the goal wins. The mod publishes `near1`, and the solver then searches G − 1, G and G + 1 and ranks all the lines together, with the ranking unchanged.

## Read first

1. `spec/near-one-passive/requirements.md` and `design.md`.
2. `CalculatorPuzzle/README.md` ("Search order and truncation", "Files").
3. `CalculatorPuzzle/MainForm.cs` (`StartDivisionSearch`, `OnSearchTick`, `OnModuloDone`, `OnStepTick`, `DoneStatus`, `OnGameFileChanged`, `ApplyGamePuzzle`).
4. `CalculatorPuzzle/GoalStepSearch.cs`, `CalculatorPuzzle/ResultLine.cs`, `CalculatorPuzzle/GameLink.cs`, the `plusOne && start == t` branch of `ArithmeticSearch.Find`.
5. `GameHook.MelonLoader/CalcHookCore.cs` (`CheckPuzzle`, `WritePuzzle`, the `state.json` writer, the warp `_pending` pattern) and `GameHook.MelonLoader/README.md`. The mod must stay read-only.

## Steps

1. `NearGoals.Targets` and its tests (`NearOneTests.cs`).
2. `ArithmeticSearch.Find(..., bareSend)` for the 0-press `=` line when S is within 1 of G.
3. `GameLink`: `GamePuzzle.Near1`, parse `near1`; test.
4. `GoalStepSearch.Search(..., near1)`: loop the targets per k; tests.
5. `MainForm`: Near 1 checkbox, phase-major target loops with a shared cap, `Reaches` for non-G targets, status prefix, rule-14 same-round guard on a `near1` change.
6. Mod: `near1` in the fingerprint, `puzzle.json`, `state.json` and the log; republish when it changes. `dotnet build GameHook.MelonLoader -c Release`.
7. Update `CalculatorPuzzle/README.md`, `GameHook.MelonLoader/README.md`, `docs/boss-rules.md` row 13, and the `spec/README.md` line.
8. Add Test U as `user-verification/test-u.md` plus a row in `user-verification/README.md` (Test T is planned by `achievement-modes/`; if T exists by then, take the next free letter). Steps:
   - U1: own `passive_near1`; `puzzle.json` has `"near1": true` and the app ticks Near 1. Without it, `false`.
   - U2: pick the passive at a perk picker; the puzzle is republished with `near1: true` (both orders of picker and publish).
   - U3: play a `goal N` line (G ± 1) from the Lines box; the round is won.
   - U4: S one off from G: the bare `=` wins.
   - U5 (if met): rule 14 or rule 12 round with Near 1; a `goal N` line wins.

## Check

- `dotnet build` and `dotnet test`: all tests pass (607+ before this spec).
- Release timing for 41 → 483 with every button, Near 1 on and off. Write both in the design's "As built" section.
