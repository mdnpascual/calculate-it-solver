# Requirements: Two-operator lines

**Status:** Complete (2026-10-24). Follow-up: after Test M2 passes, remove `MainForm.BossTestMarker`.

## Goal

Every line the solver builds today has at most one operator (`ArithmeticSearch`, `DivisionShortcut`, `ModuloSearch`). Some puzzles need two:

```
S --edits--> X1   op1 D1 =   M   op2 D2 =   Y2 --edits--> G
```

Add a **two-operator phase** that finds these lines in any round. It matters most in boss rule 11 when the required operator points the wrong way, but it is a general phase, not a boss special case.

## The puzzle that showed it

`puzzle.json` from 2026-10-24 (challenge 4, round 2, boss rule 11, `required` `operator+`, `requiredKind` `boss`):

- Start `59`, goal `40`.
- Buttons: `1:1`, `2:2`, `3:2`, `4:2`, `5:2`, `6:2`, `7:2`, `8:3`, `9:2`, `0:2`, `+:3`, `-:2`, `×:2`, `÷:2`, `round:1g`, `0->8:2` (`operator_energy` is unmapped).
- No `dX` / `Xd`, so the left side can't be made smaller than 59. A `+` line with one operator always ends at 59 or more, and `round` / `0->8` can't bring that down to 40.
- The app showed nothing. The user solved it with `- 2 0 = + 1 =` (59 → 39 → 40, 5 presses).
- Without the boss rule the one-operator line `- 1 9 =` (3 presses) already works.

## Rules

1. **Shape.** A two-operator line is: left edits on S (may be none), one operator segment, an optional middle edit (v1: none, see Out of scope), a second operator segment, left edits into G (may be none). An operator segment is one of:
   - `op D =` with `op` in `+`, `-`, `×`, `÷`, `mod`, `%` and `D` typed with operand keys (`10`, `0`–`9`, `c`, `q`);
   - `a->c k` or `b->c k` (one operand key, no `=`).
2. **Every operator the puzzle has** can be either segment, including the same operator twice (`+ 1 = + 1 =`, if the charges allow).
3. **Checked like every other line:** replayed in `EngineeringCalculator.Run` (the last display must be G) and `Buttons.Fits` (charges over the whole line). Lines that fail Fits count as `rejected`.
4. **Cost and ranking:** cost is the press count as today (`=` free). Two-operator lines join the Lines box with the same ranking (cost, drained, spare). Kind order on a tie: arithmetic, then two-operator, then division, then modulo.
5. **When it runs.** After the arithmetic phase, before division. Cap = cheapest kept arithmetic line + 2 (`CostSlack`), or 12 (`MaxPresses`) when there is none. The division cap then uses the cheapest kept line of arithmetic **and** two-operator lines. Two-operator lines cost at least 4 presses, so with a 1- or 2-press arithmetic line the phase has nothing to search and returns at once.
6. **Required button (Market Demand and boss rule 11).** Two-operator lines go through the same `accept` filter and `Missing` mark as other lines. A line uses the required button if **any** press is that button; pressing it twice is allowed (it just costs two charges).
7. **Boss rule 11 phases.** A required operator no longer means "one phase only": every operator can now appear in a two-operator line. `RequiredPlan` adds the two-operator phase for every required operator, and in that case the phase only builds pairs where `op1` or `op2` is the required operator. Left-side required buttons: the phase runs and lines are filtered, as for the other phases.
8. **Boss rule 14.** `GoalStepSearch` runs the two-operator phase for each target G + k, with the same `Charged == k` filter.
9. **Limits** (so the phase stays quick): at most 3 operand presses per segment, at most 200 000 middle values `M`, at most 100 lines kept (counting only accepted lines). Results under 0 or at 10^18 or more are dropped (the simulator already fails them). (Corrected 2026-10-25: the game clamps `+` and `×` at 999 999; see [`display-cap/`](../display-cap/).)
10. **Cancel:** Esc stops it like the other phases (`OperationCanceledException`).
11. **Status:** the done status gets `T two-operator line(s)` after the arithmetic count.

### Test M marker (temporary)

12. (Removed after Test M.) Until Test M was confirmed, every two-operator line in the Lines box ends with the text `, TEST M: report back` inside its brackets, for example:

    ```
    - 2 0 = + 1 =   (5 presses, spare …, empties 1, TEST M: report back)
    ```

    It shows in every round (normal, boss rule 11, boss rule 14), so the user doesn't have to wait for a particular boss rule: any line with the marker is a Test M case. The marker is one constant (`ResultLine.TwoOperatorMarker`), so removing it later is a one-line change. After Test M is confirmed, remove it (follow-up in this spec's status).

## Examples

| Puzzle | Expected |
|--------|----------|
| The 59 → 40 puzzle above, boss rule 11, required `+` | Lines of 5 presses, all of them pressing `+`, including `- 2 0 = + 1 =` and `÷ 2 = + 9 = round` (29 + 9 = 38, round → 40). Each has the Test M marker. |
| Same puzzle, no required button | `- 1 9 =` (3 presses, arithmetic) first. The two-operator cap is 5, so 5-press two-operator lines (with the marker) can rank below it. |
| 41 → 48, `+:2, -:2, 9:2, 2:2`, required `-` (boss) | No one-operator `-` line (41 − D < 48, no left edits). Two 4-press lines: `+ 9 = - 2 =` (41 → 50 → 48) and `- 2 = + 9 =` (41 → 39 → 48). |
| 41 → 48, `+:2, -:2, 9:2, 2:2`, no required button | `+ 7` isn't typeable, so no one-operator line; the same two 4-press lines show. |

## Out of scope (follow-ups)

- **Middle edits** (left edits between the first `=` and `op2`, for example `÷ 1 0 = X8 + 1 =`). v1 joins on the raw middle value `M`.
- **Three or more operators.**
- **Division-Shortcut-style or Modulo-Shortcut-style segments** (prefix built from a formula). Segments here type their operand directly (at most 3 presses).
- Required-aware left search (from `boss-required-button/`).
- Keeping more than the cheapest route per `M` when charges clash between the two segments (v1 keeps one forward route per `M`, so a few lines can be lost to Fits).

## Open questions

1. Does the game accept `+ 0 =` / `- 0 =` as a press? The phase skips operands that change nothing (`+ 0`, `- 0`, `× 1`, `÷ 1`), like `ArithmeticSearch`, so it doesn't matter for v1.
