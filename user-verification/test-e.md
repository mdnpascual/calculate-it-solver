# Test E: Market Demand

The mod now reports `challenge` and `required` in `puzzle.json` (and `requiredLabel`, `requiredUses` in `state.json`). The app has a Required box above Buttons and ranks lines that press that button first. Rebuild and reinstall the mod first (as in Test C), and start the app with **Follow game** on.

1. **E1.** Start a Market Demand run. Open `%LOCALAPPDATA%\CalcHook\puzzle.json`.
   - Expected: `"challenge": 8` (confirms the id) and `"required"` is the id of the button named in the rule text under the coins (`Use button "÷", ...` → `"operator/"`). The app's Required box shows its token (`÷`).
2. **E2.** Look at the Lines box.
   - Expected: lines that press the required button come first, whatever the cost. The others end in `, no <token>`. The done status ends with `K use <token>`.
3. **E3.** Play a few rounds and note which buttons get picked.
   - Expected: sometimes a left-side button (`X1`, `4X`, `flip`, a transform). If one is picked, report it; that answers open question 2. If only operators ever appear, report that too.
4. **E4.** Save and quit mid-round, then Continue.
   - Expected: `"required"` is the same after the restore.
5. **E5.** Start a normal run (not Market Demand).
   - Expected: `"challenge"` is not 8, `"required"` is `null`, the Required box is empty and the results are as before.
6. Copy the `[CalcHook]` lines (they end in `required=<id>` when set) and report any `error in` lines from `MelonLoader\Latest.log`.
