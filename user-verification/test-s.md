# Test S: Warp re-scan (done, 2026-10-25; S1–S3 and S6 confirmed, S4 and S5 skipped: boss rounds with the warp button are hard to get)

Spec: [`spec/warp-rescan/`](../spec/warp-rescan/). Rebuild the mod and the solver first (`dotnet build GameHook.MelonLoader -c Release`, copy the DLL to the game's `Mods` folder). Follow game on, Lock off. You need a round where the warp button (d20) is on the calculator.

1. **S1.** Press the warp in a normal round.
   - Expected: `Latest.log` has one `warp 1: buttons re-rolled` and one `puzzle ready: …, warp 1` line; `puzzle.json` has `"warp": 1`, the new buttons each with `uses` 1 and the warp's own `uses` one lower; the app's Buttons box matches the screen, a new search runs, and the status ends with `warp 1`.
2. **S2.** Press the warp again.
   - Expected: `warp` 2 in the file, another solve.
3. **S3.** Go to the next round.
   - Expected: `warp` 0 in `puzzle.json`.
4. **S4.** (Boss rule 14, if it comes up.) Press the warp.
   - Expected: the goal in the file is one higher than before the press, and the app solves again.
5. **S5.** (Boss rule 2, if it comes up.) Press the warp.
   - Expected: the two newly blocked buttons have `"blocked": true` and the status lists them.
6. **S6.** Press the warp after an operator (for example `÷`, then warp).
   - Expected: nothing fixed. Write down what the display shows and what the app solves from.
   - Result: the warp button is disabled while an operator is pending (after `+`), so a warp can only happen on the left side.

Report: for each step, what you saw. Mark this test done in its heading after the user reports.
