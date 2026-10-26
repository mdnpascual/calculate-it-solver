# Requirements: near, median, sup, money, equal, mw and complement buttons

**Status:** Complete (2026-10-25, 740 tests). Test V done: coins, sup, money, median, near, mw, AX and `==` confirmed in game. Optional V7 (boss rule 14 + near/median) and V8 (boss rule 10 + money) were not met; the simulator follows the decompiled `SubUse`-first order.

## Goal

Support seven more game buttons in the solver, the mod overlay and `puzzle.json`:

| Game id | Solver token | Kind | One-line effect |
|---------|--------------|------|-----------------|
| `operator_near` | `near` | [left side] transform | goal if the goal is at most 10 away, else + 10 |
| `operator_mediana` | `median` | [left side] transform | midpoint of the left side and the goal, rounded up |
| `operator_sup` | `sup` | [left side] transform | sum of the digits |
| `operator_money` | `money` | [left side] transform | + the player's money (coins) |
| `operator_equal` | `==` | [left side] operator + one [right side] key, no `=` | every digit becomes the key's first digit |
| `number_mw` | `mD` (`m5`) | [right side] operand key, once it stores a number | types the stored number |
| `operator_complement` | `AX` | [left side] operator + one [right side] key, no `=` | prepends the key's number to the left side |

The user's notes in `docs/button-id.md` were checked against the game code. They are right; the code adds the details below (the conditions on the display length, the order of charge and effect, and how money changes during a line). For `operator_complement` the note says "a single selected digit"; the code takes any number key, so `10` prepends `10`.

(`operator_remorph_b` was checked too: it is already supported as `b->c`, spec `more-buttons/`, Test H4.)

## Background (ilspycmd 9.1 on `Assembly-CSharp.dll` and UnityPy on scene `level1`, 2026-10-25)

What each button calls, from its `Button.m_OnClick` in `level1` (the game scene; `level0` has an older copy of `operator_near` with the calls in another order):

| Button | onClick, in order | Infos id |
|--------|-------------------|----------|
| `operator_near` | `CountUses.SubUse`, `MainCalc.AddSome(10)`, sound | 10 |
| `operator_mediana` | `SubUse`, `MainCalc.Median`, sound | 47 |
| `operator_sup` | `SubUse`, `MainCalc.Sup`, sounds | 40 |
| `operator_money` | `SubUse`, `MainCalc.AddNumberMoney`, sounds | 14 |
| `operator_equal` | `SubUse`, `MainCalc.MorphAll`, sound | 28 |
| `number_mw` | `SubUse`, sound, `NumberReader.SwapState` | 45 |
| `operator_complement` | `SubUse`, `MainCalc.Complement`, sound | 46 |

`SubUse` always runs **before** the effect. So a charge is used even when the effect does nothing, and in boss rule 14 the goal has already gone up by 1 when the effect reads it. Boss rule 10 and the hardest difficulties take money in `SubUse` too, so `money` reads the amount after its own press paid.

The scene texts for `operator_sup` ("Square", "Raises a number to the power of two") and `operator_equal` ("Mod", "…remainder…") are stale copies from other buttons. The code below is what the buttons do.

### `near`: `MainCalc.AddSome(10)`

```
if |goal − total| <= 10 (float math)  → total = goal
else                                  → total = total + 10
```

- `goal` is `StartLvl.number`, the goal shown now (in boss rule 14, already raised by this press).
- Reads the number (`total`), so leading zeros drop. No length check, no clamp: `999 995` with a far goal gives `1 000 005`.
- No other state changes (the number keys stay blocked), so it is a plain [left side] transform.

### `median`: `MainCalc.Median()`

`total = ceil((total + goal) / 2)` in `decimal` (exact). Reads the number. It never moves past the goal, and it reaches G only from G − 1 or G: from G + 1 it stays at G + 1 (ceil((2G + 1) / 2) = G + 1). From below, G − 1 → G. No clamp needed (the result is at most the bigger of the two).

### `sup`: `MainCalc.Sup()`

Only when the display text has **fewer than 6 characters** (leading zeros count): `total = SumOfDigits(total)`. On a 6-character display nothing happens, but the charge is used (`SubUse` ran first). Reads the number: `0099` → 18.

### `money`: `MainCalc.AddNumberMoney()`

