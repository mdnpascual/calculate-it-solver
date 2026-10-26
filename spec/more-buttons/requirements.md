# Requirements: More buttons

**Status:** Complete (2026-10-24). Game rules come from the decompiled `Assembly-CSharp.dll` (ilspycmd); in-game Test H is done except H1. Open question 2 is answered: `operator_100-x` calls `X_ops_Y(100)`.

## Goal

The solver ignores several buttons the game hands out (`GameLink.MapButton` returns null for them), so puzzles that need them get no lines or only longer ones, and the Alchemy overlay shows them as `(charge)`, `(quest)` and so on. After this spec the Buttons box, the game feed, the simulator, the searches and the mod overlays know these buttons:

| # | Game id | Solver token | Kind |
|---|---------|--------------|------|
| 1 | `number_charge` | `c` (presses shown as `c12`, `c11`, …) | right-side key |
| 2 | `operator%` | `%` | operator (needs `=`) |
| 3 | `operator_last` | `last` | left-side transform |
| 4 | `operator_remorph_a` | `a->c` | operator plus one key (no `=`) |
| 4b | `operator_remorph_b` | `b->c` | operator plus one key (no `=`) |
| 5 | `number_quest` | `q` (presses shown as `q8`) | right-side key |
| 6 | `operator_100-x` | `100-X` | left-side transform |
| 7 | `operator_flip` | `25\|69` | left-side transform |

`operator_*2` (`X*2`) and `operator_/2` (`X/2`) are already supported (`left-transforms/`), so there's no work for them here.

## Game rules (from the decompiled code, 2026-10-24)

"Left side" and "right side" mean the same as in `CLAUDE.md`: the left side is the display before an operator, and the right side is the operand after one.

### 1. `number_charge`: the charge key (right side only)

- Code: `UsesNumber.Roll` calls `MainCalc.AddNumber(digit)`, with `digit = CountUses.uses`. The in-game description says "Acts as a number equal to its charge count before pressing".
- It types its **current charge count** as a number, and then the press uses up one charge. Starting at 12, two presses type `12` then `11`, so the operand is `1211`.
- It can never type 0: at 0 charges the button can't be pressed.
- **Glass:** for a glass button `uses` doesn't go down (see `glass-buttons/`), so it types its raw `uses` value, and it can be pressed only once.

### 2. `operator%`: percent (operator)

- Code: `MainCalc.Percent` (operator code 10) and `MainCalc.Send` code 10.
- Press `%` on the left side, then type the operand with digit keys (right side only), then press `=`. The result is `left × operand / 100` with the fraction dropped: 95 `%` `2` `5` `=` → 2375 / 100 = **23**.
- One operation per `=`, the same as `+ - × ÷ mod`.
- The game stores the result through `Mathf.RoundToInt` (a `float`), so a result above 16 777 216 (2^24) may not be exact. Lines whose `%` result is above 2^24 are rejected.
- Pressing `%` again while it's pending (the game's chained `operand × 100 / total`) is out of scope.

### 3. `operator_last`: last goal (left side only)

