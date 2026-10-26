# UI cleanup: requirements

**Status:** Done (2026-10-26).

Small changes to the solver app's window (`CalculatorPuzzle/MainForm.cs`), to how a line is printed in the Lines box (`ResultLine.ToString`, `ButtonLabels.Line`), and to the status messages. No solver change: the same lines are found, in the same order.

## Changes

### Window

1. **Caption S.** `Starting number (S)` becomes `Start (S)`.
2. **Caption G.** `Goal number (G)` becomes `Goal (G)`.
3. **Lines box width.** The Lines box fills the width the window already has: as wide as the widest other row (today the Buttons box, 520 px, or the Required / Last / Money / Boss row, whichever is wider). It must not make the window wider by itself.

### Lines

4. **Press separator.** Presses are separated by ` | ` (space, bar, space) instead of one space.
   - Before: `8X / 10 = reverse   (4 presses, spare 30)`
   - After: `8X | / | 10 | = | reverse   (4 press, spare 30)`
   - Charge keys are separate presses: `volt12 | volt11 | / | =`.
5. **Shorter bracket words.** `presses` → `press` (always, also for 1 and 0), `empties` → `empty`. `spare`, `goal N` and `no <button>` stay.
   - Before: `(5 presses, goal 49, spare 12, empties 1, no X8)`
   - After: `(5 press, goal 49, spare 12, empty 1, no X8)`
6. **No credit tag.** The Just a one `[digit]` / `[÷]` / … tag after the bracket is no longer printed. Just a one keeps its order (cheapest line of each credit first); only the printing goes.

### Empty or bad boxes become 0

7. **Boss.** An empty or bad Boss box means no boss rule (0). The message `Enter a boss rule number, or leave Boss empty.` goes.
8. **Last.** An empty or bad Last box means 0. The messages `Enter a non-negative whole number for Last, …` and the note `no Last value, last ignored` go. (Follow game fills Last from `puzzle.json`; an empty box only happens after a hand edit.)
9. **Money.** An empty or bad Money box means 0. The messages `Enter a non-negative whole number for Money, …`, and the notes `money $M` and `money needs a Money value` go (the Money box already shows the amount).

### Status messages

10. **Button names in Buttons errors.** Use the in-game names (see `spec/button-labels/`):

    | Before | After |
    |--------|-------|
    | `mw key "x" needs a number from 0 to 999999 with no leading zero` | `mem key "x" needs a number from 0 to 999999 with no leading zero` |
    | `Only one mw button (mD) is allowed` | `Only one mem button (memD) is allowed` |
    | `Charge key "x" needs a value of at least 1` | `Volt key "x" needs a value of at least 1` |
    | `Charge key cN can't have more than N presses` | `Volt key voltN can't have more than N presses` |
    | `Quest key needs its digit (write qD:n)` | `Unknown key needs its digit (write unkD:n)` |

    `"x"` is still the text as typed. `Missing charge count in …` and `Bad charge count in …` keep "charge": it means the `:n` count, not the volt key.
11. **Notes removed:** `mw empty` (plus the Last and Money notes in 8 and 9).
12. **Prefixes removed:** `Rule 14: goal G + k, k = 1..K: `, `Rule 13: one = per line: `, `Rule 12: every = adds 1: `, `Near 1: goal ± 1: `. The done status is the parts list alone.
13. **One "no line" message.** Every "no line" case shows `No line fits the available buttons.`, or `No line fits the available buttons within the charges.` when some lines were rejected. These go: `… with 2 charges per press.` (rule 6), `No line sends G with every = adding 1.` (rule 12), `No line reaches G with one = at the end.` (rule 13), `No line fits the available buttons for goal G + k, k = 1..K.` (rule 14), and the `±1 of G` wording (Near 1).
14. **Search errors removed**, except an unexpected exception's message. These go: `The Division Shortcut needs ÷ and 10 or a digit key.`, `S and G must be at least 0, and the press limit at least 1.`, and a Modulo Shortcut error in the done parts. When a search can't run, the status is the done status or the "no line" message, as for a search that found nothing.
15. **Achievement mode done messages removed:** `School mathematics: N line(s)`, `No line reaches G with + - × ÷ in 12 operators or fewer.`, `School mathematics needs 10 or a digit key.`, `Just a one: S → S+1, credits: …`, `No line makes S+1 with one = this round.`, `Just a one can't go above 999 999.` A finished achievement search shows `N line(s)` plus the parts below (`N rejected`, notes), or the "no line" message. The running message (`School mathematics: searching… N line(s) so far. Esc to stop.`) stays.
16. **Kept:** the `TEST M2: boss required, report back` marker (boss rule 11).

