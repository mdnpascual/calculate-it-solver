# Requirements: Boss rule 12, every `=` adds 1

**Status:** Complete (2026-10-25). Test N: N1–N3 confirmed in game, N4 skipped.

## Why

Boss rule 12 (see [`docs/boss-rules.md`](../../docs/boss-rules.md)) shows *"Each number that you send gets bigger by 1"*. Today the app runs a normal search and shows `boss rule 12 not supported`. The lines it shows are wrong: every one of them ends 1 or more above the goal.

## Game facts (static analysis, `Assembly-CSharp.dll`, 2026-10-24)

| What | Where | Detail |
|------|-------|--------|
| Rule active | `StartLvl.NextLvl` | `rule = SelectBossRule(...)` in a boss round (`round % 5 == 0`, or every round in challenge 4). Not in challenge 2. Can happen in challenge 8 (Market Demand). |
| "Send" is `=` | `MainCalc.Send` | `Send` is the `=` handler. It applies the pending operator (`+`, `-`, `×`, `÷`, `mod`, `%`), then `if (rule == 12) { total++; }` and writes the display. So **every** `=` adds 1 to its result, not only the last one. |
| `=` with no operator | `MainCalc.Send` | No operator branch runs (also after `a->c` / `b->c`, whose key already set the value), so the display just gets the +1. |
| Win check | `StartLvl.Update` | The answer counts when the display equals the goal **and** `MainCalc.can_send` is true. `can_send` turns true only when the score animation (`Count`) started by `Send` ends; every other press sets it false. So the answer is always sent by an `=`. A line that ends with left edits (`÷ 10 = X8`) is sent by one more `=` that the solver doesn't list (the "send `=`"). |
| Mid-line `=` | same | A `=` in the middle doesn't fail the round (rule 13 isn't active with 12). Its result is 1 higher, and the line goes on from there. If that result already equals the goal, the round ends early. |
| `=` blocking | `MainCalc.Send`, left-side handlers | `Send` blocks `=` when the score changes; left-side presses (`Xd`, `sqr`, …) unblock it. So `= =` in a row doesn't work, but `=` right after an edit does. |
| `operator_swap` | `MainCalc.Swap` | Calls `Send`, so +1 too. Not a solver button; out of scope. |
| `passive_metronome` | `Send`, `NextLvl` | Before the +1, `Send` makes an even result odd; the rule-12 goal is made even. The solver doesn't model metronome; out of scope. |
| Charges | | `=` has no charges, so rule 12 costs nothing extra. |

## The model

With rule 12, every `=` the line presses adds 1 to its result, and the line ends with a send `=` that also adds 1:

- A line whose last press is `op D =`: that `=` is the send. Its operation must give **G − 1**.
- A line that ends with left edits (or has no operator): the edits must reach **G − 1**, then the send `=` gives G.
- Every `=` before the end: its result is the operation's result **+ 1**, and the next presses start from that.

Examples (S = 41):

| G | Buttons | Line | Why |
|---|---------|------|-----|
| 59 | `÷:9, 10:9, X8:9, =` | `÷ 10 = X8` (3 presses) | 41 ÷ 10 = 4, +1 → 5; `X8` → 58; send `=` → 59 |
| 48 | `÷:9, 10:9, X8:9, =` | `÷ 10 = X8` **not** kept | it sends 59 |
| 5 | `÷:9, 10:9, =` | `÷ 10 =` (2 presses) | 4, +1 → 5, and that `=` is the send |
| 48 | `+:9, 0-9 keys, =` | `+ 6 =` (2 presses) | 47, +1 → 48 |
| 439 | `+:9, 1:9, X8:9, =` | `+ 1 = X8` (3 presses) | 42, +1 → 43; `X8` → 438; send → 439 |
| 49 | `2X:9, 3X:9, mod:9, 10:9, 3:9, =` | `2X 3X mod 10 3 =` (5 presses) | 3241 mod 103 = 48, +1 → 49 |
| 42 | `=` and anything | `=` (0 presses) | S = G − 1: the send alone |

S = 59, G = 40, buttons `-:9, 0-9 keys, =`: `- 2 0 =` (39, +1 → 40). The normal answer `- 1 9 =` sends 41.

## What the solver must do

1. **Know the rule.** The Boss box already exists (`boss-goal-per-charge/`) and Follow game fills it from `bossRule`. Rule 12 drops out of the `not supported` note (2, 6, 9 stay).
2. **Simulator.** `EngineeringCalculator.Run` gets a rule-12 mode: each `=` adds 1 to its result. A new `Sent` gives the number a line sends: the last display, plus 1 in rule-12 mode when the last press isn't `=` (the send `=`). Without rule 12, `Sent` is the last display.
3. **Bare `=` (S = G − 1).** In rule-12 mode a line can be the single press `=` with no operator (0 presses, it costs nothing). `Run` allows a bare `=` only as the last press of a line in rule-12 mode; everywhere else it stays rejected as today.
4. **Every phase finds rule-12 lines.** Arithmetic (with edits-only and remorph lines), two-operator, division and modulo all take a rule-12 flag. With it, the line must send G: left edits aim at G − 1 (the display before the send), and each mid-line `=` result is 1 higher. Every kept line is checked with `Sent(...) == G` in rule-12 mode. Caps, `accept`, charges, ranking and the 100-line limits work as today.
5. **Required button together.** Rule 12 and rule 11 never meet (one rule per round). Market Demand (challenge 8) can: Required works as now.
6. **Status.** Done status starts with `Rule 12: every = adds 1: ` followed by the usual counts. No line: `No line sends G with every = adding 1.` (with the real G).
7. **Lines** look as today. The send `=` at the end of a line that ends with edits stays unlisted, as in a normal round.

## Out of scope

- The free +1 of a bare `=` in the middle of a line (`X8 = X1`: 418, +1 → 419, then 4191). The code reading says it works once after each left edit, but it is untested in-game. Follow-up after Test N5.
- `passive_metronome`, `operator_swap`, the 999 999 cap and the 6-character display (separate spec, [`display-cap/`](../display-cap/); the +1 comes after the clamp, so 999 999 + 1 = 1 000 000).
- Rule 14 together (never: one rule per round).
- Two-operator segments that change nothing except the +1 (`× 1 =`) stay skipped.

## Open questions (Test N)

- N2: does a line with an `=` in the middle that ends with edits (`÷ 10 = X8` on 41) send 59 after the send `=`?
- N3: does a line that ends with `op D =` send its result + 1, with no extra `=`?
- N4: S = G − 1: does pressing `=` first, with no operator, win the round?
- N5 (for the follow-up): after an `Xd`, does a bare `=` add 1 and let the line go on?
