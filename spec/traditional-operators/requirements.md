# Requirements: Traditional operators (+, −, ×)

**Status:** Complete (2026-10-22).

## Goal

The game also has the usual calculator operators: `+` (`operator+`), `-` (`operator-`) and `×` (`operator*`, labelled `x`). Some puzzles are a plain calculator with digit keys and these operators (for example round 1: 55 → 77 with `0`–`9`, `+`, `-`, `×`, `/`), and later puzzles mix them with `dX`, `Xd`, `flip`, `mod` and `÷`.

After this spec the solver finds **one-operator lines** with `+`, `-` and `×`, the same way the Division Shortcut handles `÷`: left-edit S into X, press the operator, type the operand D, press `=`, then left-edit the result Y into G. It also finds **edits-only lines** (no operator at all), such as 36 → 63 with a single `flip`, which no phase finds today.

## Game rules (confirmed in the game)

1. `+`, `-` and `×` work like `÷` and `mod`: pressed on the left side, they start the right side; the operand is typed with digit keys (`10`, `0`–`9`); `=` applies it.
2. **One operation per `=`.** After an operator, only digit keys and `=` can be pressed. Another operator does not chain (`3 × 2 + 1` is not a thing): the line must press `=` first.
3. **No negatives.** When `X − D` would be below 0, the result is `0`.
4. The display fits about 18 digits. No start or goal has been seen above 4 digits.
5. Digit keys work on the right side only, in every calculator. On a plain calculator (no `dX`, `Xd`, `flip`, `reset`) the first press of a line must be an operator.

## Rules

### 1. Buttons

1. The Buttons box accepts `+`, `-` and `×`, each with a charge count like every other button (`+:2`, `-:2`, `×:2`). `*` and `x` are accepted for `×`, and `−` for `-`.
2. The game feed maps `operator+` → `+`, `operator-` → `-`, `operator*` → `×`.
3. Charges, `:0`, spare and drained work as for every other button.

### 2. Simulator

1. `+`, `-`, `×` are left-side only, like `÷` and `mod`.
2. `=` gives `X + D`, `max(0, X − D)` or `X × D`. An operand of 0 is allowed for these three (only `÷` and `mod` reject it), though the search never types one.
3. A result of 10^18 or more shows `max digits reached` as that step's entry. Nothing can be pressed after it, so the line never reaches G and is dropped. (Corrected 2026-10-25: in the game `+` and `×` results above 999 999 become 999 999 and play goes on; see [`display-cap/`](../display-cap/).)

### 3. One-operator lines

A line is `pre`, operator, operand, `=`, `post`:

| Op | Operand D | Kept when |
|----|-----------|-----------|
| `+` | Y − X | D ≥ 1 |
| `-` | X − Y | D ≥ 1 |
| `×` | Y / X | X ≥ 1, X divides Y, D ≥ 2 |

- **X** (what S is edited into before the operator) comes from the same set the Division Shortcut uses: `a ‖ core ‖ t`, where core is S (or reversed S with `flip`), a is 0–2 prepended digits and t is 0–2 appended digits, with `pre` = the fewest left edits from S.
- **Y** (the result that is edited into G) is any piece of G's digits (or its reverse with `flip`) without a leading 0, with `post` = the fewest left edits from Y to G. Y = G gives an empty `post`.
- **Operand:** the fewest presses of `10` and the digit keys that type D (`TypeOperand`). D starting with 0 can't be typed.
- **Cost** = pre + 1 + operand presses + post. `=` is free.

Examples (enough charges):

| Buttons | S → G | Line | Cost |
|---------|-------|------|------|
| `0`–`9`, `+`, `-`, `×`, `/` | 55 → 77 | `+ 2 2 =` | 3 |
| `+`, `7` | 41 → 48 | `+ 7 =` | 2 |
| `-`, `8`, `4` | 91 → 7 | `- 8 4 =` | 3 |
| `×`, `4` | 12 → 48 | `× 4 =` | 2 |
| `×`, `4`, `X2` | 6 → 242 | `× 4 = X2` (6 × 4 = 24, then `X2`) | 3 |
| `1X`, `+`, `3`, `7` | 3 → 50 | `1X + 3 7 =` (13 + 37) | 4 |