With 6 and 15 the Just a one credits are no longer shown anywhere. The line order still groups them.

## Reference: what a line prints after this spec

```
<press> | <press> | …   (N press[, goal G], spare N[, empty N][, no <button>])
```

| Word | When it shows |
|------|---------------|
| `N press` | always: press count (`=` is free and isn't counted) |
| `goal N` | the line ends at a number other than G: boss rule 14 (G + k), Near 1 (G − 1 or G + 1), a `swap` line (ends at S) |
| `spare N` | always: charges left on the distinct buttons the line uses |
| `empty N` | the line leaves N buttons at 0 charges |
| `no <button>` | a Required button is set (Market Demand) and the line doesn't press it |

## Reference: status messages after this spec

### Idle and input errors

- `Press Enter to recalculate.`
- `Enter non-negative whole numbers for S and G.`
- `Can't watch the game folder: <system message>`
- Buttons box errors:
  - `Bad charge count in "x"`
  - `Missing charge count in "x" (write x:n)`
  - `Unknown button "x"`
  - `mem key "x" needs a number from 0 to 999999 with no leading zero`
  - `Only one mem button (memD) is allowed`
  - `Volt key "x" needs a value of at least 1`
  - `Volt key voltN can't have more than N presses`
  - `Unknown key needs its digit (write unkD:n)`

### While searching

- `Searching… N line(s) so far. Esc to stop.`
- `Modulo Shortcut… N line(s). Esc to stop.`
- `School mathematics: searching… N line(s) so far. Esc to stop.` (the same with `Just a one:`)
- `Stopped. N line(s) found.` (Esc)

### Done, lines found

Parts joined by `, `. Zero counts are left out.

- `N line(s)` (achievement modes only, first)
- `N arithmetic line(s)`
- `N two-operator line(s)`
- `N division line(s)`
- `N modulo line(s)`
- `N swap line(s)` (when the puzzle has `swap`, even 0)
- `N rejected`
- `K use <button>`
- Notes: `no line can use <typed>`, `<button> isn't in Buttons`, `warp N`, `blocked: <button>, <button>`, `achievement mode ignores boss rule N`, `achievement mode ignores Required`
- `TEST M2: boss required, report back` (boss rule 11)

When S = G and nothing else applies, the done status can be empty.

### Done, no line

- `No line fits the available buttons.`
- `No line fits the available buttons within the charges.`

### Errors

- An unexpected exception's message.

## Status style (decided: no change)

The user keeps the current styles as they are (2026-10-26). The notes below are kept for reference.

The messages use three styles today:

| Style | Examples | End |
|-------|----------|-----|
| Sentence | `Press Enter to recalculate.`, `No line fits the available buttons.`, `Stopped. N line(s) found.` | `.` |
| Running | `Searching… N line(s) so far. Esc to stop.`, `Modulo Shortcut… N line(s). Esc to stop.` | `…` in the middle, `.` at the end |
| List | `3 arithmetic line(s), 2 division line(s), 1 rejected, warp 2` | nothing |

Other differences:

- **Errors with or without `.`:** the input error `Enter non-negative whole numbers for S and G.` ends with `.`; the Buttons errors (`Unknown button "x"`) don't.
- **Capitals:** sentences start with a capital; the done list and its notes are lower case (`warp 2`, `blocked: X8`).
- **`line(s)`:** used everywhere, also for 1 (`1 line(s)`); the done list says `1 rejected` with no noun.
- **Running wording:** the main search says `so far`, the Modulo phase doesn't (`Modulo Shortcut… 5 line(s).`), and the Modulo one names its phase while the others don't.

A suggestion to unify them (`.` on every sentence and error, `line` / `lines` by count, `Searching modulo… N lines so far.`) was declined: no change.

## Out of scope

- Any solver, ranking or search change.
- The mod overlays (`GameHook.MelonLoader`).
- Other captions (Required, Last, Money, Boss, Buttons, Lines, Game).
- `Buttons.Parse` keeps `LastIgnored` / `MoneyIgnored` for callers that pass no value; the app just always passes one.
