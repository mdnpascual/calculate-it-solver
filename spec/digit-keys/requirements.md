# Requirements: Digit keys

**Status:** Done.

**Correction (2026-10-22):** digit keys (`10` included) work on the right side only. This spec first said they work on both sides, and the code did too. The text below is corrected; the code was fixed by [digit-keys-right-side-only](../digit-keys-right-side-only/requirements.md).

## Goal

Let the Buttons textbox take single digit keys `0`–`9`, the mid/end-game keys, next to the `10` key, and use them in the Division Shortcut. This spec also covers two small UI changes to the Division Shortcut section (see "UI changes").

This is the "Single digit keys (v2)" item from [division-shortcut](../division-shortcut/requirements.md#out-of-scope).

## Digit keys

| Button | Where it works | Effect | Cost |
|--------|----------------|--------|------|
| `d` (for example `3`) | Right side only | Add the digit d to the operand: `÷` `4` `5` → operand `45`. Not allowed on the left side | 1 press |
| `10` | Right side only | Append the digits `10` to the operand | 1 press |

- A digit key is not the same button as `Xd`. `Xd` only works on the left side, and the digit key only on the right side. A puzzle can have the key `3` without `X3`, or the other way round.
- On the right side, the operand is built from digit keys only (`10` and any single digit keys). So the divisor D can be any number whose digits can be typed with the available keys, not just 10, 1010, 101010, ….
- Cost of typing D: the fewest key presses whose digits, joined, spell D. For example, with keys `1`, `0` and `10`, D = `1010` costs 2 (`10 10`), and D = `100` costs 2 (`10 0`, not `1 0 0`).
- An operand with a leading zero (`0` `5` → `05`) means 5, but the `0` press is wasted, so the solver never builds one. D = 0 is invalid. D = 1 is skipped, because `÷ 1` changes nothing.

## Buttons textbox

- New accepted tokens: `0`, `1`, …, `9`. Case and spaces don't matter, and duplicates are ignored, as before.
- `10` is still its own key. Other digit strings (`25`, `100`) are still unknown buttons and show the error `Unknown button "25"`.
- The default Buttons text doesn't change, so the default result doesn't change.
- Nothing else in the form uses digit keys. The Modulo Shortcut section keeps its own inputs.

## Division Shortcut with digit keys

The formula is the same: edit S into X = A ‖ core ‖ T, press `÷`, type D, press `=` so that floor(X / D) = q, then edit q into G. Only D changes:

- Before: D = `10` pressed r times, cost r.
- Now: D is any typeable number ≥ 2, cost = presses to type it (see above).
- For a given X and q, floor(X / D) = q exactly when `X / (q + 1) < D ≤ X / q`. So once X is chosen, the solver picks the cheapest typeable D in that range.

Lines from the `10`-only search must still appear. With no single digit keys in the Buttons box, the results must be exactly what they are today.

### Examples

Each was checked with a brute-force search over press sequences (at most one `÷`, up to 8 presses). "Cheapest" means no line with fewer presses exists in that search.

| Start → Goal | Buttons | Cheapest line | How | Presses |
|---|---|---|---|---|
| 531 → 11 | `1X`, `10`, `÷` (no digit keys) | `1X 1X ÷ 10 10 =` | 11531 ÷ 1010 = 11 (unchanged from today) | 5 |
| 531 → 11 | `1X`, `10`, `4`, `5`, `÷` | `÷ 4 5 =` | 531 ÷ 45 = 11.8 → 11 | 3 |
| 41 → 48 | `4X`, `X4`, `10`, `8`, `÷` | None (the key `8` can't append on the left, and no typeable D gives q = 8 or 48) | | — |
| 41 → 48 | `4X`, `X4`, `10`, `÷` | None (no button makes an `8`) | | — |
| 7 → 250 | `X0`, `X2`, `X5`, `10`, `3`, `÷` | `X5 X0 ÷ 3 =` | 750 ÷ 3 = 250 | 4 |
| 1234 → 7 | `1`, `7`, `10`, `÷` | `÷ 1 7 1 =` | 1234 ÷ 171 = 7.2 → 7 | 4 |
| 41 → 48 | default + `3` | `5X flip ÷ 3 =` (ties with `8X ÷ 10 = flip`) | 541, flip → 145, ÷ 3 = 48.3 → 48 | 4 |

In the 1234 → 7 row, D can be anything in (154.25, 176.28], and 171 is the only one typeable with `1`, `7` and `10` in 3 presses.

## UI changes

These are separate from digit keys, but this spec covers them too.

### 1. Lines show only the presses and the count

Each row of the Lines box shows the press sequence and the press count, and nothing else:

```
8X ÷ 10 = flip   (4 presses)
÷ 10 = 8X flip   (4 presses)
```

The worked math (`841 ÷ 10 = 84 → 48`) is dropped from the row. The solver still checks every line with the simulator. This only changes what is shown.

### 2. No search at startup

The Division Shortcut search lags, so the app no longer runs it when it opens:

- On startup, the Lines box is empty and the status label says "Press Enter to recalculate." (the same message shown while typing).
- The first search runs when Enter is pressed in the S, G or Buttons box, as it does now.
- The Modulo Shortcut section still computes at startup (it's instant).

## Edge cases

- Digit keys on the right side are valid; on the left side they're invalid. `dX`, `Xd`, `reset` and `flip` on the right side are still invalid.
- A digit key needed for D or for edits that's missing rules out that choice, not the whole puzzle.
- D values that overflow `long` are skipped.
- S = G: no lines, no error (unchanged).

## Open questions

- Does the game have multi-digit keys other than `10` (for example `25` or `100`)? Not supported until seen in the game.
- Does a digit key count against the same use limit as `Xd`, if use limits exist? See [cost-per-line](../cost-per-line/requirements.md).

## Out of scope

- Using digit keys in the Modulo Shortcut (for example `mod 103`, as in CLAUDE.md's example).
- More than one division in a line.
- Making the search faster. Item 2 above only stops it from running at startup.
- A general search over arbitrary press sequences.
