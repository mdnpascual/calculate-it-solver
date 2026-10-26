# Requirements: Division Shortcut

**Status:** Implemented (v1). Working; the search is slow (about a second for larger puzzles), so the UI only recomputes on Enter. Speeding it up is a future ticket.

## Goal

Solve Engineering Calculator puzzles with **string edits plus integer division**. Edit S into a number X, divide by a divisor D so the quotient is a chunk q of G's digits, then edit the quotient into G.

The solver lists the candidate lines (press sequences) for a puzzle, cheapest first. Like the Modulo Shortcut, it applies known formulas instead of searching arbitrary press sequences.

## Scope: early-game Engineering Calculator

v1 covers the button set available at the start of the game:

| Button | Kind | Where it works | Effect | Cost |
|--------|------|----------------|--------|------|
| `dX` (for example `4X`) | edit | Left side only | Prepend digit d: `41` → `441` | 1 press |
| `Xd` (for example `X4`) | edit | Left side only | Append digit d: `41` → `414` | 1 press |
| `10` | digit key | Right side only | Append the digits `10` to the operand: `÷` `10` `10` → operand `1010` | 1 press |
| `reset` | | Left side | Set the entry to `0` | 1 press |
| `flip` | edit | Left side only | Reverse the digits: `41` → `14`, `123` → `321` | 1 press |
| `÷` | operator | Left side | Start a division; the right side begins | 1 press |
| `=` | | Right side | Apply the division; the result becomes the new left side | **Free**, unlimited |

**Sides:**

- The **left side** is the display before an operator: the start number S, or a result after `=`.
- The **right side** is the operand after `÷`, up to `=`.
- Edit buttons (`dX`, `Xd`, `flip`) work on the left side only. Digit keys (in v1, just `10`) work on the right side only.
- So in v1 the divisor can only be built from `10` presses: **D ∈ {10, 1010, 101010, …}**, costing 1 press per `10`.

Each `dX` and `Xd` is its own button, so a puzzle can have `4X` without `X4`. The puzzle input says which ones exist.

## Calculator rules

- The left-side entry is a digit string. Leading zeros are kept while editing (`0` → `X4` → `04`).
- **Division is whole-number only:** `X ÷ D` shows `floor(X / D)`, with the fraction dropped. `8411 ÷ 1010 = 8`.
- A computed result has no leading zeros.
- `flip` reverses the digit string as shown, keeping zeros: `120` → `021`. Leading zeros stay while editing (`120` → `flip` → `021` → `1X` → `1021`) and drop when an operator or `=` is pressed (`021` becomes 21). Confirmed in the game.
- `÷ 10` drops the last digit of X: `41 ÷ 10 = 4`. `÷ 1010`, `÷ 101010` and so on roughly drop 3, 5, … digits, nudged by the leading digits.

## The formula

Pieces:

- Split G's digits as `G = P ‖ q ‖ B`. q is the quotient: non-empty, with no leading `0`. P (prepended after `=`) and B (appended after `=`) can be empty.
- Build `X = A ‖ S ‖ T` on the left side: prepend the digits of A, append the string T (t digits). Let m = len(S) + t and L = value of `S ‖ T`, so X = A·10^m + L.
- D = `10` pressed r times (r ≥ 1).

`floor(X / D) = q` exactly when `q·D ≤ X ≤ q·D + D − 1`. Solving for A:

```
A ∈ [ ceil((q·D − L) / 10^m) ,  floor((q·D + D − 1 − L) / 10^m) ]
```

- A = 0 means no prepend. A > 0 must not start with `0`, since a leading zero adds presses without changing X.
- **Guarantee:** if D ≥ 10^m, the range always holds at least one value. The window is D wide and the X values are 10^m apart. The range has about D / 10^m values, so a bigger D gives more choices for A's digits.
- When D < 10^m, a solution needs the right digits already in place. For example, `÷ 10` only works when q is X with its last digit dropped.
- Direction doesn't matter: the same formula covers S < G and S > G.

Press sequence: edits that turn S into X, `÷`, `10` × r, `=`, edits that turn q into G.

