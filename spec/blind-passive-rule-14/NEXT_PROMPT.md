# Next prompt: blind passive in boss rule 14 (bugfix)

Implement `spec/blind-passive-rule-14/`. With the `passive_blind` passive, the round's first non-glass press is free. In boss rule 14 that press doesn't raise the goal, so a line with k charged presses ends at G + k − 1. The solver wrongly aims at G + k today.

## Read first

1. `spec/blind-passive-rule-14/requirements.md` and `design.md`.
2. `spec/boss-goal-per-charge/requirements.md` (rule 14) and `spec/near-one-passive/design.md` (the same mod and checkbox pattern).
3. Code: `CalculatorPuzzle/GoalStepSearch.cs`, `CalculatorPuzzle/SwapSearch.cs` (`FlippedStart`, `Flip`, `Check`), `EngineeringCalculator.Run` (the charged-press block and `LineContext`), `GameLink.cs`, `MainForm.cs` (`near1Box`, `ApplyGamePuzzle`, where `LineContext` is built).
4. Mod: `GameHook.MelonLoader/CalcHookCore.cs` (`CheckPuzzle`, `Near1`, `WritePuzzle`, the `state.json` writer) and its README. The mod must stay read-only.
5. `CalculatorPuzzle/README.md` ("Files").

## Steps

1. `LineContext.Blind` and the simulator change; tests 1–2 of the design.
2. `GoalStepSearch.Raises` and the `goal + k − free` target; tests 3–6.
3. `SwapSearch`; test 7.
4. `GameLink` `blind`; test 8.
5. `MainForm` Blind checkbox. No new status text and no line tag.
6. Mod: `blind` in both JSON files and the log; republish on a `passive_blind` ownership change, never on the flag change. `dotnet build GameHook.MelonLoader -c Release`.
7. Docs: `CalculatorPuzzle/README.md` (the changed files), `GameHook.MelonLoader/README.md` (`blind`), `docs/boss-rules.md` rule-14 row (the blind note), the `spec/README.md` line.
8. Add Test AB as `user-verification/test-ab.md` plus a row in `user-verification/README.md` (steps in the design; if AB is taken, the next free letter).

## Check

- `dotnet build` and `dotnet test`: every existing test passes, plus the new `BlindTests.cs`.
- App, by hand: S 41, G 46, Boss 14, Buttons `÷:9, 10:9, X8:9, =`, Blind ticked → `÷ | 10 | = | X8` with `goal 48`. Untick Blind → no line for 46 (G 45 finds it). The status reads as before.
