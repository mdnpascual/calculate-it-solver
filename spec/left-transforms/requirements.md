# Requirements: Left-side transforms

**Status:** Complete (2026-10-23). All game rules confirmed.

## Goal

Calculate It has many buttons that change the left side's value in one press: `X++`, `X--`, `X+2`, `X-4`, `X*2`, `X/2`, the bit shifts, `sqr`, `sqrt`, the digit maps (`2->4`, ...), `to25` and `round`. The solver ignores all of them today (`GameLink.MapButton` returns null), so puzzles that need them get no lines, or only longer lines.

After this spec:

1. The Buttons box, the game feed and the simulator know these buttons.
2. A new left-side search finds the fewest left-side presses between two displays using **every** left-side button (`dX`, `Xd`, `flip`, `reset` and the new ones). `LeftEdits` only handles `dX`, `Xd` and `flip`, because it assumes the display is always a piece of the target. That isn't true once `X++` or `sqr` can be pressed.
3. The arithmetic phase uses the new search for edits-only lines (S → G with no operator) and for the edits before and after its one operator.

The Division and Modulo Shortcuts are unchanged in this spec (see Out of scope).

## Game rules (confirmed by the user, 2026-10-23)

All of these buttons work on the **left side only**: the start number, the number being built before an operator, or a result after `=`. They can't be pressed after an operator.

| Game id | Solver token | Effect on the left side | Example |
|---------|--------------|-------------------------|---------|
| `operator_++` | `X++` | add 1 | `41` → `42` |
| `operator_--` | `X--` | subtract 1, not below 0 | `0` → `0` |
| `operator_+k` (k = 2, 4, 8, 10, 16, 32, 64) | `X+k` | add k | `41` + 10 → `51` |
| `operator_-k` (k = 4, 8, 10, 16, 32, 64) | `X-k` | subtract k, not below 0 | `5` − 10 → `0` |
| `operator_*2` | `X*2` | multiply by 2 | `41` → `82` |
| `operator_/2` | `X/2` | divide by 2, fraction dropped | `55` → `27` |
| `operator_<` | `<<` | bit shift left (multiply by 2) | `6` → `12` |
| `operator_>` | `>>` | bit shift right (divide by 2, fraction dropped) | `6` → `3`, `55` → `27` |
| `operator_sqr` | `sqr` | square | `9` → `81` |
| `operator_sqrt` | `sqrt` | square root, rounded to the nearest whole number (.5 rounds up) | `10` → `3`, `99` → `10` |
| `operator_atob` (a, b = 0–9, a ≠ b) | `a->b` | change every digit a to b | `3->0` on `35` → `05` |
| `operator_to25` | `to25` | set the left side to `25` | `41` → `25` |
| `operator_round` | `round` | round to the nearest 10: last digit 0–4 rounds down, 5–9 rounds up, 0 stays | `54` → `50`, `55` → `60`, `95` → `100`, `4` → `0` |

Leading zeros:

1. **Number buttons** (`X++`, `X--`, `X+k`, `X-k`, `X*2`, `X/2`, `<<`, `>>`, `sqr`, `sqrt`, `round`) read the left side as a number, so leading zeros are dropped: `021` then `X++` gives `22`. Their result has no leading zeros.
2. **Digit maps** (`a->b`) work on the digits as shown and keep any leading zero they create: `3->0` on `35` gives `05`. After that, `5X` gives `505` and `flip` gives `50`, the same as any other leading zero on the left side. A leading zero already on the display counts as a digit too: `0->8` on `05` gives `85` (confirmed 2026-10-23).
3. **`to25`** shows `25`.

Limits:

1. **No negatives.** Anything that would go below 0 gives 0.
2. **Max digits.** A result of 10^18 or more shows `max digits reached`, like `×` does today. After that no button can be pressed at all, including these ones (confirmed 2026-10-23). **Corrected 2026-10-25** ([`display-cap/`](../display-cap/)): buttons keep working; `X+k`, `X*2`, `<<`, `sqr` clamp results above 999 999 to 999 999; `p` and `round` aren't clamped.
3. `<<` and `X*2` (and `>>` and `X/2`) give the same value on this calculator, but they are separate buttons with separate charges.

## Rules

### 1. Buttons