**Cost** = edits(S → X) + 1 + r + edits(q → G). `=` is free.

### Left-side edits and `flip`

"Edits(from → to)" is the fewest left-side presses that turn one digit string into a longer one that contains it. Without `flip`, that's one `dX` per prepended digit, plus one `Xd` per appended digit. `10` can't be used here: it only works on the right side. With `flip`:

- **The kept digits may be reversed.** The target may contain the start string or its reverse. Flipping S first means X = A ‖ reverse(S) ‖ T (the formula uses L = value of reverse(S) ‖ T). Flipping the quotient last means G may contain reverse(q) instead of q.
- **Prepend and append can stand in for each other.** `flip Xd flip` prepends d, and `flip dX flip` appends d. A run of missing prepends costs 2 extra presses (flip, appends, flip), not 2 per digit.
- So when `flip` exists, A's digits may come from either the `dX` or the `Xd` set.

The solver finds edits(from → to) with a small shortest-path search instead of a formula (see the design). With no `flip` button, it gives the same costs as before.

## Examples

All buttons except `flip` available unless stated (`flip` examples are further down). These were checked with a scratch search over every split, r ≤ 3 and T up to 2 digits.

| Start → Goal | Split (P ‖ q ‖ B) | D | X | A range | Sequence | Presses |
|---|---|---|---|---|---|---|
| 41 → 48 (cheapest) | ‖ 4 ‖ 8 | 10 | 41 | [0, 0] | `÷ 10 = X8` | 3 |
| 41 → 48 | 4 ‖ 8 ‖ | 1010 | 8411 (T = `1`) | [8, 8] | `X1 8X ÷ 10 10 = 4X` | 6 |
| 41 → 48 | ‖ 48 ‖ | 1010 | 48541 | [485, 494] | `5X 8X 4X ÷ 10 10 =` | 6 |
| 41 → 48, no `8X` or `X8` | ‖ 48 ‖ | 1010 | 49041 | [485, 494], pick 490 | `0X 9X 4X ÷ 10 10 =` | 6 |
| 531 → 11 | ‖ 11 ‖ | 1010 | 11531 | [11, 11] | `1X 1X ÷ 10 10 =` | 5 |
| 7 → 250 | ‖ 25 ‖ 0 | 10 | 257 | [25, 25] | `5X 2X ÷ 10 = X0` | 5 |

### Limited buttons

The puzzle lists exactly which `dX` and `Xd` buttons exist. A, P and the prepends use only the `dX` set; T and B use only the `Xd` set.

| Start → Goal | Buttons | Cheapest lines | Presses |
|---|---|---|---|
| 41 → 48 | `4X`, `8X`, `X4`, `10`, `÷` | `4X 8X ÷ 10 10 = 4X` (441 → 8441; 8441 ÷ 1010 = 8), `X4 8X ÷ 10 10 = 4X` (8414 ÷ 1010 = 8), `8X 8X 4X ÷ 10 10 =` (48841 ÷ 1010 = 48) | 6 |
| 41 → 48 | `4X`, `X4`, `10`, `÷` | None: no button makes an `8` | — |
| 531 → 11 | `1X`, `10`, `÷` | `1X ÷ 10 10 = 1X` (1531 ÷ 1010 = 1), `1X 1X ÷ 10 10 =` | 5 |

The no-`8X`/`X8` row in the first table shows why the general form matters.

### With `flip`

Checked with a scratch prototype of the shortest-path edit search.

