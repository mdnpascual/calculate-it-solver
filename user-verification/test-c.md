# Test C: game state scan (done, 2026-10-22)

The mod now publishes each settled puzzle to `%LOCALAPPDATA%\CalcHook\puzzle.json`, and the app (with **Follow game** checked) fills in S, G and the buttons and starts the Division Shortcut. Rebuild and reinstall the mod first:

1. Close the game. Run `dotnet build GameHook.MelonLoader -c Release` and copy the built `CalcHook.MelonLoader.dll` over the one in `<game>\Mods\` (Install step 5).
2. Start the app: `dotnet run --project CalculatorPuzzle`. Keep **Follow game** checked.
3. Start the game. After each check, copy the `[CalcHook]` lines from `MelonLoader\Latest.log`.

**C1. New run.** Start a new run and play two or three rounds, pressing buttons and buying from the shop.
- Expected: exactly one `puzzle ready` log line per round. The app fills in S, G and Buttons (with `:charges`) and searches once per round. Presses and purchases don't start another search.

**C2. Continue.** Quit mid-run, start the game, press Continue.
- Expected: only the restored puzzle (for example 4 → 24) is published. If the fresh round (for example 98 → 63) is published too, note the time gap between the two `puzzle ready` lines and tell me. That decides whether the 50 ms settle time or the flags need changing.

**C3. Reroll.** Use a reroll in the same round.
- Expected: a new goal in the same round publishes again, and the app restarts with it.

**C4. Charges.** Find a puzzle where a button has 0 or 1 charge left.
- Expected: no line in the app presses a button more often than its charges. The status says how many lines were rejected. A 0-charge button never appears in the Buttons box's search (it is left out).

Result: the app followed the game as expected (reported working). Also **Lock**: tick it to freeze the boxes, untick it to re-read `puzzle.json`.

Also report: any `error in` log lines, and whether `puzzle.json` ever shows a wrong S (for example the state after a press instead of the start).
