# Requirements: Glass buttons

**Status:** Complete (2026-10-23). Tested in-game; legendary not confirmed (the solver doesn't use it).

## Goal

Some buttons in Calculate It are **glass**. A glass button breaks after one press, whatever charge count it has. Today the mod reports only id, label and charges, so the solver thinks a glass `X1` with 4 charges can be pressed 4 times and can show lines the game won't let you finish.

The mod must report each button's glass state. The solver must treat a glass button as having at most **1** charge.

## Background (from REA / ilspycmd on `Assembly-CSharp.dll`)

| What | Where in the game |
|------|-------------------|
| Glass flag | `CountUses.glassed` (public bool) on the button |
| Glass is applied | `StartLvl` (when a bought button is placed): adds `glass_base(Clone)` as a child, makes the button image transparent, sets `glassed = true`. `StartLvl.CanBecomeGlass` decides which buttons may become glass. |
| One press | `CountUses.SubUse`: when `glassed`, `uses` is **not** lowered. Instead the button is disabled, the glass break animation plays, and `Break()` hides the button after 0.5 s. |
| Charges | Glass buttons can't get more charges: the shop refuses to add uses to a glass button (`ChargeOwnedOffer`, `Buy`). |
| Restore | `CountUses.RestoreGlass` (used by `operator_resurrect`) sets `glassed = false` and the button becomes normal again. |
| Legendary | Not a field. A button is legendary when its slot (`Infos.id`) is 10, 11, 28, 46 or 47 (`operator_near`, `operator_cut`, `operator_equal`, `operator_complement`, `operator_mediana`, see `Descriptionframe.UpdateItemHints`). The in-game text says: "Legendary – cannot have more than 1 charge". The shop enforces that, so `uses` is already correct. |

So `uses` on a glass button is meaningless for the solver: it can be 4 on screen and still allow just one press.

## Rules

### Mod (stays read-only)

1. For every button it reports, the mod adds `glass`: the value of `CountUses.glassed` (`true` / `false`).
2. The mod also adds `legendary` (`true` when `Infos.id` is 10, 11, 28, 46 or 47). This is for diagnostics only. The solver doesn't use it, and none of those buttons is mapped by the solver yet.
3. The mod reports the **raw** charges in `uses`. It doesn't change `uses` for glass buttons. The glass rule belongs to the solver.
4. `puzzle.json` and `state.json` both get the new fields. The `[CalcHook]` log line marks glass buttons (for example `X1:4g`).
5. The settle fingerprint includes the glass flag, so a button changing glass state counts as a change.
6. The format stays `version: 1`. The fields are additive: an old app ignores them, and a new app reading an old file treats a missing `glass` as `false`.
7. Nothing new is written to the game. No new Harmony patches.

### Solver

1. When `GameLink.Parse` reads a button with `glass: true`, its charge becomes `min(uses, 1)`. A glass button with 0 uses stays at 0 (left out, as now).
2. The Buttons box gets the clamped charge (`X1:1`), so every search (arithmetic, left transforms, division, modulo) and `Fits` already respect it. No search code changes.
3. (Dropped 2026-10-23: the status line doesn't list glass buttons, to keep it short.) Was: the status line lists the glass buttons after the game info, like the ignored ones: `Game: round 5, 41 → 48. Glass: X1, ÷.` (solver tokens; unmapped glass buttons are already in the ignored list).
4. Hand-typed buttons have no glass state. The user types `:1` to get the same effect.

## Examples

| Button in `puzzle.json` | Buttons box |
|-------------------------|-------------|
| `{ "id": "operator_x1", "uses": 4, "glass": true }` | `X1:1` |
| `{ "id": "operator_x1", "uses": 4, "glass": false }` | `X1:4` |
| `{ "id": "operator_x1", "uses": 4 }` (old mod) | `X1:4` |
| `{ "id": "number10", "uses": 0, "glass": true }` | left out (`10:0`) |
| `{ "id": "operator/", "uses": 3, "glass": true }` | `÷:1` |

Puzzle 41 → 48 with `÷:3 (glass), 10:13, X8:2`: the line `÷ 10 = X8` is fine (one `÷`). A line that needs `÷` twice is rejected.

## Out of scope

- `legendary` in the solver (charges already respect the cap).
- `operator_resurrect`: it turns a glass button back into a normal one mid-puzzle. The puzzle is published once, so the solver keeps the glass limit. Re-solving after a resurrect is a follow-up (same as re-solving after any press).
- Other per-press rules found in `SubUse`, each a later spec:
  - `StartLvl.take_two`: each press costs 2 charges.
  - `GamePrefs "blind"`: the next normal press costs no charge.
  - boss rule 14: each press raises the goal by 1.
  - boss rule 10: each press costs a coin.
- The glass calculator itself (`glassForbiddenIds`): which buttons can exist there doesn't matter, the solver only sees what is on screen.

## Answered questions (user, 2026-10-23)

- **Glass button at 0 uses:** it can't be pressed, and it shouldn't happen in practice: a bought key starts at 2 uses, and glass uses never go down. `min(uses, 1)` keeps 0 as 0 for safety.
- **Glass operator (`÷`, `mod`, `+`, …):** the press works (the operation applies), then the button breaks. So a glass button is exactly "1 charge" for the solver, operators included.