| Start → Goal | Buttons | Cheapest line | How | Presses |
|---|---|---|---|---|
| 41 → 48 | default (see UI) | `8X ÷ 10 = flip` | 841 ÷ 10 = 84, flip → 48 | 4 |
| 41 → 48 | default | `÷ 10 = 8X flip` | 41 ÷ 10 = 4, `8X` → 84, flip → 48 | 4 |
| 7 → 250 | `X0`, `X2`, `X5`, `flip`, `10`, `÷` (no prepends) | `X2 flip ÷ 10 = X5 X0` | 72, flip → 27, ÷ 10 = 2, → 25 → 250 | 6 |
| 7 → 250 | same, without `flip` | None | | — |
| 531 → 11 | `1X`, `flip`, `10`, `÷` | `1X 1X ÷ 10 10 =` (flip doesn't help) | | 5 |

Without `flip`, 41 → 48 with the default buttons is 6 presses (`5X 8X 4X ÷ 10 10 =`). The 3-press line needs `X8`, but D = 1010 opens the range [485, 494], and 490 avoids the missing buttons.

## Inputs

- S (≥ 0) and G (≥ 1).
- The available buttons: which of `0X`–`9X` and `X0`–`X9` exist, and whether `10` and `reset` exist. `÷` and `10` must exist for this shortcut.
- A maximum press count, to bound the search over r and T.

## Output

Lines sorted by cost, cheapest first. Each line shows:

- The expression (`8411 ÷ 1010 = 8`, then `4X` → `48`).
- The press sequence, with the entry after each press.
- The total press count.

Every line is checked by simulating its presses; a line only appears if the simulation ends on G.

If no line fits the available buttons and press limit, the solver returns no lines and an error saying so. It's not a crash.

## UI

The existing form gets a Division Shortcut section, using the same S and G boxes:

- **Buttons** textbox: the available buttons, separated by commas, for example `4X, 8X, X4, 10, ÷`.
  - Accepted tokens: `dX` and `Xd` for d = 0–9, `10`, `reset`, `÷` (or `/`), `mod`, `flip`, `=`. Case and spaces don't matter (`4x` = `4X`), and duplicates are ignored.
  - `=` is always available, whether listed or not. `mod` is accepted but this shortcut doesn't use it.
  - An unknown token shows an error naming it (for example `Unknown button "Y4"`), and no lines are shown.
  - If `÷` or `10` is missing, show an error saying the Division Shortcut needs them.
- **Results**: the cheapest lines (up to 10), one per row, showing the press sequence, the expression and the press count. For example: `4X 8X ÷ 10 10 = 4X   8441 ÷ 1010 = 8 → 48   (6 presses)`.
- A status label shows errors, or the number of lines found.
- Results recompute when Enter is pressed in the S, G or Buttons box, and once at startup. (Originally this was on every change, like the Modulo Shortcut section, but the search lags too much for that. While typing, the status label says "Press Enter to recalculate." and the old lines stay.)
- The default Buttons text is `5X, X1, X2, X7, X4, 4X, 6X, 3X, 8X, 10, /, X9, flip, mod, =, reset`. With S = 41, G = 48 the cheapest lines are 4 presses: `8X ÷ 10 = flip` and `÷ 10 = 8X flip`. (There's no `X8`, so `÷ 10 = X8` is out.)

## Edge cases

- S = G: zero presses, no lines needed.
- G = 0: out of scope for this shortcut (use `reset`).
- Splits where q starts with `0` are skipped.
- A digit needed in A, P, T or B whose button is missing (in both directions, when `flip` exists) rules out that choice, not the whole puzzle.
- Pressing `dX`, `Xd` or `reset` on the right side is invalid. After an operator, only digit keys can be pressed. (`reset` on the right side is untested in the game, but believed not to work.)
- Numbers that overflow `long` are skipped.

## Open questions

- Are there per-button use limits in the early game? v1 ignores limits; [cost-per-line](../cost-per-line/requirements.md) covers them.

## Out of scope

- **Single digit keys (mid/end game, v2).** Keys like `3` and `7` add to the operand, like `10` (right side only), so they let the divisor be any number. The formula stays the same, but D can be anything you can type, and the cost of D changes. Specced in [digit-keys](../digit-keys/requirements.md).
- More than one division in a line (for example `÷ 10 = ÷ 10 =`). Each extra `÷ 10 =` drops one more digit for 2 presses.
- Chaining several operations before `=`, and combining division with `mod`.
- Use limits per button, and a press-limit field in the UI (the UI uses a fixed limit).
- Feeding the Buttons textbox into the Modulo Shortcut section.
- A general search over arbitrary press sequences.
