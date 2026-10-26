# Test R: Boss rule 13, one `=` per line (done, 2026-10-25; R1–R4 confirmed)

Spec: [`spec/boss-one-send/`](../spec/boss-one-send/). Rebuild the solver first. Use challenge 4 (every round is a boss) until the text reads `After clicking the "Send" button, the required number changes`.

1. **R1.** Look at the app (Follow game on).
   - Expected: the Boss box shows 13, the status starts `Rule 13: one = per line:`, and no line has an `=` before its last press.
2. **R2.** Press a top line that ends with `op D =`.
   - Expected: the round is won on that `=`.
3. **R3.** (If one comes up.) Press a line of edits only, then `=`.
   - Expected: the round is won, no re-roll.
4. **R4.** Press a line's middle `=` on purpose (any `op D =` that misses).
   - Expected: the goal changes, and within a second the app shows lines for the new goal from the current display.

Report: for each step, what you saw. Mark this test done in its heading after the user reports.
