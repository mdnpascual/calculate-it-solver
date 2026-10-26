# Requirements: Market Demand (required button)

**Status:** Complete (2026-10-24). Tested in-game (Test E, E1–E5).

## Goal

In the **Market Demand** challenge, every round names one button that must be pressed. If the round ends without a press of it, the round's money reward is `$0`. The game shows the rule under the coins: `Use button "÷", otherwise the round reward is $0.`

Two updates:

1. **Mod:** detect the current required button and publish it in `puzzle.json` (and `state.json`).
2. **Solver:** a new **Required** textbox above Buttons. The search prefers lines that press the required button. With **Follow game** on, the box is filled from `puzzle.json` like S, G and Buttons.

## Background (REA + ilspycmd on `Assembly-CSharp.dll`)

| What | Where in the game |
|------|-------------------|
| Challenge id | `GamePrefs.GetInt("challenge") == 8` (a getter over the prefs store). Every Market Demand branch tests for 8. |
| Required button | `StartLvl.challengeButtonName` (**private** string): the `GameObject.name` of the button. `null` when no button could be picked. |
| Label shown | `StartLvl.challengeButtonLabel` (private string): `Infos.name` (`rname` in Russian), or the button's `Text` for slots 35/36. |
| Presses counted | `StartLvl.challengeButtonUses` (private int). `CountUses` press handler calls `RecordChallengeButtonUse` (`++` when the name matches, not while restoring or `notwork`). `MainCalc.Cancel` calls `CancelChallengeButtonUse` (`--`, min 0) for a cancelled operator. |
| Pick | `BeginChallengeButtonRound`: a random button under `calc_transform` with tag `operator_tag`, `uses != 0`, not `glassed`, with `Infos`, not `Blockirator.inf_blocked`. Runs at the end of `NextLvl` and from `Update` while `challengeButtonInitialized` is false (first round, after a load). |
| Reward | `CanEarnRoundCoins` = `challengeButtonUses > 0` in challenge 8; `AddRoundCoins` adds nothing otherwise. Only the **round reward** is lost, not the coins already saved. |
| Rule text | `Canvas/ShakerItems/coins and hints/rule_challenge`: `Use button "<label>", otherwise the round reward is $0.` or `No available button. Round reward: $0.` |
| Save/load | `RunSave` stores every `StartLvl` field, private ones too, so the required button survives a restore. |

Consequences for the solver:

- The required button is never a number key (`10`, `0`–`9` are `number_tag`). It can be an operator (`÷`, `mod`, `+`, …) or any left-side button with tag `operator_tag`.
- It always has charges (`uses != 0`) and is never glass when picked.
- One press is enough. Pressing an operator and then cancelling it doesn't count, but solver lines never cancel.

## Rules

### Mod (stays read-only)

1. The mod reads the challenge id (`GamePrefs.GetInt("challenge")`) and, by reflection, `StartLvl.challengeButtonName`. Reading a private field with `FieldInfo.GetValue` changes nothing in the game. No game method that changes state is called, no new Harmony patch.
2. `puzzle.json` gets two fields, after `scene`:
   - `"challenge"`: the challenge id (int, `0` when not in a challenge).
   - `"required"`: the **button id** of the required button, as used in `buttons[].id` (`buttonKey` or the object name without `(Clone)`). `null` when the challenge isn't 8, when `challengeButtonName` is null, or when no active button has that name.
3. `state.json` gets `challenge`, `required`, plus diagnostics `requiredLabel` (`challengeButtonLabel`) and `requiredUses` (`challengeButtonUses`).
4. The settle fingerprint includes `required`, so a puzzle isn't published before the pick (the `Update` path can pick it a frame late).
5. The log line adds `required=<id>` when set.
6. `version` stays 1. Old apps ignore the new fields; a new app reading an old file sees no required button.
7. If reflection fails (the field is renamed in a game update), the mod logs the error once and writes `"required": null`. Everything else keeps working.

### Solver

1. **Required box:** a textbox in the main form, in its own row directly above **Buttons**. It holds one solver token (`÷`, `X1`, `flip`, …) or is empty. Empty means "no required button" (normal search).
2. **Follow game:** when a puzzle is read, Required is set from `required` through `GameLink.MapButton`. A `null` or missing `required` clears the box. **Lock** applies as for the other boxes.
3. **Unmapped required button** (the solver doesn't know it, for example `operator_prime`): the box shows the id without `operator_` (as the ignored list does), and the status says no line can use it. The search runs as if Required were empty.
4. **Hand input:** Enter in the Required box starts the search like the other boxes. The token is parsed like a Buttons token without a charge (`÷` and `/` both work). If it isn't one of the puzzle's Buttons with a charge above 0, the status says so and the search runs as if Required were empty.
5. **Ranking:** a line that presses the required button at least once ranks before every line that doesn't, whatever the cost. Inside each group the current order applies (cost, drained, spare, kind, …).
6. **Caps:** the division cap (cheapest arithmetic + 2) and the modulo cap (best line + 2) are computed from lines that press the required button. While none is found, the cap is the current maximum (as when there is no arithmetic line). So a cheap line without the required button never prunes a dearer line with it.
7. **Display:** a line without the required button gets `, no <token>` in its row (for example `÷ 10 = X8   (3 presses, spare 14, no flip)`). The done status adds `K use <token>` (`3 division line(s), 2 modulo line(s), 0 rejected, 4 use flip`).
8. Lines without the required button stay in the list, below the others. If none uses it, the user can still pick the cheapest one and lose the reward.
9. The required press uses a charge like any press. `Fits` doesn't change.

## Examples

| `puzzle.json` | Required box |
|---------------|--------------|
| `"challenge": 8, "required": "operator/"` | `÷` |
| `"challenge": 8, "required": "operator_reverse"` | `flip` |
| `"challenge": 8, "required": null` (no available button) | empty |
| `"challenge": 0, "required": null` | empty |
| no `required` field (old mod) | empty |
| `"challenge": 8, "required": "operator_prime"` | `prime`, status: no line can use it |

Puzzle 41 → 48, Buttons `÷:3, 10:13, X8:2, 8X:2, flip:2`, Required `flip`:

| Rank | Line | Note |
|------|------|------|
| 1 | `8X ÷ 10 = flip` (4 presses) | uses `flip` |
| 2 | `÷ 10 = X8` (3 presses) | `no flip` |

## Out of scope

- Making every search *build* lines around the required button. The searches keep producing the lines they produce today; v1 only filters and ranks. A required button that isn't useful for the puzzle (for example `X++` when no transform line exists) may give no line that uses it. Required-aware search (a "used it" bit in the `LeftSearch` / `LeftEdits` state, or padding a line with a harmless press) is a follow-up.
- Boss rule 11 (`StartLvl.must_butt`, "You must use button …, otherwise the answer will not be counted"). Same idea, harder penalty, different field. Follow-up; it could reuse the Required box.
- Re-solving after the required button is pressed (one solve per puzzle, as now).

## Open questions

1. (Answered by the user, 2026-10-24: yes, challenge id 8 is Market Demand.)
2. (Answered by the playtest, 2026-10-24: yes.) Left-side buttons can be picked: the game named `money`, a left-side button the solver doesn't support yet. So required-aware search stays a worthwhile follow-up.
