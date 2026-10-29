# Requirements: blind passive in boss rule 14 (bugfix)

**Status:** Complete 2026-10-28. Test AB done in game.

## Why

With the `passive_blind` passive, the first charged press of each round is free. In boss rule 14 a free press also doesn't raise the goal. The solver doesn't know this, so in a rule-14 round with the passive it looks for G + k when the game wants G + k − 1. Every line it shows misses the goal by 1.

## Game facts (ilspycmd 9.1 on `Assembly-CSharp.dll`, 2026-10-28)

| What | Where | Detail |
|------|-------|--------|
| Flag | `GamePrefs` int `blind` | 1 = the next normal press is free. Saved with the run (`RunSave.PreferenceKeys`). |
| Set to 1 | `PassiveProp.OnEnable` (`passive_blind` picked, not during a restore); `StartLvl.GoAwayRoutine` (end of every round, if `GameObject.Find("passive_blind") != null`, else 0) | So each round starts with `blind` = 1 while the passive is owned. |
| Set to 0 | `StartLvl` run start; `CountUses.SubUse` (the free press) | |
| The free press | `CountUses.SubUse`, `if (!glassed) { if (blind == 0) { … } else blind = 0; }` | The first **non-glass** charged press skips the whole charge branch: no `uses − 1`, no `inf_blocked`, no rule-14 `number++`, no rule-10 coin, no rule-6 double charge. |
| Not affected | same method, before that branch | The `diff_type` ≥ 7 drain (`press_count` ≥ 3) and rule 11's `must_butted` still happen. A glass press doesn't use the free press (the glass branch doesn't read `blind`). `=` isn't a `CountUses` button. |

So in rule 14 with the flag set, a line with **k charged presses** (every press except `=` and glass presses, the same count as `GoalStepSearch.Charged`) ends at **G + k − 1**, for k ≥ 1.

## What the solver must do

1. **Know the flag.** The mod publishes `blind` (true when `GamePrefs.GetInt("blind") == 1`) in `puzzle.json` and `state.json`. Rule-14 rounds publish once per round, at the start, so the flag is still 1 then.
2. **App.** A **Blind** checkbox next to Near 1. Follow game ticks it from `blind`, and it can be set by hand. Changing it marks the search stale like Near 1 does.
3. **Rule 14 with Blind.** `GoalStepSearch` targets G + k − 1 instead of G + k, and still keeps only lines with exactly k charged presses. With Near 1 the targets are G + k − 1 ± 1.
4. **Goal-reading buttons.** `near` and `median` read the goal shown at their press. In the simulator the first non-glass press doesn't raise it. Example: `near` as the first press reads G, not G + 1.
5. **Swap.** `swap` is a non-glass first press, so with Blind it is the free one: F = G (rule 12 still adds 1), and the flipped leg runs without Blind. A glass `swap` leaves the free press for the flipped line's first non-glass press.
6. **No new text.** No status part, no line tag. The line's existing `goal N` already shows the goal it reaches (now G + k − 1). The Blind checkbox is the only UI change.

## Examples

S = 41, rule 14, Blind on:

| G | Buttons | Line | Charged | Reaches |
|---|---------|------|---------|---------|
| 46 | `÷:9, 10:9, X8:9, =` | `÷ 10 = X8` | 3 | 48 = 46 + 3 − 1 |
| 47 | `÷:9, 10:9, X8:1g, =` | `÷ 10 = X8` | 2 (glass `X8`) | 48 = 47 + 2 − 1 |
| 45 | `÷:9, 10:9, X8:9, =` | `÷ 10 = X8` is **not** kept (it reaches 47 with Blind) | 3 | — |

S = 47, G = 48, rule 14, Blind on, Buttons `X++:1, =`: the line `X++` (1 charged press) reaches 48 = 48 + 1 − 1. With Blind, a one-press line in rule 14 can end at G itself. Blind off: no line (it would need 49).

Blind off: unchanged from today (G + k).

## Out of scope (follow-ups)

- The free press outside rule 14: the first non-glass press doesn't use a charge, so a line may press one button once more than its charges allow. Today such a line is rejected (`R rejected`). The solver stays safe, it only misses lines. Same for rule 6 (`take_two`) and the rule-10 coin, both skipped on the free press.
- Re-solving mid-round after the free press was spent (type S and G and untick Blind by hand).

## Open questions

- None from the code. Test AB checks it in game.
