# Test X: button labels (done, 2026-10-26: `X²` renders in overlay and solver, `mem` when empty and `mem2` after assigning, all relabelled buttons seen)

Spec: `spec/button-labels/`. Rebuild and reinstall the mod (`dotnet build GameHook.MelonLoader -c Release`, then [install-and-remove.md](install-and-remove.md)) and rebuild the app (`dotnet build`).

1. **X1 `²` in the overlay.** Open the overlay (F9 in any mode) in a round where the calculator has `operator_sqr`.
   - Expected: the cell shows `X²` with a real superscript 2, not a box or `X?`. If it fails, set `sqr`'s label back to `sqr` (one entry in `ButtonLabels.Fixed` and in `ButtonSymbols.Fixed`).
2. **X2 `X²` in the solver app.** Run the solver app with `sqr` in the Buttons box (Follow game on).
   - Expected: Follow game writes `X²:n`; a line that uses it shows `X²`. If the box shows a box or `?` instead of `²`, revert as in X1.
3. **X3 charge, quest and mw keys.** Play a round with the charge, quest and mw keys.
   - Expected: the overlay shows `volt12`, `unk8`, `mem5` (`mem` while the mw key is empty); the Lines box uses the same text.
4. **X4 one round with several relabelled buttons.** Compare each overlay cell with the in-game button.
   - Expected: every cell matches the in-game label (`->)` for round, `x->0` for reset, `$`, `reverse`, `mirror`, `combine`, `_2_`, `+-10`, `X<<`, `X>>`, `x->25`); `equalize` and `volt12` fit in their cells.
5. **X5 old tokens still parse.** Type old tokens (`c12:12, flip:1`) in the Buttons box and press Enter.
   - Expected: the same lines as with labels (`volt12:12, reverse:1`).
6. Copy any `error in` lines from `MelonLoader\Latest.log`.
