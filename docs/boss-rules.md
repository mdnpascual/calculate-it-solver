# Boss rules

Boss rules in Calculate It: the `bossRule` value (`StartLvl.rule`, published as `bossRule` in `state.json`), the text the game shows when the boss round starts, and what the rule does. This is the starting point for specs that add solver support for boss rounds.

Source: static analysis of `Assembly-CSharp.dll` (ilspycmd 9.1 decompile, 2026-10-24). Methods: `StartLvl.NextLvl` (rule setup and text), `StartLvl.SelectBossRule`, `StartLvl.IsBossRound`, `CountUses` press handler, `MainCalc.Send`, `MainCalc.Count`, `MainCalc.Cancel`. The Solver status column says which rules are complete.

## When a boss round happens

- `IsBossRound`: `round % 5 == 0`, or every round in challenge 4.
- The text goes to `boss_items[2]` (a `Text`). The game also has a Russian version of each text (not listed here).
- `SelectBossRule(challenge, ...)` picks a rule at random from a pool, without repeats until the pool is used up. Then the pool refills, minus the rule just played.
- The pool is rules 2 and 5–14. Rules 1, 3 and 4 still have code (same as rule 2) but are never picked.
- Some challenges leave rules out:

| Challenge | Rules left out |
|-----------|----------------|
| 2 | 12, 14 |
| 8 (Market Demand) | 2, 9, 11 |
| 10 | 7, 13, 14 |

## Rules

`<A>`, `<B>` and `<button>` stand for a button's name: its on-screen label for `Infos.id` 35 and 36, `Infos.name` for the others.

| bossRule | Text shown | What it does | Solver status |
|----------|------------|--------------|---------------|
| 1, 3, 4 | (same as rule 2) | Never picked by `SelectBossRule`. Leftover code shared with rule 2. | Not needed |
| 2 | ``You can`t use buttons "<A>" and "<B>"`` | Blocks two buttons for the round (`Blockirator.inf_blocked`): A is a random digit key (`number_tag`; a random `operator_tag` button if there is none), B is a random `operator_tag` button. If A and B are the same, A is re-picked from the operator buttons, up to 3 times. Charges are unchanged, and adding charges doesn't unblock them (`CountUses.AddUses` skips `rules_tempus`). The d20 button re-rolls the pair mid-round (`StartLvl.Reblock`); the mod follows it (`spec/warp-rescan/`). | Complete ([`spec/boss-blocked-buttons/`](../spec/boss-blocked-buttons/)) |
| 5 | ``You don`t see some buttons labels`` | `Questionaise`: puts a `?` cover on every third digit key and every third operator button (indexes 0, 3, 6, …). Covered operator buttons also get `Infos.can = false`. The mod still reads the real ids. | Complete ([`spec/redacted-overlay/`](../spec/redacted-overlay/)) |
| 6 | `Each button press consumes 2 charges` | `take_two`: every press of a non-glass button costs 2 charges (down to 0). A button with 1 charge can still be pressed (it goes to 0), so n charges give ceil(n / 2) presses. The charge key types its charges, so it goes `c12 c10 …`. Glass presses are unchanged. `Cancel` gives 2 back. | Complete ([`spec/boss-double-charge/`](../spec/boss-double-charge/)) |
| 7 | `You need to calculate large number` | Goal is a random 100–999 (1–49 with `passive_min`; made odd with `passive_metronome`; 0 in challenge 2). | Ignored: solved as a normal puzzle |
| 8 | `The calculator screen is broken` | Hides the display (`display` off, `display_error` and `broken_screen` on). The values stay readable to the mod. | Ignored: solved as a normal puzzle |
| 9 | `You can't use legendary buttons` | Blocks the legendary buttons for the round: `Infos.id` 10, 11, 28, 46, 47 (`operator_near`, `operator_cut`, `operator_equal`, `operator_complement`, `operator_mediana`). | Complete ([`spec/boss-blocked-buttons/`](../spec/boss-blocked-buttons/)) |
| 10 | `You lose 1 coin when using the button (if more than 0)` | Each button press takes 1 coin while coins > 0 (glass presses too). Costs money, not charges. | Ignored: no penalty at $0 (buttons still work with no money), so solved as a normal puzzle |
| 11 | `You must use button "<button>", otherwise the answer will not be counted. ` | `must_butt`: a random `operator_tag` button with charges left (glass allowed). The answer counts only after it is pressed (`must_butted`); `Cancel` of that operator clears it. | Complete ([`spec/boss-required-button/`](../spec/boss-required-button/)) |
| 12 | `Each number that you send gets bigger by 1` | `Send` is the `=` handler: after the operation it adds 1 (`total++`), on **every** `=`, mid-line ones included, and on a bare `=` with no operator. The answer counts only on a display made by `=` (`can_send`), so a line that ends with edits is sent by one more `=`, which also adds 1. With `passive_metronome` the goal is made even (and `Send` makes even results odd before the +1). | Complete ([`spec/boss-send-plus-one/`](../spec/boss-send-plus-one/)) |
| 13 | `After clicking the "Send" button, the required number changes` | `renumber`: after a `Send` that misses the goal, the goal is re-rolled (`Reroll`). With `passive_near1`, a miss by 1 doesn't re-roll. Every `=` is a `Send` (see rule 12), so a mid-line `=` that misses re-rolls the goal and the rest of the line aims at an old goal. | Complete ([`spec/boss-one-send/`](../spec/boss-one-send/)): keeps only lines with no `=` before the last press. Drops two-operator lines and lines that edit after an `=`. A lucky middle `=` that hits the goal is ignored. `passive_near1` is published (`near1`) and the solver also searches G ± 1 (`spec/near-one-passive/`); the one-`=` filter is unchanged |
| 14 | `The needed number increases by 1 each time the charge is spent.` | Each press that spends a charge (non-glass) adds 1 to the goal. The goal moves while the line is pressed. | Complete ([`spec/boss-goal-per-charge/`](../spec/boss-goal-per-charge/)). With the `passive_blind` passive the round's first non-glass press is free and does not raise the goal, so a line ends at G + k - 1 (Blind checkbox; [`spec/blind-passive-rule-14/`](../spec/blind-passive-rule-14/)) |

## Notes for later specs

- 14: search goal G + k for k = 1, 2, 3, … and keep only lines with exactly k charged presses (see the spec).
- Ignored (a normal search works): 7, 8, 10. Complete: 2, 5, 6, 9, 11, 12, 13, 14.
- The mod publishes `bossRule` in `puzzle.json` and `state.json` (0 outside boss rounds).
