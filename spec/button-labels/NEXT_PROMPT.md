# Next prompt: implement button labels

Implement `spec/button-labels/`. The solver app and the mod overlays show the in-game button labels (`volt12`, `unk8`, `mem5`, `$`, `reverse`, `->)`, `combine`, `X²`, `_2_`, `mirror`, `equalize`, `X<<`, `X>>`, `+-10`, `x->25`, `x->0`) instead of the solver tokens. Internal tokens don't change; the Buttons and Required boxes accept both forms.

The requirements' "Decisions" section settles the `round`, `reset`, empty mw and `x`-case questions; use the table as written.

## Read first

1. `spec/button-labels/requirements.md` and `design.md`.
2. `CalculatorPuzzle/README.md` ("Files").
3. `CalculatorPuzzle/GameLink.cs` (`MapButton`, `Parse`, `ButtonsText`), `EngineeringCalculator.cs` (`Buttons.Parse`), `ResultLine.cs` (`ToString`), `MainForm.cs` (Required box, `ApplyGamePuzzle`, status notes: grep `use {`, `no line can use`, `blocked:`, `mw empty`).
4. `GameHook.MelonLoader/ButtonSymbols.cs`, `CalcHookCore.cs` (`SymbolOf`, `SymbolFontSize`), `CalculatorPuzzle.Tests/ButtonSymbolsTests.cs`.

## Steps

1. `ButtonLabels.cs` (`Label`, `Token`, `Line`) and `ButtonLabelsTests.cs`.
2. `Buttons.Parse` and the Required box: `ButtonLabels.Token` on each entry. Parse tests with labels.
3. `ResultLine.ToString`, `GameLink` `ButtonsText`, the `MainForm` status notes: labels. Update expected strings in existing tests.
4. Mod: `ButtonSymbols.Label`, `Of` and the live overloads, `SymbolOf` for the shop mw. Update `ButtonSymbolsTests`. `dotnet build GameHook.MelonLoader -c Release`.
5. Docs listed in the design's "Docs to update when implemented", and the `spec/README.md` line.
6. Add Test X as `user-verification/test-x.md` plus a row in `user-verification/README.md` (next free letter if X is taken). Steps:
   - X1: overlay with `operator_sqr` (F9 in any mode). Expected: the cell shows `X²` with a real superscript 2, not a box or `X?`. If it fails, set `sqr`'s label back to `sqr` (one entry in both copies).
   - X2: solver app with `sqr` in Buttons. Expected: Follow game writes `X²:n`; a line with it shows `X²`.
   - X3: charge, quest and mw keys. Expected: overlay `volt12`, `unk8`, `mem5` (`mem` while empty); the Lines box uses the same text.
   - X4: one round with several relabelled buttons. Expected: every cell matches the in-game label (`->)` for round, `x->0` for reset); `equalize` and `combine` fit in their cells.
   - X5: type old tokens (`c12:12, flip:1`) in the Buttons box and press Enter. Expected: same lines as with labels.

## Check

- `dotnet build` and `dotnet test`: all tests pass (count before this spec in `spec/README.md`'s last entries; write the new count there).
- Mod builds in Release.
