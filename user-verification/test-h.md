# Test H: More buttons (done, 2026-10-24; H1 left unconfirmed)

The mod now also publishes `last` (top level) and a `digit` on the `number_quest` button in `puzzle.json` / `state.json`, and the overlay shows live `c12` / `q8` cells. Rebuild and reinstall the mod first (as in Test C). The rules come from the decompiled code; these runs confirm them.

1. **H1.** (Left unconfirmed: hard to check in-game. The solver assumes `1211`.) Get `number_charge` (shop or Alchemy), e.g. at 12 charges, then in a round press `+` and the charge key twice, then `=`.
   - Expected: the operand shows `1211` (open question 4: two presses at 12 give `1211`, not `1110`). The overlay cell reads `c12`, then `c11`, then `c10`. Report what you saw if it differs.
2. **H2.** (Confirmed, 2026-10-24: the overlay cell shows the new digit after a press.) Get `number_quest`. Check the overlay cell and `puzzle.json` `digit`, press it once after an operator, then look at the cell and `state.json` again.
   - Expected: the cell shows `q<digit>` equal to the game's label; after the press the new random digit shows within 0.1 s and `state.json` has the new `digit`.
3. **H3.** (Round 1 confirmed, 2026-10-24: `last` = 0.) Win a round, then start the next one with `operator_last`. Open `puzzle.json`.
   - Expected: `last` equals the previous round's goal and the solver's Last box is filled by Follow game. Press `last`: the left side becomes that goal. In round 1 note `last` (open question 1: 0, or a saved value).
4. **H4.** (Confirmed for `a->c` and `b->c`, 2026-10-24. Charge and quest keys after them not reported.) `operator_remorph_a` and `_b`: on a 3-digit left side press `a->c`, then `10`; again with `b->c` and a digit key.
   - Expected: `483` `a->c` `10` → `103`; `483` `b->c` `7` → `487`; no `=` needed (open question 3). Also try the charge key and the quest key after `a->c`.
5. **H5.** (Confirmed in-game by the user before this test; nothing to do.) `operator%`: `95` `%` `2` `5` `=`.
   - Expected: `23`.
6. **H6.** (Confirmed, 2026-10-24: expected numbers for `100-X` and `25|69`.) `operator_100-x` on `41`, `100` and `150`; `operator_flip` on `2569`.
   - Expected: `59`, `0`, `0`; `5296`.
7. **H7.** (Done, 2026-10-24: `100-X`, `25|69`, `a->c`, `b->c`, `last`, `q#`, `c#` seen on the calculator overlay; the shop panel shows plain `q` and `c`. `%` cell seen too.) In challenge 9 (Alchemy) check the calculator overlay and the shop panel for each new id.
   - Expected: `c<n>`, `q<d>`, `%`, `last`, `a->c`, `b->c`, `100-X`, `25|69` instead of `(…)`; shop offers of the charge and quest keys show `c` and `q`.
8. Copy any `error in` lines from `MelonLoader\Latest.log` and one `buttons=[...]` change line with a quest key (`/d<digit>`).
