# Test W: operator_swap (2026-10-26: W1, W2, W3 and W4 confirmed in one round: swap at the start, the app re-solved the swapped puzzle, the rest of the line won; W6 confirmed: swap after `=` works and the round was won on the re-solved list; W5 not met)

Spec: `spec/operator-swap/`. `operator_swap` is rare, so do each step when a round gives it; "not met" is fine.

1. **W1 own the button.** Rebuild and reinstall the mod (`dotnet build GameHook.MelonLoader -c Release`, then [install-and-remove.md](install-and-remove.md)) and rebuild the app (`dotnet build`). Own `operator_swap` and start a round.
   - Expected: `puzzle.json` lists `operator_swap`; the app's Buttons box shows `swap:n`; the overlay (F9) shows `swap`.
2. **W2 press swap at the start of a round.** Press it before any other button. Then, in another round, press an operator and a digit key first.
   - Expected: the display shows the old goal, the goal shows the old start, 1 charge is used. After an operator and a digit key, swap can't be pressed.
3. **W3 play a swap line.** Pick a line that starts with `swap` from the Lines box and play it.
   - Expected: the round is won. The row shows `goal S` (the old start).
4. **W4 the mod after swap.** Press swap, then look at `puzzle.json` and the app.
   - Expected: the mod publishes the swapped puzzle (start = the old goal, goal = the old start), and the app's new list contains the rest of the line.
5. **W5 (if met) boss rules.** Rule 12: after swap the display shows G + 1. Rule 14: after swap the display shows G + 1 (the goal rose before the swap); a glass swap shows G. Rule 13: swap re-rolls the goal.
   - Expected: as listed; the app has no swap lines in rule 13.
6. **W6 swap after an `=` mid-line.** Press an operator, a number and `=`, then swap.
   - Expected: it works (this is the mid-line follow-up; the solver doesn't use it).
7. Copy any `error in` lines from `MelonLoader\Latest.log`.