### 4. Edits-only lines

When the left-edit search can turn S directly into G (G contains S, or reversed S with `flip`), that is a line too: `LeftEdits.Plan(S, G)`, no operator. Example: 36 → 63 with `flip` → `flip`, cost 1.

### 5. Which lines are kept

1. Cost at most the app's press limit (12), buttons and charges fit, and the simulator ends on G.
2. Two lines with the same presses count once.
3. Keep the 100 cheapest (ties: the ranking in rule 6).

### 6. When the search runs, and ranking

1. The arithmetic search (rules 3 and 4) runs **first**, when Enter is pressed or a game puzzle arrives. It takes well under a second, so its lines appear before the Division Shortcut's.
2. Then the Division Shortcut runs, then the Modulo Shortcut. If the arithmetic phase found lines, the division press limit is `min(12, cheapest arithmetic cost + 2)`; otherwise it stays 12. The modulo cost cap is `min(12, cheapest cost among all lines found so far + 2)`, so with no arithmetic line the best comes from division. (Changed 2026-10-22 after playing: was no division cap and best + 1 for modulo.)
3. If S = G, nothing runs (as now).
4. One list, ranked as now: cost, drained (fewest), spare (highest), then kind: **arithmetic, division, modulo**. Arithmetic lines among themselves: the order they were kept (cheapest first, then press text).
5. Status after all phases: `A arithmetic line(s), N division line(s), M modulo line(s), R rejected`. Lines rejected for charges in the arithmetic phase are not counted in R (same as modulo).
6. The arithmetic search never reports an error: with no `+`, `-` or `×` it still looks for edits-only lines.

### 7. Optional, not in v1: cap the division search

**Status:** Done in a changed form (2026-10-22): the division cap is cheapest arithmetic cost + 2 (see rule 6.2), and the modulo cap is best + 2.

Idea: run the Division Shortcut with press limit `min(12, cheapest arithmetic cost + 1)` instead of 12, the same "best + 1" rule the modulo phase uses. A smaller budget cuts most of the division work, so it could drop from seconds to well under a second when an arithmetic line is cheap.

Why it might not pay off:

- `+`, `-` and `×` usually have few charges (2 in the rounds seen so far), so they run out quickly and the arithmetic phase often finds nothing. The cap then stays at 12 and nothing changes.
- A cheap arithmetic line can drain a scarce operator. The cap would hide division lines one or two presses longer that keep it, and the player may prefer those.

Decide after playing: if division lines above the cap were never picked, turn it on.

## Decisions taken

- Basic operators first. Multi-operator lines and the other unusual buttons are separate specs (see Out of scope).
- Arithmetic runs before division, because it is fast and its lines are often the cheapest.
- The operator label for multiplication is `×`, matching `÷`.
- Digit keys never type on the left side, even on a plain calculator.
- Results of 10^18 or more show `max digits reached`. (Corrected 2026-10-25: the game clamps at 999 999; see [`display-cap/`](../display-cap/).)

## Open questions

None.

## Out of scope

- **Multi-operator lines** (for example `× 3 = + 7 =`, or `÷` then `+`). Planned as a follow-up: meet in the middle on the value after the first `=`, forward one segment from S, backward one segment from G with the inverse operators, join on a hash map, then check in the simulator.
- **`number_charge`** (`9|9`): a digit key that types its current charge (9, then 8, …) and then loses one charge. Its behavior is known, so it can be the next small spec.
- Unknown buttons seen in the game, behavior not yet checked: `operator_+2` (`X+2`), `operator_-4` (`X-4`), `operator_--` (`X--`), `operator_2to4` (`2->4`), `operator_energy`. They are still ignored by `MapButton`.
- Threads for the arithmetic search (only if it turns out slow).
