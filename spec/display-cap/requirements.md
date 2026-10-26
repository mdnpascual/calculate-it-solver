# Requirements: the 999 999 cap and the 6-character display

**Status:** In testing (implemented 2026-10-25). Waiting for Test O (in-game).

## Why

The solver lets numbers grow to 10^18 and then shows `max digits reached`, after which no press is allowed. The game works differently: results above 999 999 become 999 999 and play goes on (the user can still press buttons; seen in-game), and the display holds at most 6 characters, so `Xd` and `dX` stop working at 6. The solver lists lines the game can't play (a k = 4 Modulo line `flip X0 X0 X0 X4 X5 X1 flip …` builds 7 digits) and misses lines that use the clamp.

This replaces the follow-ups "the 6-character display limit and the 999 999 cap" in `leading-zeros-flip-and-prepend/` and `more-buttons/`.

## Game facts (static analysis of `MainCalc`, `Assembly-CSharp.dll`, 2026-10-25)

### Clamp to 999 999

| Button | Game method | Rule |
|--------|-------------|------|
| `+` | `Send` (and `Sum` when chaining) | `left + D > 999 999` → 999 999 |
| `×` | `Send` (and `Multiply`) | `left × D > 999 999` → 999 999 |
| `X+k`, `X++` | `PlusNumber` | `v + k > 999 999` → 999 999 (`X-k`, `X--`: `max(0, v − k)`, same method) |
| `X*2` | `MultNumber` | `2v > 999 999` → 999 999 |
| `<<` | `ShiftNumberleft` | `2v > 999 999` → 999 999 |
| `sqr` | `Sqr` | `v² > 999 999` → 999 999 |
| `>>` | `ShiftNumber` | has the same check, but `>>` never grows |

Reaching the cap unlocks the `MAX` achievement (`+`, `×`, `sqr`); nothing else happens. The line goes on from 999 999.

### No clamp

| Button | Biggest result from 999 999 or less |
|--------|-------------------------------------|
| `p` (`Prime`) | `999 999` → `1 000 003` (7 characters) |
| `round` (`Round`) | `999 995` → `1 000 000` (7 characters) |
| `%` | `left × D / 100`; the solver already rejects results above 2^24 |
| `-`, `÷`, `mod`, `X/2`, `>>`, `sqrt`, `flip`, `25\|69`, digit maps, `to25`, `100-X`, `a->c`, `b->c`, `reset`, `last` | can't grow past the input (or are small) |

Rule 12's +1 comes after the clamp (`Send`), so `999 999` + 1 = `1 000 000`.

### 6-character display

| Button | Game method | Rule |
|--------|-------------|------|
| `Xd` | `AddToEnd` | does nothing when the display text has 6 or more characters (leading zeros count: `000041`) |
| `dX` | `AddToBegin` | same |
| Operand keys (`10`, `0`–`9`, `c`, `q`) | `AddNumber` | the first key after the operator starts the operand. After that a key appends only while the operand has fewer than 5 characters; at 5 or more, the key **restarts** the operand with its own digits |

So an operand has at most 5 characters, or 6 when the last key adds two (`10`, `c12`): `10 10 10` = `101010` works, a fourth `10` gives `10` again. `9 9 9 9 9` = `99999`; a sixth `9` gives `9`.

Not checked: whether a press that does nothing (`Xd` at 6 characters) still spends a charge. The solver never makes such a press, so it doesn't matter.

## What the solver must do

1. **One cap: 999 999.** Every place that uses 10^18 (`Limit` in `EngineeringCalculator`, `LeftTransforms`, `TwoOperatorSearch`) uses `EngineeringCalculator.Max = 999_999` instead.
2. **Clamp, don't stop.** `+`, `×`, `X+k`, `X++`, `X*2`, `<<`, `sqr` give `min(result, 999 999)`. `p`, `round` and `%` keep their real result. Nothing ends a line any more: `max digits reached` (`MaxDigits`, the `full` state in `Run`) goes away.
3. **`Xd` / `dX` only below 6 characters.** The simulator rejects an `Xd` or `dX` pressed on a display of 6 or more characters (the game would ignore it, so the line is wrong). `LeftEdits` and `LeftSearch` never make such a press. So does every phase that builds a left-side number with them (division `A ‖ core ‖ T`, modulo prefixes and padding, arithmetic `X` and `Y`).
4. **Operands up to 5 characters, or 6 ending in a two-character key.** The simulator rejects an operand key pressed when the operand already has 5 or more characters (in the game it would restart the operand; the solver doesn't use that). `TypeOperand`, `CheapestDivisor`, the division Path A (`10` pressed r times: r ≤ 3) and the two-operator operands follow it.
5. **Searches are smaller.** Division and modulo no longer build X above 6 characters, so many candidates go and the searches are faster. Lines that need more than 6 characters are no longer listed (they never worked in the game).
6. **Backward searches.** `LeftSearch.Backward` and the two-operator backward half use exact preimages. A value of 999 999 reached through a clamp (`X*2` from 600 000) is found only by the forward search. This is accepted: 999 999 as G, or as the value before the last edits, is rare.
7. **Display texts** in the Lines box don't change; a clamped step simply shows `999999`.

## Examples

| S | Presses | Today | After |
|---|---------|-------|-------|
| 999 000 | `+ 5 0 0 0 =` | `1004000` | `999999` |
| 600 000 | `X*2` | `1200000` | `999999` |
| 1 000 | `sqr` | `1000000` | `999999` |
| 999 999 | `p` | `1000003` | `1000003` |
| 123 456 | `X7` | `1234567` | rejected |
| 12 345 | `X6` | `123456` | `123456` |
| 41 | `÷ 10 10 10 10 =` | allowed | rejected (fourth `10` on a 6-character operand) |
| 41 | `÷ 9 9 9 9 9 =` | allowed | allowed (`99999`) |
| 999 999 999 999 | anything | worked | S can't have more than 6 digits in the game; the solver still accepts what it is given |

## Out of scope

- How the game picks S and G (assumed to fit in 6 digits; not checked).
- The operand restart (a key on a 5-character operand starting a new operand) as a trick.
- Left-side presses that do nothing (`Xd` at 6 characters) as a way to spend a charge.
- `Secret`, `Sup`, `Complement` (they also stop at 6 characters) and other buttons the solver doesn't map.

## Open questions (Test O)

- O1: `999 000` `+ 5000 =` shows `999999` and the next press works (the user saw the clamp and that buttons still work; confirm with a `+` line).
- O2: `Xd` on a 6-character display does nothing.
- O3: a sixth digit key on a 5-character operand restarts it (`9 9 9 9 9 9` → `9`).
- O4: does `p` on `999999` show `1000003`?
