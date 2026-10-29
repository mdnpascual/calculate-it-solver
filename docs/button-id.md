# Button ids

Button ids used by Calculate It (the `id` field in `%LOCALAPPDATA%\CalcHook\puzzle.json`).

The game has no enum for these. Each id is the name of a button object (prefab) in the scenes. The closest thing to a master list is the button collection (see [Collection order](#collection-order) below). Sources:

- Every id except `operator_tag` appears as an exact length-prefixed Unity string in `CalculateIt_Data\level0` and/or `level1` (byte scan of the shipped scene files, 2026-10-23).
- `operator_tag` appears only as a string literal in `Assembly-CSharp.dll` (REA `inspect_managed_members`). It isn't placed in any scene, so it may be created at runtime or unused.

The label is what the solver app and the overlay show ([`spec/button-labels/`](../spec/button-labels/requirements.md)). A description is given only for buttons the solver maps (`GameLink.MapButton`). The others are blank until the solver supports them.

| id | description |
|----|-------------|
| `number0` | Digit key `0`: appends 0 to the operand (right side only) |
| `number1` | Digit key `1`: appends 1 to the operand (right side only) |
| `number2` | Digit key `2`: appends 2 to the operand (right side only) |
| `number3` | Digit key `3`: appends 3 to the operand (right side only) |
| `number4` | Digit key `4`: appends 4 to the operand (right side only) |
| `number5` | Digit key `5`: appends 5 to the operand (right side only) |
| `number6` | Digit key `6`: appends 6 to the operand (right side only) |
| `number7` | Digit key `7`: appends 7 to the operand (right side only) |
| `number8` | Digit key `8`: appends 8 to the operand (right side only) |
| `number9` | Digit key `9`: appends 9 to the operand (right side only) |
| `number10` | Digit key `10`: appends 10 to the operand (right side only) |
| `number11` | Digit key `11`: appends 11 to the operand (right side only) |
| `number15` | Digit key `15`: appends 15 to the operand (right side only) |
| `number32` | Digit key `32`: appends 32 to the operand (right side only) |
| `number99` | Digit key `99`: appends 99 to the operand (right side only) |
| `number_charge` | Label `volt<n>`. types its current charge count as a number (right side only), then loses one charge. At 12, two presses type `12` then `11`, so the operand is `1211`. Never types 0. Solver: `c:12` or `c12:n`, lines show `c12 c11`. A glass one types its raw uses once. (Order of reading and lowering unconfirmed in-game: Test H1.) |
| `number_mw` | Label `mem<n>`. treat as a digit (right side only), parse label like this: for example 5|2. Treat it as a 5 digit with 2 usage Checked in the game code (`NumberReader`): empty, one press absorbs the next number key (the left side stays, "cannot be overwritten"); stored, it is an operand key that types the stored number (0–9, `10`, `c12`'s 12, …). Solver: `mD:n` once stored (the mod publishes `stored`; an empty one is ignored as `mw`), lines show `m5`; its charges are its own. |
| `number_quest` | Label `unk<d>`. types its current digit (right side only), then the digit is re-rolled at random, so a line presses it at most once. Label `8|4`: digit 8, 4 charges. Solver: `qD:n` (the mod publishes `digit`), lines show `q8`. |
| `number_test` |  |
| `operator+` | `+`: add (one operation per `=`) |
| `operator-` | `-`: subtract; below 0 gives 0 |
| `operator*` | `×`: multiply |
| `operator/` | `÷`: whole-number division (fraction dropped) |
| `operator%` | percent (operator, needs `=`): `left × operand / 100`, fraction dropped. `95` `%` `2` `5` `=` → `23` (confirmed in-game). The game keeps the result as a float, so the solver rejects results above 2^24. Solver token `%`. |
| `operator_mod` | `mod`: remainder |
| `operator_3x` | `3X`: prepends 3 to the left side |
| `operator_4x` | `4X`: prepends 4 to the left side |
| `operator_5x` | `5X`: prepends 5 to the left side |
| `operator_6x` | `6X`: prepends 6 to the left side |
| `operator_8x` | `8X`: prepends 8 to the left side |
| `operator_x1` | `X1`: appends 1 to the left side |
| `operator_x2` | `X2`: appends 2 to the left side |
| `operator_x4` | `X4`: appends 4 to the left side |
| `operator_x7` | `X7`: appends 7 to the left side |
| `operator_x9` | `X9`: appends 9 to the left side |
| `operator_+2` | `X+2`: adds 2 to the left side (left side only) |
| `operator_+4` | `X+4`: adds 4 to the left side (left side only) |
| `operator_+8` | `X+8`: adds 8 to the left side (left side only) |
| `operator_+10` | `X+10`: adds 10 to the left side (left side only) |
| `operator_+16` | `X+16`: adds 16 to the left side (left side only) |
| `operator_+32` | `X+32`: adds 32 to the left side (left side only) |
| `operator_+64` | `X+64`: adds 64 to the left side (left side only) |
| `operator_-4` | `X-4`: subtracts 4 from the left side, not below 0 (left side only) |
| `operator_-8` | `X-8`: subtracts 8 from the left side, not below 0 (left side only) |
| `operator_-10` | `X-10`: subtracts 10 from the left side, not below 0 (left side only) |
| `operator_-16` | `X-16`: subtracts 16 from the left side, not below 0 (left side only) |
| `operator_-32` | `X-32`: subtracts 32 from the left side, not below 0 (left side only) |
| `operator_-64` | `X-64`: subtracts 64 from the left side, not below 0 (left side only) |
| `operator_++` | `X++`: adds 1 to the left side (left side only) |
| `operator_--` | `X--`: subtracts 1 from the left side, not below 0 (left side only) |
| `operator_*2` | `X*2`: multiplies the left side by 2 (left side only) |
| `operator_/2` | `X/2`: divides the left side by 2, fraction dropped (`55` → `27`) (left side only) |
| `operator_sqr` | Label `X²`. `sqr`: squares the left side (left side only) |
| `operator_sqrt` | `sqrt`: square root of the left side, rounded to the nearest whole number (left side only) |
| `operator_100-x` | sets the left side to `100 - L`, or 0 when L > 100 (left side only). `41` → `59`. The onClick argument is 100 (checked with UnityPy). Solver token `100-X`. |
| `operator_0to8` | `0->8`: changes every digit 0 on the left side to 8, keeping leading zeros (left side only) |
| `operator_1to5` | `1->5`: changes every digit 1 on the left side to 5, keeping leading zeros (left side only) |
| `operator_2to4` | `2->4`: changes every digit 2 on the left side to 4, keeping leading zeros (left side only) |
| `operator_3to0` | `3->0`: changes every digit 3 on the left side to 0, keeping leading zeros (left side only) |
| `operator_4to7` | `4->7`: changes every digit 4 on the left side to 7, keeping leading zeros (left side only) |
| `operator_5to6` | `5->6`: changes every digit 5 on the left side to 6, keeping leading zeros (left side only) |
| `operator_6to2` | `6->2`: changes every digit 6 on the left side to 2, keeping leading zeros (left side only) |
| `operator_7to3` | `7->3`: changes every digit 7 on the left side to 3, keeping leading zeros (left side only) |
| `operator_8to9` | `8->9`: changes every digit 8 on the left side to 9, keeping leading zeros (left side only) |
| `operator_9to1` | `9->1`: changes every digit 9 on the left side to 1, keeping leading zeros (left side only) |
| `operator_reverse` | Label `reverse`. `flip`: reverses the left side's digits (`120` → `021`) |
| `operator_flip` | Label `mirror`. swaps the digits 2↔5 and 6↔9 in one press (left side only), others stay: `2569` → `5296`. Reads the number, so leading zeros drop. Solver token `25\|69` (`flip` is `operator_reverse`). |
| `operator_<` | Label `X<<`. `<<`: bit shift left, multiplies the left side by 2 (`6` → `12`) (left side only) |
| `operator_>` | Label `X>>`. `>>`: bit shift right, divides the left side by 2, fraction dropped (`6` → `3`, `55` → `27`) (left side only) |
| `operator_cut` |  |
| `operator_swap` | trades the display and the goal, then sends (`MainCalc.Swap` then `Send`; the click runs `SubUse` first). Left side only: an operator press blocks it and a digit key does not unblock it. Solver token `swap`: the solver tries `swap` as a line's first press (cost + 1), see `spec/operator-swap/`. |
| `operator_last` | sets the left side to the previous round's goal (`StartLvl.last`, left side only). The mod publishes it as `last` in `puzzle.json`; the app has a Last box. Round 1: unconfirmed (Test H3). Solver token `last`. |
| `operator_round` | Label `->)`. `round`: rounds the left side to the nearest 10 (last digit 0–4 down, 5–9 up) (left side only) |
| `operator_near` | Label `+-10`. Sets current number to the goal if the difference is at most 10. Otherwise adds 10. Solver token `near` (left side only, 1 press, reads the number). `SubUse` runs first, so in boss rule 14 the goal is already raised when it reads it. No clamp. |
| `operator_complement` | Prepends a single selected digit on the current number (left side only, once it is pressed, only digits can be selected, then after selecting a digit, calculator goes back to left side phase (can't press digits)). Checked in the game code (`MainCalc.Complement`): works only on a display of fewer than 6 characters; the key can be any number key, not only a digit (`41` `AX` `10` → `1041`, `41` `AX` `7` → `741`); leading zeros of the left side drop. Solver token `AX`, lines show `AX 7`; the solver drops a result of more than 6 characters. |
| `operator_mediana` | Label `_2_`. finds the midpoint between current number and goal number rounding up. Solver token `median`: `ceil((L + G) / 2)`; from above it stops at G + 1, from below G − 1 reaches G. Left side only. |
| `operator_prime` | `p`: sets the left side to the nearest prime, a tie goes down (`8084` → `8081`, `88` → `89`, `0` → `2`; `MainCalc.FindNearestPrime`). Left side only |
| `operator_sup` | Label `combine`. adds all digits of a number. Solver token `sup`: the digit sum, only on a display of fewer than 6 characters (on 6 nothing happens, but the charge is used). Left side only. |
| `operator_to0` | Label `x->0`. `reset`: sets the left side to `0` |
| `operator_to25` | Label `x->25`. `to25`: sets the left side to `25` |
| `operator_random` |  |
| `operator_energy` |  |
| `operator_resurrect` | can be ignored, it resurrects glass buttons (doesn't affect calculation) |
| `operator_remorph_a` | operator plus one key, no `=`: result `(L mod 10) + k × 10`, where k is the number the next key types (a digit, `10`, the charge key or the quest key). `483` `a->c` `7` → `73`; with `10` → `103`. A multi-digit key is multiplied in as one number. Solver token `a->c`. |
| `operator_remorph_b` | operator plus one key, no `=`: result `(L / 10) × 10 + k`, which replaces the last digit. `483` `b->c` `7` → `487`; with `10` → `490`. Solver token `b->c`. |
| `operator_reroll` |  |
| `operator_bomb` |  |
| `operator_money` | Label `$`. adds current amount of money to the current number. Solver token `money`: only on a display of fewer than 5 characters (on 5 or 6 nothing happens, but the charge is used); the amount is the money after its own press paid. Money also moves during a line (difficulty 7+ from the third charged press, boss rule 10, coin passives on `=`); the simulator models it. Left side only. |
| `operator_credit` |  |
| `operator_coinadd` |  |
| `operator_coinsub` | Label `$++` (the face is likely an icon; scene text child `11`). `CountUses.SubUse`, then `MainCalc.UseCoins(-1)`: only with coins left, -1 coin and `PlusNumber(1)` (the `X++` code: +1, clamp 999 999). At 0 coins nothing happens but the charge is used. Boss rule 10: `SubUse` takes a coin first, so a press costs $2 (with $1 it takes it and does nothing). Left side only. Solver token `coinsub`: a fallback loop when no line is found, see `spec/coinsub-fallback/`. |
| `operator_send` |  |
| `operator_warp` | d20: re-rolls every button (`StartLvl.D20`, wired in the scene `onClick` after `SubUse`). Unmapped. The mod publishes the new button set again after it (`warp` in `puzzle.json`). See `spec/warp-rescan/`. |
| `operator_secret` |  |
| `operator_equal` | Label `equalize`. sets all digit of the current number to a chosen digit. Checked in the game code (`MainCalc.MorphAll`): operator plus one key, no `=`; every digit becomes the key's first digit (`483` `==` `7` → `777`, `483` `==` `10` → `111`). Solver token `==`. |
| `operator_tag` |  |

## Collection order

Found with REA (`inspect_managed_members` on `Assembly-CSharp.dll`) plus a UnityPy read of the `StartLvl` component in `level1` (2026-10-23).

- `ButtonDiscovery.CollectionCount` is 48; `HasCompleteCollection` checks slots 0–47. A slot counts as found when `button-discovery.json` (in the game's persistent data folder) has the key `button:<slot>`.
- `ButtonDiscovery.CollectionType(button, StartLvl)` gives a button's slot. It strips `(Clone)` from the object name, then checks in this order:
  1. In `StartLvl.all_numbers` → 7
  2. In `StartLvl.appenders_tr` → 8 if the button's `Text` label starts with `X`, otherwise 6
  3. In `StartLvl.transformers_tr` → 20
  4. Its index in `StartLvl.buttons_tr` (first match by name)
  5. In `StartLvl.adders_tr` → 35
  6. In `StartLvl.subers_tr` → 36
  7. Otherwise -1 (not in the collection)
- So a whole family shares one slot: for example, every `aToB` button counts as slot 20.
- Slots 25–29 are locked until a difficulty challenge is beaten (`CollectionManage.Start`, `ChallengeRewards`).
- `StartLvl.glassForbiddenIds` = 1, 2, 3, 4, 13, 17, 45 (slots barred on the glass calculator); `glassCalculatorForbiddenIds` = 45.
- `level0` (the menu scene) has only the first 15 entries of `buttons_tr`.

| slot | `buttons_tr` entry | also counts | note |
|------|--------------------|-------------|------|
| 0 | `operator+` |  |  |
| 1 | `operator-` |  | Forbidden on glass |
| 2 | `operator*` |  | Forbidden on glass |
| 3 | `operator/` |  | Forbidden on glass |
| 4 | `operator_reverse` |  | Forbidden on glass |
| 5 | `operator_to25` |  |  |
| 6 | `operator_x1` | appenders_tr, label not starting with `X` (prepend `dX`): `operator_3x`, `operator_4x`, `operator_5x`, `operator_6x`, `operator_8x` | Mismatch: the entry is an append button, but appends resolve to slot 8 |
| 7 | `number1` | all_numbers: `number0`–`number9` |  |
| 8 | `operator_5x` | appenders_tr, label starting with `X` (append `Xd`): `operator_x1`, `operator_x2`, `operator_x4`, `operator_x7`, `operator_x9` | Mismatch: the entry is a prepend button, but prepends resolve to slot 6 |
| 9 | `operator_random` |  |  |
| 10 | `operator_near` |  |  |
| 11 | `operator_cut` |  |  |
| 12 | `operator_last` |  |  |
| 13 | `operator_mod` |  | Forbidden on glass |
| 14 | `operator_money` |  |  |
| 15 | `operator_bomb` |  |  |
| 16 | `operator_sqrt` |  |  |
| 17 | `operator_energy` |  | Forbidden on glass |
| 18 | `operator_reroll` |  |  |
| 19 | `operator_++` |  | Also in `adders_tr`, but the `buttons_tr` lookup runs first, so it resolves to 19 |
| 20 | `operator_2to4` | transformers_tr: `operator_1to5`, `operator_2to4`, `operator_3to0`, `operator_4to7`, `operator_5to6`, `operator_6to2`, `operator_7to3`, `operator_8to9`, `operator_9to1`, `operator_0to8` |  |
| 21 | `number_quest` |  |  |
| 22 | `operator_sqr` |  |  |
| 23 | `operator_swap` |  |  |
| 24 | `operator_prime` |  |  |
| 25 | `operator_100-x` |  | Locked: Complete difficulty 2 on a Normal calculator |
| 26 | `operator_remorph_a` |  | Locked: Complete difficulty 2 on a Engineering calculator |
| 27 | `operator_remorph_b` |  | Locked: Complete difficulty 2 on a Steel calculator |
| 28 | `operator_equal` |  | Locked: Complete difficulty 2 on a Old calculator |
| 29 | `operator_coinadd` |  | Locked: Complete difficulty 3 on a Normal calculator |
| 30 | `operator_coinsub` |  |  |
| 31 | `operator_round` |  |  |
| 32 | `number_charge` |  |  |
| 33 | `operator_round` |  | Duplicate of slot 31; the name lookup returns 31 first. `ButtonDiscovery.IsFound` has the literal keys `button:31` and `button:33`, so 33 is probably handled as a special case there (not decoded) |
| 34 | `operator%` |  |  |
| 35 | `operator_+2` | adders_tr: `operator_+2`, `operator_+4`, `operator_+8`, `operator_+16`, `operator_+32`, `operator_+64` |  |
| 36 | `operator_-4` | subers_tr: `operator_-4`, `operator_-8`, `operator_-16`, `operator_-32`, `operator_-64` |  |
| 37 | `operator_>` |  |  |
| 38 | `operator_<` |  |  |
| 39 | `operator_secret` |  |  |
| 40 | `operator_sup` |  |  |
| 41 | `operator_flip` |  |  |
| 42 | `operator_credit` |  |  |
| 43 | `operator_resurrect` |  |  |
| 44 | `operator_--` |  |  |
| 45 | `number_mw` |  | Forbidden on glass |
| 46 | `operator_complement` |  |  |
| 47 | `operator_mediana` |  |  |

### Not in the collection

These ids are in the scenes but in none of the `StartLvl` arrays above, so `CollectionType` returns -1 for them:

`number10`, `number11`, `number15`, `number32`, `number99`, `number_test`, `operator_+10`, `operator_-10`, `operator_*2`, `operator_/2`, `operator_to0`, `operator_send`, `operator_warp`, `operator_tag`