1. The Buttons box accepts the tokens in the table, each with a charge count like every other button (`X++:2`, `X+16:1`, `2->4:1`, `to25:1`). Case and spaces are ignored. `X+k` and `X-k` accept any whole number k ≥ 1, so new amounts need no code change.
2. `−` is accepted for `-` inside a token (`X−4`).
3. `:0` leaves the button out. Charges, spare and drained work as for every other button.
4. The game feed maps the ids in the table to the tokens. `operator_last`, `operator_100-x`, `operator%` and the other unknown ids stay unmapped (ignored).
5. The new buttons don't change how the existing tokens parse (`x4` is still an append, `x` alone is still `×`, `+` is still the operator).

### 2. Simulator

1. Each new button is valid on the left side only and applies its effect to the display, following the leading-zero rules above.
2. Pressed on the right side, or missing from the buttons when buttons are given, the press is invalid (the run returns null), like `flip` or `reset` today.
3. A result of 10^18 or more shows `max digits reached` and ends the run, as for `×`. (Corrected 2026-10-25: the game clamps at 999 999 and the run goes on; see [`display-cap/`](../display-cap/).)

### 3. Left-side search

A search for the fewest left-side presses that turn one display into another, using every left-side button the puzzle has (`dX`, `Xd`, `flip`, `reset` and all new buttons). It must:

1. Give the fewest presses (breadth-first, or meet in the middle with the same guarantee within its limits).
2. Never use a button more often than its charges along one path.
3. Stay within limits: a press cap, a digit cap on intermediate displays (at most 18, and by default a few digits longer than S or G), and a cap on the number of states explored. When a limit cuts the search, a longer or different line may be missed. This is accepted, and the limits are documented.
4. Be cancellable.

### 4. Arithmetic phase

1. **Edits-only lines:** the left-side search connects S to G directly. Several distinct edits-only lines may be kept (cheapest first), not just one. Examples: 41 → 42 with `X++` → `X++` (1 press); 35 → 505 with `3->0` and `5X` → `3->0 5X` (2 presses); 41 → 84 with `X++` and `X*2` → `X++ X*2` (2 presses).
2. **One-operator lines:** besides today's candidates, X (what S becomes before the operator) can be any display the left-side search reaches from S, and Y (the result after `=`) can be any number from which the left-side search reaches G. Example: 12 → 97 with `×`, `8`, `X++` → `× 8 = X++` (12 × 8 = 96, then 97; 3 presses).
3. With none of the new buttons in the puzzle, the arithmetic phase gives exactly the same lines as before (all existing tests pass unchanged).
4. Every line is still checked in the simulator (it must end on G) and against the charges, and keeps the existing caps (press limit 12, up to 100 lines, cost cap rules for division and modulo).
5. Lines that use the new buttons are arithmetic lines in the merged list (same ranking and status line as today).

### 5. Speed

With every button (all digits, all operators, all new buttons), the arithmetic phase should stay under about 1 second in Release for puzzles like 41 → 483, so it still shows its lines before the Division Shortcut starts. If it's slower, lower the state caps before adding threads.

## Decisions taken

- All the "value in, value out" left-side buttons go in one spec, because they share one mechanism (a function on the left side) and one search.
- The bit shifts are in scope: on this calculator they are ×2 and ÷2 (fraction dropped), so they need no memory.
- New buttons are data (a catalogue of transforms), not new search code per button.
- Division and modulo keep `LeftEdits` for now, so their speed and results don't change.
- Number keys `11`, `15`, `32` and `99` are a separate spec. They are the only multi-digit keys found in the game, so that spec hard-codes them instead of supporting any multi-digit key.

## Open questions

None.

## Out of scope

- **`operator_last`** (brings back the previous goal): needs the solver to remember the previous puzzle. Separate spec.
- **Number keys `11`, `15`, `32`, `99`**: separate spec (right side, hard-coded keys).
- **Using the new buttons in the Division and Modulo Shortcuts** (for example `÷ 10 =` then `X++`). Planned follow-up: reuse the left-side search's backward map for the step after `=`.
- **Multi-operator lines.**
- `number_charge`, `operator_100-x`, `operator%`, `operator_cut`, `operator_swap`, `operator_near`, `operator_prime`, and the other ids with no description yet.
- Other calculators (binary, glass, ...). Only the Engineering Calculator rules above are modelled.
