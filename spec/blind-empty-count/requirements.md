# Requirements: blind passive and the empty count (correctness)

**Status:** Complete (2026-10-29). Minor: it only changes the ranking and the `empty N` text.

## Why

A line's `empty N` counts the buttons the line leaves at 0 charges (`Buttons.Drained`). The Lines box ranks on it: cost first, then fewer empty buttons, then more spare. With `passive_blind` the round's first non-glass press is free (`CountUses.SubUse` skips `uses − 1`; see `spec/blind-passive-rule-14/requirements.md`). The solver still counts that press as a charge.

Example: Buttons `p:1, …`, Blind on, the line is `p`. In the game `p` keeps its 1 charge. The solver says `empty 1`. It ranks the line below an equal-cost line that really empties nothing, and the text is wrong.

## What the solver must do

1. **Free press.** With Blind on (`LineContext.Blind`), the first press of the line that is not `=` and not a glass button uses no charge. Count the other presses as today.
2. **Empty count.** `Drained` counts a distinct button when `pressed − (1 if it is the free press's button) >= charges`. A button with `n` charges pressed `n` times is still empty, unless one of those presses was the free one.
3. **Ranking and text.** `ResultLine.Ranking` is unchanged; it reads the corrected `Drained`, and the `empty N` text follows.
4. **Blind off:** unchanged.
5. **Whole-line rule.** The free press is the first non-glass press of the **whole** line: a leading `swap` (non-glass) is the free press, and so is the first press of a `$++ (n press)` prefix. Glass presses don't use it, so the free press moves to the next non-glass press.

## Examples

Blind on:

| Buttons | Line | Today | Wanted |
|---------|------|-------|--------|
| `p:1, =` | `p` | `empty 1` | no `empty` |
| `X++:2, =` | `X++ X++` | `empty 1` | no `empty` (first press free, 1 of 2 used) |
| `X++:1, X8:1, =` | `X++ X8` | `empty 2` | `empty 1` (`X8`; `X++` is free) |
| `X1:1g, X8:1, =` | `X1 X8` | `empty 2` | `empty 1` (glass `X1` can't take the free press, so `X8` does and stays full; `X1` is empty) |
| `swap:1, X++:1, =` | `swap X++` | `empty 2` | `empty 1` (`swap` is free) |

Blind off: all rows as today.

## Out of scope

- `Fits`: still strict. A line may press a button once more than its charges allow thanks to the free press; it is rejected today and stays so (follow-up named in `blind-passive-rule-14`).
- `Spare`: it sums the starting charges of the distinct buttons pressed, so the free press doesn't change it. Unchanged.
- Rule 6 (`take_two`): the free press also skips the double charge. `Buttons.TakeTwo()` already turns charges into presses, so the same free-press rule applies to those counts; no extra work expected.

## Open questions

- Should `Spare` count the free press as one more spare charge? Default here: no.
