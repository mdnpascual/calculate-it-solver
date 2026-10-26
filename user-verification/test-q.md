# Test Q: Boss rule 6, 2 charges per press (done, 2026-10-25; Q1–Q3 confirmed, Q4 not run)

Spec: [`spec/boss-double-charge/`](../spec/boss-double-charge/). Rebuild the solver first. Use challenge 4 (every round is a boss) until the text reads `Each button press consumes 2 charges`.

1. **Q1.** Look at the app (Follow game on).
   - Expected: the Boss box shows 6, the status has no `boss rule 6 not supported` note.
2. **Q2.** Press any button with an odd charge count (for example 3).
   - Expected: 3 → 1 → 0, and the button can still be pressed at 1 charge.
3. **Q3.** Play the top line.
   - Expected: it never runs out of charges and wins the round.
4. **Q4.** (If the charge key is there.) After an operator, press the charge key twice. Write down the operand shown.
   - Expected: `1210` at 12 charges. `108` would mean the charge drops before typing: then `ChargeValue` must start at `start - 2` (and `start - 1` in normal rounds).

Report: for each step, what you saw. Mark this test done in its heading after the user reports.
