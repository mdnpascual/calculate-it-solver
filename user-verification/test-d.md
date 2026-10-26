# Test D: glass buttons

The mod now reports `glass` and `legendary` per button, and the app clamps a glass button to 1 charge. Rebuild and reinstall the mod first (as in Test C).

1. **D1.** Get a glass button (buy one when the shop offers glass). Open `%LOCALAPPDATA%\CalcHook\puzzle.json`.
   - Expected: that button has `"glass": true` and its raw `uses`. In the app (Follow game on) the Buttons box shows it at `:1`. Normal buttons show `"glass": false`.
2. **D2.** Press the glass button once in the game.
   - Expected: it breaks. No second `puzzle ready` line appears (same round and goal).
3. **D3.** If a legendary button shows up, check `"legendary": true` for it in `puzzle.json`.
4. Copy the `[CalcHook]` lines from `MelonLoader\Latest.log` (glass buttons end in `g`, legendary in `L`) and report any `error in` lines.