- Code: `MainCalc.Last` sets the left side to `StartLvl.last`. `StartLvl.GoAwayRoutine` sets `last = number` (the goal) when a round is won. The button's description shows the same value ("Generates needed number from last round (N)").
- It sets the left side to the **previous round's goal**, like `to25` sets it to 25.
- On the first round `last` is 0 (the field's default) unless a save restores it, so the button then works like `reset`. The solver uses whatever value the game holds. **Change from the original request:** the request said to ignore the button on the first run, but with the value coming from the game, 0 is the real effect. Check this in-game.
- **Where the value comes from (decided 2026-10-24):** the mod reads `StartLvl.last` and publishes it in `puzzle.json`. The app does **not** remember goals itself.

### 4. `operator_remorph_a` / `operator_remorph_b`: digit swaps (operator plus one key)

- Code: `MainCalc.Remorph(last)` (operator codes 7 and 8) and the code 7/8 branch of `MainCalc.AddNumber`.
- Press `a->c` (or `b->c`) on the left side. The left side is kept as the stored value L, and the right side opens. Exactly **one** right-side key press k follows (a digit key, `10`, the charge key or the quest key). The result appears **at once, with no `=`**, and the left side is active again:
  - `a->c`: **`(L mod 10) + k × 10`**. Only the last digit of L survives, so for a two-digit L this replaces the tens digit (`48` `a->c` `7` → `78`). A longer L loses its other digits too: `483` `a->c` `7` → **`73`**. With `10`: `483` → `3 + 100` = `103`. With a charge key at 12: `3 + 120` = `123`.
  - `b->c`: **`(L / 10) × 10 + k`**, which replaces the last digit: `483` `b->c` `7` → `487`, and with `10`: `480 + 10` = `490`.
- The `remorph_a` "single digit or double digits" question in `docs/button-id.md` is answered by the formulas: a multi-digit key is multiplied in as one number.
- The game shows `a->c` / `b->c` on the display while it waits. That's where the tokens come from.
- Pressing `=` after the result does nothing useful, so lines never do it. Cancelling a pending remorph is out of scope.
- The `rule 11` (boss "must press") handling in `Cancel` is out of scope (boss rule 11 is already a follow-up).

### 5. `number_quest`: the quest key (right side only)

- Code: `QuestionNumber.Reroll` calls `AddNumber(digit)` and then sets `digit = Random.Range(0, 10)`. `digit` is a public field.
- It types its current digit (0–9). Then the digit is **picked again at random**, so the solver can't know the next one. A line may press it **at most once**, whatever its charge count. Label example `8|4`: digit 8, 4 charges.
- Its charges count like any other button (spare, drained).

### 6. `operator_100-x` (left side only)

- Code: `MainCalc.X_ops_Y(int y_op)`, presumably wired with `y_op = 100` (the onClick argument isn't checked yet; the design checks it with UnityPy).
- It sets the left side to `100 − L`, or to 0 when L > 100: `41` → `59`, `100` → `0`, `150` → `0`.
- It reads the number, so leading zeros drop (`05` → `95`).

### 7. `operator_flip`: swap 2↔5 and 6↔9 (left side only)

- Code: `MainCalc.Flip` / `FlipNumbers`. Every `2` becomes `5`, every `5` becomes `2`, every `6` becomes `9` and every `9` becomes `6`. It works in both directions in one press, and the other digits stay: `2569` → `5296`, `41` → `41`.
- It reads the number (`(int)total`), so leading zeros drop. It can never create a leading zero, because 0 isn't swapped.
- Token `25|69` (decided 2026-10-24), because `flip` already means `operator_reverse`.
- The `(int)` cast would wrap above 2 147 483 647. The left side never gets that big in the game (6-character display), so the solver ignores it.

## Rules

### R1. Buttons box

1. New tokens, each with a charge count like every other button: `c:12`, `c12:1`, `q8:4`, `%:2`, `last:1`, `a->c:1`, `b->c:1`, `100-X:1`, `25|69:2`. Case and spaces are ignored, and `−` is read as `-`.
2. **Charge key:** `c:n` means it starts at value n with n presses. `cV:n` means it starts at value V with n presses (used for glass, where the game shows V but allows one press). `V` ≥ 1 and n ≤ V. A line's charge presses type V, V − 1, V − 2, … in order.
3. **Quest key:** `qD:n`, where D is the digit now shown (0–9). It can be pressed at most once per line. `q:n` without a digit is an error ("quest key needs its digit, write qD:n").
4. **Last:** a new **Last** textbox (a whole number ≥ 0) below Required. The `last` button needs it: if `last` has charges but Last is empty, the search runs without `last` and the status says `no Last value, last ignored`. Follow game fills the box.
5. `:0` leaves any of these buttons out, as it does for the others. Tokens that already exist parse the same as before (`10`, `0`–`9`, `X4`, `flip`, `2->4`, …).

### R2. Simulator (`EngineeringCalculator.Run`)

1. The charge key and the quest key work on the right side only, after any operator (`+ - × ÷ mod %`, or as the one key after `a->c` / `b->c`). On the left side they're invalid, like `10`.
2. The charge key types its current value, and the value drops by 1 per press across the whole line. Pressing it more often than its charges is invalid (that's `Fits`'s job). A value of 0 is never typed.
3. A second quest key press in one line is invalid (`Run` returns null).
4. `%` behaves like the other operators: `=` applies it, with no operand it's invalid, and a result above 2^24 is invalid.
5. `a->c` / `b->c` start the right side. The next press must be one right-side key, and it ends the operation at once. Any other press, or `=`, right after `a->c` / `b->c` is invalid.
6. `last`, `100-X` and `25|69` are left-side transforms (they work only on the left side, and on the right side they're invalid).
7. Every intermediate entry is logged as today. For `a->c`, the press after it logs the new left side.

### R3. Charges and ranking

1. `Fits`, `Spare` and `Drained` count charge-key presses under one key, `c`, and quest presses under `q`, whatever values they typed.
2. Ranking is unchanged (cost, drained, spare, kind).
3. Required (Market Demand) works with the new tokens: `c`, `q`, `%`, `last`, `a->c`, `b->c`, `100-X` and `25|69` can be required. A line "uses" the charge key if it has any `c…` press.

### R4. Lines box display

1. Charge presses show as `c` plus the value typed: `÷ c12 c11 =`.
2. Quest presses show as `q` plus the digit: `% q8 =`.
3. The others show their tokens: `%`, `last`, `a->c`, `b->c`, `100-X`, `25|69`.

### R5. Searches

1. **Operand typing** (`DivisionShortcut.TypeOperand` and `CheapestDivisor`, used by the arithmetic, division and modulo phases): the charge key and the quest key can spell parts of an operand. The charge key's values go down as it's used inside one operand (`1211` = `c12 c11` when the charge key is at 12).
2. **Arithmetic phase** (`ArithmeticSearch`):
   - `%` joins `+ - ×` as a one-operator line: edit S into X, `%`, the operand D, `=`, edit Y into G. D is any typeable number with floor(X·D / 100) = Y, so D runs from ceil(100Y / X) to floor((100Y + 99) / X) (X > 0), and the cheapest typeable D in that range is used.
   - `a->c` / `b->c` lines: edit S into X, the remorph key, one key k, edit Y into G. Y is fixed by X and k (rule 4 above), so every X candidate is tried with every one-press key.
3. The left-side search (`LeftSearch`) gets `last`, `100-X` and `25|69` for free as transforms. The Division and Modulo Shortcuts still don't use transforms (unchanged).
4. The division phase also runs when the only right-side keys are charge or quest keys (today it needs `10` or a digit key).

### R6. Game feed (`puzzle.json`, `GameLink.Parse`)

1. The mod adds a top-level `"last": N` (`StartLvl.last`) to `puzzle.json` and `state.json`, and a `"digit": D` field on the `number_quest` button (`QuestionNumber.digit`). Both are new optional fields, and `version` stays 1.
2. `GameLink.Parse` maps:
   - `number_charge` → `c{uses}:{charges}` (charges = `min(uses, 1)` when glass, otherwise `uses`).
   - `number_quest` → `q{digit}:{charges}`. Without a `digit` field (an older mod) the button is ignored, as unmapped ids are today.
   - `operator%` → `%:n`, `operator_last` → `last:n`, `operator_remorph_a` / `_b` → `a->c:n` / `b->c:n`, `operator_100-x` → `100-X:n`, `operator_flip` → `25|69:n`.
   - `GamePuzzle.Last` = the `last` field (null when it's missing). Follow game writes it into the Last box (an empty box when null).
3. Required: `GameLink` maps a required `number_charge` to `c` and a required `number_quest` to `q`.

### R7. Mod overlays (`ButtonSymbols`, Alchemy calculator and shop panel)

1. The overlay cell for `number_charge` shows its current value (the number it would type), written like the Lines box: **`c12`**. (The request said "display the digit"; `c12` shows it with the same prefix as the lines, so a line can be matched to a cell.)
2. The cell for `number_quest` shows its **current digit**: **`q8`**. The overlay redraws from live data every frame, so after a press the new random digit shows on the next frame. `state.json` and the log line also include the new digit on the next state change (the digit is part of the change check).
3. The cells for the other new ids show their tokens (`%`, `last`, `a->c`, `b->c`, `100-X`, `25|69`) instead of `(…)`.
4. Shop offers show the same tokens. A shop offer of `number_charge` / `number_quest` has no live value yet (it isn't on the calculator), so it shows `c` / `q`.
5. The mod stays read-only (no presses, charges or save writes).

## Examples

| Start → Goal | Buttons | Expected line (one of the cheapest) |
|--------------|---------|------------------------------------|
| 10 → 1221 | `+:1, c:12` | `+ c12 c11 =` → 10 + 1211 (3 presses) |
| 50 → 10 | `÷:1, c:5` | `÷ c5 =` → 50 / 5 (2 presses) |
| 41 → 8 | `÷:1, q5:3` | `÷ q5 =` → 41 / 5 (2 presses) |
| 95 → 23 | `%:1, 2:1, 5:1` | `% 2 5 =` → 2375 / 100 (3 presses) |
| 483 → 73 | `a->c:1, 7:1` | `a->c 7` → 3 + 70 (2 presses) |
| 483 → 487 | `b->c:1, 7:1` | `b->c 7` → 480 + 7 (2 presses) |
| 41 → 59 | `100-X:1` | `100-X` (1 press) |
| 41 → 37 (Last = 37) | `last:1` | `last` (1 press) |
| 2569 → 5296 | `25\|69:1` | `25\|69` (1 press) |

Simulator-only checks (exact press lists):

- 10, `+ c12 c11 =` with `c:12` → `10`, `12`, `1211`, `1221`.
- 10, `+ c12 c11 c10 =` with `c:2` → valid in `Run`, rejected by `Fits` (3 presses, 2 charges).
- 10, `+ q8 q3 =` → invalid (second quest press).
- 483, `a->c 10` → `483`, `103`. 483, `a->c =` → invalid. 483, `a->c 7 =` → invalid.
- 95, `% 2 5 =` → `95`, `2`, `25`, `23`.
- `05` (after `3->0` on `35`), `100-X` → `95`. `120`, `25|69` → `150`.
## Open questions

1. **`last` on round 1:** is `StartLvl.last` 0, or does a save restore the previous run's value? (Check in-game, Test H.) **Answered 2026-10-24: 0 in round 1 (Test H3).**
2. **`100-X` onClick argument:** confirm `y_op = 100` in the scene data (UnityPy). If it's another value, the token becomes `<y>-X`.
3. **Remorph with `10`, the charge key and the quest key:** the formula multiplies the key's value by 10. This follows from the code (`number * 10`), but nobody has pressed it in-game yet. **Answered 2026-10-24: `a->c` and `b->c` confirmed in-game (Test H4); the `10` and charge-key cases not reported.**
4. **Charge-key order of events:** the code reads `uses` before `SubUse` lowers it ("before pressing", per the description). Check in-game that two presses at 12 give `1211`, not `1110`. **Left unconfirmed (hard to check in-game); the solver assumes `1211`.**

## Out of scope

- `number_mw`, `number_test`, `number11`/`15`/`32`/`99`, and the other unmapped ids (`cut`, `swap`, `near`, `prime`, `complement`, `mediana`, `equal`, `sup`, …).
- The 6-character display limit and the 999 999 cap (existing follow-ups; now [`display-cap/`](../display-cap/)).
- Multi-operator lines (for example `%` then `+`).
- Chained `%` (pressing `%` twice) and cancelling a pending operation.
- Solver-side memory of previous goals (replaced by the mod's `last`).
- Boss rule 11 (`must_butt`).
