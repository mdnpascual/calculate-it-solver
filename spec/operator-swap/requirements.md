# Operator swap: requirements

## Goal

The game has a button `operator_swap` (button id 23) that trades the display and the goal: the display becomes the goal, and the goal becomes what the display showed. When the puzzle has it, the line "press swap, then solve from G to S" can be shorter than any line from S to G.

1. Map `operator_swap` to a solver token, `swap`.
2. When `swap` has a charge, solve twice: once for S → G (as today), then once with the two numbers flipped, G → S. Every flipped line starts with `swap`, which costs 1 press.
3. `swap` appears at most once on a line, and only as the first press.
4. Both solves go into one Lines box with the same ranking.

## Game facts (decompiled `Assembly-CSharp.dll` with ilspycmd 9.1, and a UnityPy read of `level1`, 2026-10-25)

| What | Where | Detail |
|------|-------|--------|
| Click order | `operator_swap`'s `Button.m_OnClick` in `level1` | `CountUses.SubUse`, then `MainCalc.Swap`, then two sounds. So the charge is spent (and rule 14 raises the goal) **before** the swap. |
| The swap | `MainCalc.Swap` | `can_send = false`; `total` (display) ↔ `StartLvl.number` (goal), the goal stored as `(int)`; both texts updated; then `Send()`. |
| It sends | `MainCalc.Send` | With no operator pending (`currentoperand == 0`) no arithmetic runs. The rest of `Send` does run: `passive_metronome`, boss rule 12 (`total++`), `AddTurn`, coin passives, then `Count`, which runs rule 13's re-roll check and sets `can_send = true`. |
| Left side only | `MainCalc` operator presses, `AddNumber` | An operator press blocks every `operator_tag` button. A digit key unblocks them all **except** `operator_swap`. So swap can't be pressed on the right side. |
| Usable again | end of `Send` / `Count` | `Send` blocks swap while it runs; `Count` unblocks it at the end. So in the game swap works on any left side, including after an `=`, as often as it has charges. |
| Charges | `CountUses.SubUse` | Like any button: 1 charge (2 in rule 6), glass breaks after one press, rule 10 costs a coin. |
| Win check | `StartLvl` (`can_send && total == number`) | After a swap the display is the old goal and the goal is the old display, so the swap alone wins only when they are equal (not a real puzzle). |

## Inputs

- `puzzle.json`: no change. The mod already lists every button by id; `GameLink.MapButton` maps `operator_swap` to `swap`.
- Buttons box: `swap:n` (and `swap:1g` for glass), like any button.

## Rules

1. **When.** The flipped solve runs only when `swap` has at least 1 charge and S ≠ G.
2. **Direct solve.** S → G with every phase, as today. It never presses `swap`.
3. **Flipped solve.** Start F, goal S, every phase, with the same buttons. F is G, plus 1 in boss rule 12 (the swap's `Send` adds 1), plus 1 in boss rule 14 when `swap` isn't glass (`SubUse` raises the goal before the swap). Each flipped line is `swap` followed by a line found for F → S. Its cost is that line's cost + 1.
4. **At most once.** A flipped line has exactly one `swap`, as its first press. The direct line has none. The phases never generate `swap`, so this holds by construction; the simulator check enforces it.
5. **Charges.** A flipped line is checked against the puzzle's charges with the `swap` press included (`Buttons.Fits` on the whole line). Drained and spare are counted on the whole line too, so a 1-charge `swap` counts as drained.
6. **One list, same ranking.** Lines from both solves go into the Lines box, sorted by `ResultLine.Ranking` with the costs above. A flipped line shows `goal S` (the number it ends at, the existing `Reaches` text), so its end differs visibly from G.
7. **Shared caps.** The phase caps (cheapest line so far + 2, `CostSlack`) count lines from both solves, with the flipped cost including `swap`. The flipped phases search with the cap minus 1.
8. **Swap alone.** If F = S (rule 12 or 14 with S = G + 1), the 1-press line `swap` wins: the display becomes S and so does the goal.
9. **Boss rule 13 (an `=` that misses re-rolls).** Swap is a send that misses (display G, goal S), so the goal re-rolls. No flipped solve in rule 13.
10. **Boss rule 11 (required button).** Required `swap` in a boss round: only flipped lines count, so the direct solve is skipped. Market Demand with `swap`: both solves run; direct lines show `no swap`. Any other required button: both solves use the usual filter (the button must be in the line after `swap`).
11. **Boss rule 12.** The flipped solve runs with `plusOne`, from F = G + 1, aiming at S.
12. **Boss rule 14.** The flipped solve is a `GoalStepSearch` from F to S: a flipped line with k charged presses after `swap` ends at S + k. `swap`'s own charge is already in F.
13. **Boss rule 6.** Nothing new: `Buttons.TakeTwo()` already turns charges into presses for every button.
14. **Status.** The done status adds `M swap line(s)` when the flipped solve ran.

## Examples

- S = 4, G = 48, buttons `swap:1, ÷:1, 10:1` (no `dX` / `Xd`). Direct: no line (4 can't grow). Flipped: `swap` (display 48, goal 4), `÷ 10 =` → 4 = goal. Line `swap ÷ 10 =`, 3 presses, `goal 4`.
- S = 12, G = 1, buttons `swap:1, X2:1`. Direct: no line. Flipped: `swap` (display 1, goal 12), `X2` → 12, then the free `=` sends it. Line `swap X2`, 2 presses (an edits-only line after the swap).
- S = 41, G = 48, buttons `+:1, 7:1, swap:1`: direct `+ 7 =` (2 presses). Flipped: 48 → 41 needs `-`, which isn't there, so no flipped line. The list shows only the direct line.
- Boss rule 12, S = 49, G = 48, `swap:1`: F = 49 = S, so `swap` alone wins (1 press).

## Out of scope (follow-ups)

- **Swap mid-line.** The game allows swap on any left side, including after an `=` (`S edits X, swap` makes the goal X). A search that places `swap` after other presses needs the goal inside the search state. Not now: the user asked for swap first only.
- More than one swap per line (the user ruled it out).
- `passive_metronome` (unsupported everywhere) and `passive_near1` (its own spec, [`near-one-passive/`](../near-one-passive/); when both exist, the flipped targets are S − 1, S, S + 1).
- Challenge 3 (fail after 4 turns): swap's `Send` counts as a turn (`AddTurn`). The solver doesn't model turns.

## Open questions

1. After the player presses `swap`, the goal changes, so the mod sees a new (round, goal) and publishes F → S (not in rule 14, where it publishes once per round). The app then solves that puzzle; the rest of the line should be in the new list. Test W checks this; no code change is planned for it.
2. The in-game label and description of the button (for `docs/button-id.md`). Not needed for the code.
