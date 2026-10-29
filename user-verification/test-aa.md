# Test AA: coinsub (`$++`) fallback

Checks the coinsub loop (`spec/coinsub-fallback/`). Install the freshly built `CalcHook.MelonLoader.dll` and run the app with Follow game on. The loop runs only when the normal search finds no line.

1. **AA1.** Own `operator_coinsub` (the button that pays coins to add 1). Look at the app's Buttons box and the mod overlay.
   - Expected: both show `$++` with its charges (for example `$++:5`). Note what the button face shows in the game (text or icon) and tell me.
2. **AA2.** Find a round where the app says `Searching… $++ (n press). Esc to stop.` and ends with a line that starts with `$++ (n press)`. Play that line (press coinsub n times, then the rest).
   - Expected: the round is won; the coins drop by n (by 2n in boss rule 10); the press count in the line is the number of presses you made (`=` not counted).
3. **AA3.** With $0, press coinsub once.
   - Expected: the display does not change and the button's charge drops by 1. The app shows no `$++` line for a round with $0.
4. **AA4.** (Boss rule 10, if met.) Press coinsub once with $5, then once with $1.
   - Expected: the first press costs $2 and adds 1; the second takes the last coin and the display does not change.
5. **AA5.** (Boss rule 14, if met.) Play a `$++ (n press)` line.
   - Expected: the goal rises by 1 on each coinsub press, the line still wins. With the blind passive the first press does not raise it (see Test AB).
6. **AA6.** Press Esc while the status says `Searching… $++ (n press)`.
   - Expected: the search stops, the status says `Stopped. 0 line(s) found.` and no further press is tried.
