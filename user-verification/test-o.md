# Test O: The 999 999 cap and the 6-character display

Spec: [`spec/display-cap/`](../spec/display-cap/). Any round with the buttons at hand. Rebuild the solver (`dotnet build`) first.

1. **O1.** Press a `+` or `×` (or `X*2`, `<<`, `sqr`, `X+k`) so the result goes above 999 999. Example: `999 000`, `+`, `5000`, `=`.
   - Expected: the display shows `999999`, and the next press works.
2. **O2.** On a 6-character display (for example `123456`, or `000000` after `0` and five `X0`), press `Xd` or `dX`.
   - Expected: nothing changes.
3. **O3.** After an operator, press digit keys six times (`9 9 9 9 9 9`).
   - Expected: the sixth key restarts the operand: it shows `9`, not `999999`.
4. **O4.** If you own `p`: press `p` on `999999`.
   - Expected: `1000003`.
5. **O5.** Open the app on a puzzle that used to list a 7-digit line (for example a Modulo line with k = 6, or a `10 10 10 10` division).
   - Expected: no such line is listed any more, and every line shown plays in the game.

Result: not run yet.

Report: for each step, what the display showed. Mark this test done in its heading after the user reports.
