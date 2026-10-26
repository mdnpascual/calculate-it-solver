# Requirements: Game state scan

**Status:** Complete (2026-10-22). The read-only MelonLoader mod (`GameHook.MelonLoader/`) publishes each settled puzzle to `puzzle.json`, and the solver follows it. Added after the first draft: a **Lock** checkbox (see the design's "As built").

## Goal

When a new puzzle appears in Calculate It, the solver app fills in S, G and the buttons (with their charges) by itself and starts the Division Shortcut search. It does this **once per puzzle**, after the game has settled, and it never shows a line the remaining charges can't pay for.

## Background (what the test showed)

MelonLoader 0.7.3 on the game (Unity 2022.3.32f1, Mono) loads the mod. It reads these values correctly (checked against a played puzzle, 4 → 24 with `X1 ÷ 10 = X2 flip`):

| Value | Game field (from REA static analysis) |
|-------|---------------------------------------|
| Goal | `StartLvl.number` (int) |
| Left side | `MainCalc.total` (long), shown by `MainCalc.output` |
| Pending operand / operator | `MainCalc.operand` (999999999 = none), `MainCalc.currentoperand` (4 = ÷) |
| Round | `StartLvl.round` (int) |
| Buttons | active `CountUses` components under `StartLvl.calc_transform` |
| Charges | `CountUses.uses` (int) |
| Button id | `CountUses.buttonKey`, or the object name when that's empty, minus `(Clone)` |

Two findings drive the rules below:

- **Continue a saved run:** the game first generates a fresh round (seen: 98 → 63), then loads the saved one (4 → 24). Only the second one is real.
- **Every press and purchase changes the state** (left side, charges, new shop buttons). Those must not restart the solver.

BepInEx was never tested; we went with MelonLoader only.

## Rules

### When a puzzle counts as new

1. A puzzle is identified by **(round, goal)**. A new round, or a new goal in the same round (for example a reroll), is a new puzzle.
2. A new puzzle is **published** only when the game has settled:
   - `RunSave.Restoring` is false,
   - `StartLvl.IsTransitioning` is false,
   - the round, goal, left side and button list (ids and charges) haven't changed for **50 ms** (configurable),
   - left side ≠ goal (a solved puzzle or the shop phase isn't a puzzle).
3. Each puzzle is published **at most once**. Presses, charge changes and purchases inside the same (round, goal) don't publish again.
4. If a second puzzle replaces the first before the app finishes (for example the Continue case if the flags don't catch it), the newer one wins: the app cancels the running search and starts over.

### What a published puzzle contains

- `start`: the left side at publish time (S)
- `goal` (G)
- `round`
- every active button: its id, its on-screen label and its charges
- the calculator object name and scene name, for diagnostics (no calculator-type field was found)

### Mapping game buttons to solver buttons

| Game id | Solver token |
|---------|--------------|
| `operator_dx` (for example `operator_5x`) | `dX` (`5X`) |
| `operator_xd` (for example `operator_x1`) | `Xd` (`X1`) |
| `number10` | `10` |
| `numberN` (N = 0–9) | digit key `N` |
| `operator/` | `÷` |
| `operator_mod` | `mod` |
| `operator_reverse` | `flip` |
| `operator_to0` | `reset` |

Any other id (for example `operator_prime`) is **ignored** and listed in the status line so it's visible.

### Charges

1. Each button's charges are the number of presses left. `=` has no charges and is free, as now.
2. A button with **0 charges** counts as unavailable.
3. A solution line is **rejected** if it presses any button more times than that button's charges. Rejected lines are never shown. The status says how many were rejected.
4. Buttons typed by hand without a charge count stay unlimited, so the manual workflow keeps working.

### App behaviour

1. A **Follow game** checkbox (on by default) turns the feed on or off.
2. When a new puzzle is published, the app:
   - fills in the S, G and Buttons boxes (with charges) as if the user had typed them,
   - resets the Modulo Shortcut iterator A to 1 (k already resets to S's digit count when S changes), so a previous puzzle's A doesn't carry over,
   - starts the Division Shortcut search as if Enter had been pressed (the Modulo Shortcut rows update as usual),
   - shows `Game: round R, S → G` plus any ignored buttons in the status.
3. Typing in a box doesn't stop following. The next new puzzle overwrites the boxes.

## Examples

| Puzzle | Charges | Expected |
|--------|---------|----------|
| 41 → 48 | `X8:1, 10:1, ÷:1` | top line `÷ 10 = X8` (3 presses) |
| 41 → 48 | `X8:0` | no line contains `X8` |
| 4 → 24 | the charges from the log (`X1:1, X2:1, flip:7, ÷:6, 10:13`, …) | every line shown uses each button at most its charges |
| any | a button at 1 charge that the cheapest line uses twice | that line is rejected |

Note: the Division Shortcut only produces lines that contain `÷`. The cheapest answer for 4 → 24 is `X2 flip` (two left-side edits, no division), which the app doesn't offer today. See "Out of scope".

## Out of scope

- Pressing buttons, changing charges, or writing anything into the game or its save. The mod stays strictly read-only, and the game has Steam leaderboards.
- Challenge rules that change press costs (for example "each press consumes 2 charges"). Those are a later spec.
- Charge-aware **Modulo Shortcut** lines. These need press sequences first; see `spec/cost-per-line/`.
- Re-solving from the current state after each press (option B in the discussion). It may be added later as a setting.
- A general search that routes around scarce buttons (see the design's known limitation).
- Edit-only lines (S edited straight into G with `dX`, `Xd` and `flip`, no operator, for example 4 → 24 = `X2 flip`). `LeftEdits` can already find these, so this is a small follow-up spec, but it's a new kind of line and not part of this feed.

## Open questions

- Does `RunSave.Restoring` cover the whole Continue window, or is the fresh round visible for longer than 50 ms first? The test plan checks this.
- Which operator codes do other operators use (only `4` = ÷ is confirmed)? This is diagnostic only.
- Button ids on calculators other than Engineering (Normal, Steel, Old, …). Unknown ids are ignored for now.
