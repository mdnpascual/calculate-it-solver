# Test V: near, median, sup, money, ==, mw, AX (done, 2026-10-25; V1 coins, V1–V4 lines that won, V5 (a winning `==` line), V6 and V6a confirmed; V7, V8 not met; the "nothing happens but the charge is used" cases of V3/V4/V6a not reported separately)

Spec: `spec/near-median-sup-money-equal-mw/`. These buttons are rare, so use whatever run gives them (shop, or a round that offers them). Do each step when you meet the button; skipping is fine and you can say "not met".

1. Rebuild and reinstall the mod (`dotnet build GameHook.MelonLoader -c Release`, then see [install-and-remove.md](install-and-remove.md)) and rebuild the app (`dotnet build`). Use Follow game. In `puzzle.json` check the new top-level fields `coins` (equals the money on screen), `diffType` and `coinPassives` (the coin passives you own, if any).
   - Expected: the three fields are there and match the game. The log line says `coins=…`.
2. **V1 near.** Play a round with `near`. Press it with the left side within 10 of the goal, then farther away.
   - Expected: within 10 → the goal; farther → + 10. The overlay (F9) shows `near`, and the app finds the line `near`.
3. **V2 median.** Press `median` with the left side below the goal (odd distance, so it rounds up) and above it.
   - Expected: below → `ceil((L + G) / 2)`; above → goes down but stops at G + 1 (pressing again does nothing new). From G − 1 it reaches G.
4. **V3 sup.** Press `sup` on a display of 5 characters, then on a display of 6 characters.
   - Expected: 5 characters → the digit sum. 6 characters → nothing happens, and the button's charge count still drops by 1.
5. **V4 money.** Press `money` on a display of 4 characters, then on 5 characters. Compare `coins` in `puzzle.json` with the money shown.
   - Expected: 4 characters → + your money. 5 characters → nothing happens, charge used.
6. **V5 ==.** With `==` and a digit key: `==` then `7` on `483`; then `==` then `10` on a number.
   - Expected: `483` → `777`; with `10` every digit becomes `1`. Between the two presses `=` and the other operators are blocked, and after the key the calculator is back on the left side. The overlay shows `==`; the app line reads `== 7`.
7. **V6 mw.** With an empty `number_mw` (label `MW`): absorb a digit (press mw, then a number key). Check `puzzle.json` and the overlay. Then use it on the right side.
   - Expected: `puzzle.json` is written again with `"stored": <digit>` on the `number_mw` button, the overlay shows `m<digit>` (`mw` while empty), and the app's Buttons box has `m<digit>:n`. On the right side it types the stored number (one charge per press); on the left side it can't be pressed. While empty, the app status says `mw empty`.
8. **V6a AX.** `AX` then `7` on `41`; `AX` then `10`; `AX` on a number with a leading zero (for example `04`); `AX` on a 6-character display.
   - Expected: `741`; `1041`; `04` → `74`; `=` and the other operators are blocked between the two presses; on 6 characters `AX` does nothing and the charge count drops by 1. The overlay shows `AX`.
9. **V7 (optional) boss rule 14 + near/median.** In a rule-14 round press `near` (or `median`) with the left side exactly 10 below the goal.
   - Expected: the game uses the raised goal (G + the charged presses so far, this press included): `near` on G − 10 gives G + 1, not G. The app's line for G + k should agree.
10. **V8 (optional) boss rule 10 + money.** In a rule-10 round press a charged button, then `money`.
    - Expected: `money` adds the amount after its own press paid (and after the earlier press paid): 2 less than the amount before the first press. A difficulty 7+ run drains 1 from the third charged press on.
11. Copy any `error in` lines from `MelonLoader\Latest.log`.
