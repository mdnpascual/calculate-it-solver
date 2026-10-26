# Test N: Boss rule 12, every `=` adds 1 (done, 2026-10-25; N1, N2 and N3 confirmed; N4 skipped, N5 optional)

1. Rebuild and run the app. Play challenge 4 (every round is a boss) until the boss text reads "Each number that you send gets bigger by 1".
2. **N1.** Look at the app.
   - Expected: the Boss box shows 12, the status starts `Rule 12: every = adds 1:`, and there is no `boss rule 12 not supported` note.
3. **N2.** Press a top line that has an `=` in the middle and ends with edits (for example `÷ 10 = X8`), then press `=` once more.
   - Expected: the round is won (the mid `=` adds 1, the send `=` adds 1).
4. **N3.** Press a top line that ends with `op D =` (for example `+ 6 =`).
   - Expected: the round is won on that `=`, with no extra press.
5. **N4.** If a round comes up with S = G − 1 (or set it up by hand), press `=` first, with no operator.
   - Expected: the round is won. The app shows the 0-press line `=`.
6. **N5** (information for the follow-up). After an `Xd` press, press `=`. Write down whether the display goes up by 1 and whether the next press can keep editing.
7. Copy any `error in` lines from `MelonLoader\Latest.log`.
