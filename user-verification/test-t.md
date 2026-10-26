# Test T: Achievement modes (School mathematics, Just a one) (done, 2026-10-25; T1, T2, T4 and T5 confirmed, T3 not tested: the achievement was already unlocked)

Spec: [`spec/achievement-modes/`](../spec/achievement-modes/). Run the solver app (`dotnet run --project CalculatorPuzzle`), Follow game on, Lock off. Open the **Achievements** section in the app. Steam may already show an achievement as unlocked; then check the game's behaviour (round won, display) instead of the unlock popup.

1. **T1 (School mathematics).** In any round, click `(+, -, /, *) solve (School mathematics)`. Press the top line (every operator followed by its operand and `=`).
   - Expected: the round is won (the line ends on G) and Steam shows *School mathematics*. A line with `× 1 =` or `÷ 1 =` counts like any other.
2. **T2 (Just a one, `score_is` = S).** At a round start, click `+1 with 4 ops (Just a one)`. Press a top line.
   - Expected: the display shows S + 1 after the `=`. Write down the tag at the end of the line (`[÷]`, `[digit]`, …).
3. **T3 (`0` and `10` don't take the credit).** Over the run, press Just a one lines with 4 different tags, at least one `÷`, `-`, `+` or `mod` line typed with `10` only.
   - Expected: *Just a one* unlocks on the fourth different tag, not before. The digit keys `1`–`9` count as one tag.
4. **T4.** After a Just a one line, type the new S (S + 1) into the app and solve the round normally (turn the mode off by clicking the active radio button).
   - Expected: the round goes on as usual and is won.
5. **T5 (UI).** Open and close the Achievements section. Click a radio button twice.
   - Expected: the second click turns the mode off; the collapsed header names the active mode (`▸ Achievements: School mathematics`); with a mode on, a new puzzle from Follow game is solved in that mode.

Report: for each step, what you saw. Mark this test done in its heading after the user reports.
