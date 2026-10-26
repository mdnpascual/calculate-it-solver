# Near-one passive: requirements

## Goal

The game has a passive skill, `passive_near1`, that also wins the round when the sent number misses the goal by 1. When the player owns it, a line that ends at G − 1 or G + 1 is as good as a line that ends at G. It may be cheaper, too. The solver doesn't know this yet, so it can miss the shortest line.

1. The mod detects whether `passive_near1` is active and publishes it in `puzzle.json`.
2. When it is active, the solver searches for G − 1, G and G + 1 and puts all those lines in one list.
3. The ranking stays the same (`ResultLine.Ranking`: cost, then drained, then spare, then kind). A line to G − 1 or G + 1 is not ranked below a line to G.

## Game facts (decompiled `Assembly-CSharp.dll`, 2026-10-25)

| What | Where | Detail |
|------|-------|--------|
| Win check | `StartLvl` (the per-frame check, before `notwork = true`) | The round is won when `can_send` is true and either `total == number`, or `Math.Abs(total - number) <= 1` and `GameObject.Find("passive_near1") != null`. `total` is the display, `number` the goal. |
| Rule 13 re-roll | `MainCalc.Count` | After a `Send` that misses, `renumber` re-rolls the goal, unless the miss is at most 1 and `passive_near1` exists. |
| How the passive exists | `PassivePool.CreatePassive` | Instantiates the passive prefab under `pas_parent` and renames it to the prefab name, so an owned near-one passive is an active GameObject named exactly `passive_near1`. The game's own test is `GameObject.Find("passive_near1") != null`. |
| `can_send` | `MainCalc` | Set to true when an `=` finishes (`Count`). Edits and operators set it to false. So the display must come from an `=`, as for an exact win. |

`passive_near1` doesn't change the goal (`number`) or the published `goal`. It only widens the win check.

## Inputs

- `puzzle.json` gets `"near1": true | false`. It is missing from an older mod, which counts as false.
- The app gets a **Near 1** checkbox. Follow game sets it from `near1`; the user can also tick it by hand for a typed puzzle.

## Rules

1. **Targets.** With Near 1 on, the targets are G − 1, G and G + 1. A target below 0 is dropped (G = 0 gives 0 and 1). In boss rule 12 (every `=` adds 1), a target below 1 is dropped, the same lower bound the `plusOne` phases already use.
2. **Every phase, every target.** Arithmetic, two-operator, division and modulo each run for all targets. The same filters apply as today (charges, required button, rule 13's one `=`, rule 6's two charges per press).
3. **One list, same ranking.** Lines from all targets are merged into the Lines box and sorted by `ResultLine.Ranking`, unchanged. A line to a target other than G shows `goal N` (the existing `Reaches` text), so the user can see where it ends.
4. **Caps are shared.** Today each phase is capped at the cheapest line so far + 2 (`CostSlack`). With Near 1 the "cheapest line so far" is over all targets, because all of them compete in one list.
5. **S within 1 of G.** If S = G − 1 or S = G + 1 (and not rule 12), the bare `=` sends S and wins. It is a 0-press line (`=` is free). Today S == G gives no line and no search (`searchSameNumber`); that stays for G itself.
6. **Boss rule 12 (`plusOne`).** Each target is searched with `plusOne`, so a line sends target t when `EngineeringCalculator.Sent(...) == t`. The existing S = t − 1 bare `=` line covers rule 5 there.
7. **Boss rule 14 (goal + 1 per charged press).** For each k, the targets are G + k − 1, G + k and G + k + 1, still with exactly k charged presses. The stop rule (10 lines or k = 12) counts lines over all three.
8. **Boss rule 13 (an `=` that misses re-rolls).** The one-`=` filter stays. With Near 1 a miss by 1 doesn't re-roll, but it also wins at once, so a line still has only one useful `=`.
9. **Status.** The done status starts `Near 1: goal ± 1:` when Near 1 is on (after any `Rule 12:` / `Rule 13:` prefix, like those prefixes stack today).
10. **Mod republish.** The passive can be picked while a puzzle is already published. A change of `near1` in the same round publishes the puzzle again, the way a warp does. In a rule-14 round the app accepts that same-round file when `near1` changed.

## Examples

- S = 41, G = 50, buttons `+:5, 9:5, 10:5`, Near 1 on. Without it, `+ 9 =` (2 presses) is the best line. With it, `+ 10 =` → 51 is also a 2-press line (`goal 51`); both show and the existing tie-breaks order them.
- S = 41, G = 48, buttons `X7:1, X8:1, ÷:1, 10:1`. Exact: `÷ 10 = X8` (3 presses). Near 1: `÷ 10 = X7` → 47 (3 presses, `goal 47`) also shows. Same cost; drained / spare decide the order.
- S = 49, G = 48, Near 1 on: the 0-press line `=` wins at once.
- G = 0, Near 1 on: targets 0 and 1 only.

## Out of scope

- Other passives (`passive_metronome`, `passive_min`, `passive_uses`, `passive_lucky`, …). Publishing a general `passives` list is a possible follow-up.
- Challenge 1's ±10 window (`IsWithinMoreLessWindow`, only with a pressed required button).
- A lucky mid-line `=` that lands within 1 of the goal (the solver also ignores the exact case today).
- Preferring the exact goal on a tie. The user asked for the same ranking.

## Open questions

1. The in-game name and description of `passive_near1` (for the checkbox label and Test U). Not needed for the code.
2. When the perk picker (every 5 rounds) runs relative to the next puzzle's publish. Rule 10 covers both orders; Test U checks it.