On the left side (`startsum` false) it works only when the display text has **fewer than 5 characters**: `total = total + StartLvl.coins` (through a float, exact at these sizes). On 5 or 6 characters nothing happens, but the charge is used. Reads the number. No clamp.

(The code has a second branch for `startsum` true, which is the first key after an operator. The button can't be pressed there: an operator blocks every other operator button, and `operator_money` is one.)

**Money moves during a line.** `StartLvl.coins` changes in these places, all deterministic:

| When | Change | Code |
|------|--------|------|
| Every charged press (glass too), from the 3rd charged press of the round, when `diff_type` ≥ 7 | − 1 if coins > 0 | `SubUse`, `press_count` (reset each round in `GoAwayRoutine`), `SubCoins(-1)` |
| Every charged press (glass too) in boss rule 10 | − 1 if coins > 0 | `SubUse`, `ChangeCoins(-1)` |
| Each `=` whose result is 777, with `passive777` | + 10 | `Send` |
| Each `=` whose result is divisible by 7 (0 too), with `passive_lucky` | + 1 | `Send` |
| Each `=` whose result is divisible by 10 (0 too), with `passive_perfect` | + 1 | `Send` |
| Each `=` whose result is odd, with `passive_odd` | + 1 | `Send` |

The `=` checks use the result before `passive_metronome` and before boss rule 12's + 1. The two drains apply in the order above, on the same press. `=` is not a charged press (it has no `CountUses`). The coin buttons `operator_coinadd` / `operator_coinsub` also change money but are unmapped, so a line never presses them.

### `==` (the game's `operator_equal`): `MainCalc.MorphAll()` + the next number key

- Like `a->c` / `b->c`: pressing it unblocks the number keys and blocks every other operator **and `=`**. The next number key finishes it (`AddNumber`, `currentoperand == 9`): `total = ReplaceDigits(operand, key)`, the result goes back to the left side.
- `operand` is the left side as a number at the press (leading zeros drop). `ReplaceDigits` writes the **first character** of the key's number once for every digit of `operand`: `483` + `7` → `777`; `483` + `10` → `111`; `05` + `8` → `8`; `0` + `4` → `4`; `52` + `0` → `0` (`long.Parse("00")`).
- The key can be any operand key: a digit, `10` (and `11`, `15`, `32`, `99` when they are supported), the charge key (`c12` → `1`), the quest key, a stored `mw`.
- 2 presses (the button + the key), 2 charges (one each).

### `mD` (the game's `number_mw`): `NumberReader`

Two states, saved with the run (`NumberReader.readed`, `RestoreAfterLoad`):

1. **Empty** (`readed` false, tag `operator_tag`, label `MW`): a press calls `MainCalc.Absorb()`, an operator that waits for one number key. That key's number is stored (`NumberReader.number`, `Setup()`), the button becomes a number key (tag `number_tag`) and gets back the charge the press used (`AddUse`). The left side doesn't change (leading zeros drop). "It cannot be overwritten."
2. **Stored** (`readed` true, label like `5|2`: number 5, 2 charges): a press calls `MainCalc.AddNumber(number)`, so it is an operand key that types its stored number, [right side] only, one charge per press. The stored number is whatever key was absorbed: 0–9, `10`, `c12`'s 12, and so on.

The solver supports state 2 only (the user's note: "treat as a digit"). State 1 is out of scope.

### `AX` (the game's `operator_complement`): `MainCalc.Complement()` + the next number key

(ilspycmd 9.1 on `MainCalc.Complement`, `AddNumber`, `AddToBegin`, `Cancel`; UnityPy onClick in `level1`, 2026-10-25.)

- Works only when the display text has **fewer than 6 characters** (leading zeros count). On 6 characters `Complement` returns at once, but the charge is used (`SubUse` ran first).
- Otherwise it works like `a->c` / `b->c` / `==`: the button shows `AX`, the number keys are unblocked, every other operator **and `=`** is blocked (the complement button too, so it can't be pressed twice). `currentoperand = 12`, `operand = total` (the left side as a number; leading zeros drop).
- The next number key finishes it (`AddNumber`, `currentoperand == 12`): `total = operand`, then `AddToBegin(number)`: `output.text = number.ToString() + total`, `total = Convert.ToInt32(output.text)`. Then `AddNumber` writes `total.ToString()` back to the display, so the result has no leading zeros. The result goes back to the left side.
- `number` is the whole number the key types, not one digit: `41` + `7` → `741`; `41` + `10` → `1041`; `41` + `c12` → `1241`; `04` + `7` → `74`; `0` + `7` → `70`; `41` + `0` → `41` (`"041"` parsed, then shown as `41`).
- The game doesn't check the length again: on a 5-character display a 2-digit key gives 7 characters (`99999` + `10` → `1099999`). No clamp.
- 2 presses (the button + the key), 2 charges (one each). `Cancel` gives the charge back (not used by the solver).
- It is a legendary button (Infos id 46): boss rule 9 blocks it (already handled through the `blocked` flag).

## Rules

### Solver

1. **Tokens and game ids** (`GameLink.MapButton`): `operator_near` → `near`, `operator_mediana` → `median`, `operator_sup` → `sup`, `operator_money` → `money`, `operator_equal` → `==`, `operator_complement` → `AX`. `number_mw` → `mD`, where D is the stored number from `puzzle.json`; an empty mw is left out and listed as ignored (`mw`).
2. **Buttons box:** `near:1`, `median:1`, `sup:2`, `money:2`, `==:1`, `m5:2`, `AX:1` (any case, like the other tokens). `near` and `median` need the goal (the G box) and `money` needs a money amount (a new Money box); without the amount, `money` is left out and the status says so (like `last` without a Last value).
3. **`near`** [left side], 1 press: `|G − v| ≤ 10` → G, else v + 10. Reads the number. No clamp.
4. **`median`** [left side], 1 press: `ceil((v + G) / 2)`. Reads the number.
5. **`sup`** [left side], 1 press: the digit sum, allowed only on a display of fewer than 6 characters (leading zeros count). Reads the number.
6. **`money`** [left side], 1 press: v + M, allowed only on a display of fewer than 5 characters (leading zeros count). M is the money **at that press, after the press paid** (rules 9–10). Reads the number. No clamp.
7. **`==`** [left side] starts it; exactly one operand key follows, then the line is back on the left side; no `=`. Result: the left side's digits (as a number, leading zeros dropped) all replaced by the first digit the key types. Lines show `== 7`.
8. **`mD`** [right side] operand key: types D (its decimal digits), like the digit keys and `10`, one charge per press, following the 5-character operand rule. Lines show `m5`. It is its own button: `m5` and the digit key `5` have separate charges.
8a. **`AX`** [left side] starts it, allowed only on a display of fewer than 6 characters (leading zeros count); exactly one operand key follows, then the line is back on the left side; no `=`. Result: the key's typed text (all of it: `10`, `c12` → `12`, `m5` → `5`) followed by the left side as a number, read as a number (leading zeros drop). Lines show `AX 7`. The solver drops an `AX` whose result has more than 6 characters (only a multi-digit key on a 5-character display does that).
9. **Goal at a press:** `near` and `median` read the goal shown at that moment. Normally that is G. In boss rule 14 it is G + the number of charged presses so far, **this one included** (`SubUse` runs first).
10. **Money at a press:** M starts at the published `coins` (or the Money box). The simulator applies the changes in the Background table: before each charged press's effect, the difficulty drain then the rule-10 drain; after each `=`, the passive gains. Searches may assume M doesn't change; the simulator checks every line with the real amounts and drops lines that don't reach the goal.
11. **Search phases:** `near`, `median`, `sup`, `money` are transforms, used by the arithmetic phase through `LeftSearch` (forward and backward), like `p` and `last`. `==` and `AX` get one-step lines in the arithmetic phase, like `a->c` / `b->c`, and can be either operator of a two-operator line. `mD` is an operand key in every phase that types operands (arithmetic, two-operator, division, modulo, remorph, `==` and `AX` keys), like the quest key but with no re-roll.
12. **Rule 12 (`plusOne`)** changes nothing in the buttons: `near` and `median` still read G (not G − 1).

### Mod (stays read-only)

13. `puzzle.json` and `state.json` get:
    - `coins`: `StartLvl.coins` at publish time.
    - `diffType`: `GamePrefs.GetInt("diff_type")`.
    - `coinPassives`: the names among `passive777`, `passive_lucky`, `passive_perfect`, `passive_odd` for which `GameObject.Find(name) != null` (the game's own test).
    - on the `number_mw` button: `"stored": <NumberReader.number>` when `readed` is true (no field when empty).
14. A change of `coins` alone doesn't republish (money changes with every press in some modes). A change of the mw state does (an absorb in the round changes the button set): fingerprint `|mw:<stored or empty>`.
15. **Calculator overlay:** `near`, `median`, `sup`, `money`, `==`, `AX`; mw shows `m<stored>` when stored and `mw` when empty, live (read each frame, no per-id cache). **Shop overlay:** same tokens; an mw offer is always empty, so `mw`.
16. `ButtonSymbols.Token` mirrors `GameLink.MapButton` for the six fixed ids (the linked test checks it); mw is handled like the charge and quest keys (`Of(id, …)` with the stored value).

### App

17. A **Money** box next to Last (Follow game fills it from `coins`). `GamePuzzle` gets `Coins`, `DiffType`, `CoinPassives`. The status adds `money $M` when the Buttons box has `money`.

## Examples

All with the buttons listed and enough charges; G = goal.

| S | G | Buttons | Line | Presses |
|---|---|---------|------|---------|
| 41 | 48 | `near:1` | `near` | 1 |
| 20 | 48 | `near:3` | `near near near` (30, 40, 48) | 3 |
| 47 | 48 | `median:1` | `median` | 1 |
| 46 | 48 | `median:2` | `median median` (47, 48) | 2 |
| 50 | 48 | `median:3` | none: from above `median` stops at 49 | |
| 941 | 14 | `sup:1` | `sup` | 1 |
| 123456 | 21 | `sup:1` | none: 6 characters | |
| 36 | 48 | `money:1`, Money 12 | `money` | 1 |
| 12345 | 12357 | `money:1`, Money 12 | none: 5 characters | |
| 483 | 777 | `==:1, 7:1` | `== 7` | 2 |
| 483 | 111 | `==:1, 10:1` | `== 10` | 2 |
| 103 | 48 | `-:1, m5:2` | `- m5 m5 =` (103 − 55) | 3 |
| 41 | 741 | `AX:1, 7:1` | `AX 7` | 2 |
| 41 | 1041 | `AX:1, 10:1` | `AX 10` | 2 |
| 04 | 74 | `AX:1, 7:1` | `AX 7` (leading zero drops) | 2 |
| 0 | 70 | `AX:1, 7:1` | `AX 7` | 2 |
| 123456 | 7123456 | `AX:1, 7:1` | none: 6 characters | |
| 12345 | 1012345 | `AX:1, 10:1` | none: the result has 7 characters (the game allows it; the solver drops it) | |

Money that moves (simulator):

- Money 5, `passive_lucky`, S = 14, G = 27, Buttons `+:1, 7:1, money:1`: `+ 7 =` gives 21 (divisible by 7, money 6), then `money` gives 27.
- Money 5, boss rule 10, S = 4, G = 46, Buttons `X1:1, money:1`: a search with M = 5 offers `X1 money` (41 + 5). In the game `X1` pays (money 4), `money` pays before it adds (money 3), so the display is 44. The simulator drops the line.
- Boss rule 14, S = 30, G = 48, `near:1`: the press raises the goal to 49 first, so `near` gives 40 (49 − 30 = 19 > 10). With S = 39 it gives 49, the raised goal: a winning line for G + 1 = 49 with k = 1.

## Out of scope

- An empty mw (absorbing a key). It changes the button set for the rest of the run, so it is a player decision. The solver ignores an mw with no stored number (user, 2026-10-25); no follow-up planned.
- Searches that plan around money changes (they assume M is fixed; the simulator just drops the lines that miss).
- Using the new transforms in the division and modulo phases' left edits (the transforms aren't used there yet).
- `operator_coinadd`, `operator_coinsub`, `passive_metronome`, `passive_cointobut` (the last only adds charges).
- A press that does nothing (`sup` on 6 characters, `money` on 5, `AX` on 6) as a way to spend a charge: the solver never makes it.
- An `AX` result over 6 characters (a display the game allows but the solver's display rules don't model).
- The `Cancel` button after `AX` (gives the charge back).

## Open questions

Answered by the user, 2026-10-25:

1. The token for `operator_equal` is `==` (lines show `== 7`).
2. An mw with no stored number is ignored (no absorb support planned).
3. Test V as planned checks the press order (`SubUse` first) for rules 14 and 10, and that `sup` / `money` on a too-long display still use the charge.

Still open (default used until the user answers):

4. Token for `operator_complement`: `AX`, the symbol the game shows on the display while it waits for the key. Rename before implementation if another token is wanted.
