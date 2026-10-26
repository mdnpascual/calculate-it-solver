# Test U: Near-one passive (done, 2026-10-25; U1–U3 confirmed, U4 checked in the app only, U5 not met)

1. Rebuild and reinstall the mod, then rebuild the app (`dotnet build`). Use Follow game.
2. **U1.** Play a run where you own `passive_near1`. Look at `%LOCALAPPDATA%\CalcHook\puzzle.json` and the app.
   - Expected: `"near1": true`, and the Near 1 box is ticked. Without the passive: `"near1": false`, box unticked.
3. **U2.** Pick the passive at a perk picker (every 5 rounds). Try to see both orders: picker before the puzzle is published, and picker after.
   - Expected: the puzzle is published again with `"near1": true` (the `seq` goes up; the log says `near1`) and the app re-searches.
4. **U3.** With Near 1 on, play a line from the Lines box that shows `goal N` (G ± 1).
   - Expected: the round is won.
5. **U4.** Find or set up a round where S is one off from G.
   - Expected: the Lines box shows `=` (0 presses, `goal S`); pressing `=` wins.
   - Result: the game wins such a round by itself, so this can't be played. Typing S 17, G 18 in the app (Near 1 ticked) shows the `=` line with 0 presses.
6. **U5.** (If met) Do U3 in a boss rule 14 or rule 12 round with Near 1.
   - Expected: a `goal N` line wins.
7. Copy any `error in` lines from `MelonLoader\Latest.log`.
