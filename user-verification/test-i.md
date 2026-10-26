# Test I: Prime button (done, 2026-10-24; I1–I3 confirmed)

The overlay now shows `operator_prime` as `p<nearest prime>` (the mod computes it itself); the solver maps the button to `p`. Rebuild and reinstall the mod first (as in Test C). Already confirmed, no test needed: `p` works on the left side only, and the round is won after `=`.

1. **I1.** Get `operator_prime` (shop, or a run that has it). Look at the calculator overlay (F9 if not in challenge 9) and hover the real button.
   - Expected: the cell reads `p<number>`, the number equals the one in the game's hover text `(...)` (`8084` shows `p8081`), and it changes when the left side changes.
2. **I2.** In challenge 9, open the shop with an `operator_prime` offer.
   - Expected: the offer shows `p`.
3. **I3.** Start the app with Follow game on a puzzle that has `operator_prime`. Follow a line that uses `p`, then press `=`.
   - Expected: Buttons shows `p:N`, the line passes the simulator check and wins the round. A tie case (`8084` → `8081`, `93` → `89`) goes down.
4. Copy any `error in` lines from `MelonLoader\Latest.log`, including `error in OnGUI`.
