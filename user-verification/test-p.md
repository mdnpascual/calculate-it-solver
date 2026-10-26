# Test P: Boss rules 2 and 9, blocked buttons (done, 2026-10-25; P1–P5 confirmed)

Spec: [`spec/boss-blocked-buttons/`](../spec/boss-blocked-buttons/). Rebuild the mod (`dotnet build GameHook.MelonLoader -c Release`) and the solver first. Use challenge 4 (every round is a boss) until the text reads ``You can`t use buttons "..." and "..."``.

1. **P1.** Open `%LOCALAPPDATA%\CalcHook\puzzle.json`. Write down the two named buttons.
   - Expected: `"blocked": true` on those two buttons, `false` on the others.
   - Result (2026-10-25, done): round 4, S 80, G 73. `puzzle.json` had `"blocked": true` on `0`, `×` and `2` (three, since `2` was already empty), `false` on the rest.
2. **P2.** Look at the app (Follow game on).
   - Expected: both buttons show `:0` in the Buttons box; the status ends with `blocked: <A>, <B>` and has no `boss rule 2 not supported`.
   - Result (2026-10-25, done): Buttons box `5X:2, 1:3, 2:0, 3:2, +:1, X4:1g, 4:2, 5:2, 6:3, -:2, 7:3, 8:2, 9:2, ×:0, last:2, 0:0, ÷:2, mod:2`.
3. **P3.** Play the top line.
   - Expected: it doesn't press A or B, and wins the round.
   - Result (2026-10-25, done): top line `-` `7` `=` (80 - 7 = 73), won the round.
4. **P4.** Go to the next round (not a boss).
   - Expected: no `blocked` note; the two buttons are back with their charges.
   - Result (2026-10-25, done): next round was boss rule 14, and no `blocked` note appeared.
5. **P5.** (Rule 9, if a round reads ``You can't use legendary buttons``; easier with a legendary button bought.)
   - Expected: the Boss box shows 9, no `boss rule 9 not supported` note, `puzzle.json` has `"blocked": true` on the legendary buttons, and the top line wins the round.
   - Result (2026-10-25, done): confirmed.

Result: all steps done 2026-10-25.

Report: for each step, what you saw. Mark this test done in its heading after the user reports.
