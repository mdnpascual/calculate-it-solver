# Test K: Boss required button (done, 2026-10-24; K1–K5 confirmed)

1. Rebuild and reinstall the mod (`dotnet build GameHook.MelonLoader -c Release`); start the app with Follow game on.
2. **K1.** Play a normal run (not challenge 8) to a boss round (round 5, 10, 15 or 20) until the rule text says `You must use button "…"`. Look at `%LOCALAPPDATA%\CalcHook\puzzle.json`.
   - Expected: `required` is that button's id and `requiredKind` is `boss`. The Required box shows its token, every line shown presses it, and no other lines appear. If the button is `÷`, `mod`, `+`, `-`, `×`, `%`, `a->c` or `b->c`, only lines of that phase appear.
3. **K2.** Follow a line shown, then press `=`.
   - Expected: the round ends. Optional: reach the goal without pressing the button; the round should not end (confirms the penalty).
4. **K3.** Reach a boss round with another rule.
   - Expected: `required` is `null` and `requiredKind` is `null` in `puzzle.json`; `bossRule` in `state.json` shows the rule number.
5. **K4.** Start challenge 8 (Market Demand).
   - Expected: `requiredKind` is `market`; lines without the button still appear, marked `no <token>` (Test E unchanged).
6. **K5.** Play the round after the boss.
   - Expected: `required` is `null` again.
7. Copy any `error in` lines from `MelonLoader\Latest.log`.
