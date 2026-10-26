# Test L: Boss goal per charge (done, 2026-10-24; L1–L3 confirmed, L4 partly: `round:1g` parsed, glass press untested)

1. Rebuild and reinstall the mod (`dotnet build GameHook.MelonLoader -c Release`); start the app with Follow game on. Play challenge 4 (every round is a boss) until the rule text says `The needed number increases by 1 each time the charge is spent.`
2. **L1.** Look at `%LOCALAPPDATA%\CalcHook\puzzle.json`.
   - Expected: `"bossRule": 14`. The app's Boss box shows 14. (Confirmed.)
3. **L2.** Press the top line shown in the app (then `=`). Each line shows `goal N`.
   - Expected: each charged press raises the in-game goal by 1 (not `=`, not glass); the goal after the last charged press is the `goal N` of the line, and `=` ends the round. This answers whether the raise comes before the check on `=`. (Confirmed: `>>` took the goal 18 → 19 in `state.json`, and `=` ended the round.)
4. **L3.** While pressing the line, watch `seq` in `puzzle.json` and the app's Lines box.
   - Expected: `seq` doesn't change during the round, and the Lines box stays. After the round the next round is solved as usual. (Confirmed: `seq` stayed 3 through the press and `=`.)
5. **L4.** (Only if a glass button shows up in a rule-14 round.) Look at the Buttons box and press the glass button.
   - Expected: the Buttons box shows it as `:1g`, and pressing it doesn't raise the goal. (Partly: the Buttons box showed `round:1g`; pressing the glass button was not tested.)
6. Copy any `error in` lines from `MelonLoader\Latest.log`.
