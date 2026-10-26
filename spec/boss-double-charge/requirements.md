# Requirements: Boss rule 6, each press costs 2 charges

**Status:** Done 2026-10-25 (Test Q1–Q3 confirmed; Q4 not run).

## Why

Boss rule 6 (see [`docs/boss-rules.md`](../../docs/boss-rules.md)) shows `Each button press consumes 2 charges`. A button with 3 charges can be pressed only 2 times in that round, not 3. Today the solver counts 1 charge per press, so it can show a line that runs out of charges in the middle. The charge key also types a different number. The status only says `boss rule 6 not supported`.

## Game facts (static analysis, `Assembly-CSharp.dll`, ilspycmd 9.1, 2026-10-25)

| What | Where | Detail |
|------|-------|--------|
| Rule active | `StartLvl.NextLvl`, `rule == 6` → `take_two = true` | Only in a boss round. Reset by `ResetPreviousBossEffects`. Can happen in every challenge (not left out anywhere), so also with Market Demand (challenge 8). One boss rule per round, so never with rules 11, 12, 14. |
| Charge cost | `CountUses.SubUse`, non-glass branch | `uses = Mathf.Max(0, uses - 2)` instead of `- 1`. |
| Last charge | same | A button with **1** charge can still be pressed (it goes to 0, not −1). Then `inf_blocked`. So a button with n charges gives **ceil(n / 2)** presses: 1 → 1, 2 → 1, 3 → 2, 4 → 2. |
| Glass | `CountUses.SubUse`, glass branch | No charge change; the button breaks after one press, as in a normal round. |
| Charge key | `UsesNumber.Update`: `digit = uses` | The key types its charge count, and each press lowers it by 2: with 12 charges, `c12 c10 c8 …`. With 13: `c13 c11 … c1`. |
| `Cancel` | `MainCalc.Cancel` | Gives the operator back 2 charges in rule 6. The solver never presses Cancel. |
| `=` | not a `CountUses` button | No charges, as today. |

## What the solver must do

1. **Know the rule.** The Boss box already exists (filled by Follow game from `bossRule`, or typed). No mod change: `bossRule` and raw `uses` are already published.
2. **Buttons box unchanged.** It keeps the game's real charge counts (`X8:3`), so it matches the screen.
3. **Presses per button.** With Boss 6, every non-glass button with n charges can be pressed ceil(n / 2) times in a line. Glass buttons stay at 1 press. Lines that need more are rejected and counted in `R rejected`, like today.
4. **Charge key.** With Boss 6 the charge key's k-th press types `start − 2(k − 1)` (`c12 c10`), and the key can be pressed while that value is at least 1 (and within ceil(n / 2) presses). The simulator, the operand typing in every phase and the line text follow this.
5. **Ranking.** `Drained` and `Spare` count presses left, not charges: a button with 3 charges is drained after 2 presses; spare is the number of presses left. This keeps the same meaning as in a normal round ("how many more times can I press it").
6. **Status.** No status prefix (one was added, then dropped after Test Q1). Rule 6 leaves the `boss rule N not supported` list.
7. **Required button** (Market Demand in a rule 6 round): unchanged. A required button with 1 charge still counts as usable.

## Examples

Boss 6, S 41, G 48:

| Buttons | Line | Fits? |
|---------|------|-------|
| `÷:1, 10:1, X8:1` | `÷ 10 = X8` | yes (1 charge = 1 press) |
| `X1:2, 8X:2, 4X:2, ÷:2, 10:2` | `X1 8X ÷ 10 10 = 4X` | **no**: `10` has 2 charges = 1 press, the line presses it twice |
| `X1:2, 8X:2, 4X:2, ÷:2, 10:3` | `X1 8X ÷ 10 10 = 4X` | yes (3 charges = 2 presses) |
| `÷:2, c12:12, X8:2` | `÷ c12 c10 = …` | operand `1210` (not `1211`) |

Without Boss 6, the second row fits (2 presses of `10`, 2 charges).

## Out of scope

- The game's `blind` passive (`GamePrefs` `blind`: one free press). Not modelled today in any round.
- Cancel.
- Showing "presses left" in the Buttons box. It shows real charges; the conversion is inside the search.

## Open questions

- The charge key order: does a press type its value **before** the charge drops (`c12 c10`, assumed here and by `more-buttons/`) or after (`c10 c8`)? `more-buttons/` Test H1 left the normal-round order (`1211`) unconfirmed. Test Q checks it in a rule 6 round.
