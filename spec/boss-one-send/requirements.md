# Requirements: Boss rule 13, one `=` per line

**Status:** Implemented 2026-10-25 (607 tests). Test R (in-game) R1–R4 confirmed.

## Why

Boss rule 13 (see [`docs/boss-rules.md`](../../docs/boss-rules.md)) shows *"After clicking the "Send" button, the required number changes"*. Today the app runs a normal search. Many of its lines press `=` in the middle (`÷ 10 = X8`, `- 2 0 = + 1 =`). In this round that `=` misses the goal, the goal is re-rolled, and the rest of the line aims at a goal that is gone.

This is a quick help: drop those lines. It doesn't search for anything new.

## Game facts (static analysis, `Assembly-CSharp.dll`, 2026-10-25)

| What | Where | Detail |
|------|-------|--------|
| Rule active | `StartLvl.NextLvl` | `renumber = true` in a rule-13 boss round. Never in challenge 10. |
| "Send" is `=` | `MainCalc.Send` | Every `=` (with or without an operator) runs `Send`, which ends with the score animation `MainCalc.Count`. |
| Re-roll | `MainCalc.Count` | When the animation ends: if the display isn't the goal and `renumber` is on, `StartLvl.Reroll()` picks a new goal. With `passive_near1`, a miss by 1 doesn't re-roll. |
| Win check | `StartLvl.Update` | The answer counts only when the display equals the goal **and** `can_send` is true, which only `Count` sets. So a line that ends with left edits is sent by one more `=` with no operator (the "send `=`", not listed in the line). |
| Display after a re-roll | `StartLvl.Reroll` | Only the goal changes; the display keeps its value. The mod publishes the new goal (same round, new goal: see `game-state-scan/`), so Follow game re-solves from the current display. |

## The rule

A line is kept when the only `=` it presses, counting the send `=`, is the one that sends the answer:

- `edits` (then the send `=`): kept. No `=` in the listed presses.
- `edits op D =`: kept. The listed `=` is the last press.
- `edits a->c k edits` / `b->c`: kept. Remorph keys don't press `=`.
- `edits op D = edits`: dropped. The middle `=` misses.
- `op1 D1 = op2 D2 =`: dropped. Every two-operator line has a middle `=`.

So the test on the listed presses is: **no `=` except as the last press**.

Examples (S = 41):

| G | Buttons | A normal line | Rule 13 |
|---|---------|-----------------|---------|
| 48 | `÷:9, 10:9, X8:9` | `÷ 10 = X8` | no line |
| 4 | `÷:9, 10:9` | `÷ 10 =` | `÷ 10 =` |
| 418 | `X8:9, ÷:9, 10:9` | `X8` | `X8` |
| 48 | `2X:9, 3X:9, mod:9, 10:9, 3:9` | `2X 3X mod 10 3 =` | same |
| 438 | `+:9, 1:9, X8:9` | `+ 1 = X8` | no line (unless another one) |

S = 59, G = 40, `-:9, +:9`, keys `0`–`9`, no `dX` / `Xd`: `- 2 0 = + 1 =` is dropped; `- 1 9 =` is kept.

## What the solver must do

1. **Know the rule.** The Boss box (filled by Follow game from `bossRule`) says 13.
2. **Filter.** Every phase keeps only lines with no `=` before the last press. The 100-line limits and the cost caps count only kept lines (the same way as a boss rule 11 required button).
3. **Skip the two-operator phase.** None of its lines can be kept.
4. **Together with a required button.** Rule 13 and rule 11 never meet (one rule per round), but Market Demand (challenge 8) can: both filters apply (Market Demand still keeps lines without the button, ranked last).
5. **Status.** Done status starts with `Rule 13: one = per line: `, then the usual counts. No line: `No line reaches {G} with one = at the end.` (with the real G).
6. **Lines** look as today.

## Out of scope

- A middle `=` whose result happens to be the goal (the round would end there). Ignored: such lines are dropped.
- `passive_near1` (a miss by 1 doesn't re-roll). Ignored: the filter is the same with or without it.
- Lines built for the re-roll (pressing `=` on purpose to get a new goal). Not modelled.
- Rule 12 and rule 14 together (never: one rule per round).

## Open questions (Test R)

- R2: does a line that ends with `op D =` win on that `=`?
- R3: does a line of edits only win on the send `=` with no re-roll?
- R4: does a middle `=` re-roll the goal, and does Follow game then re-solve from the current display?
