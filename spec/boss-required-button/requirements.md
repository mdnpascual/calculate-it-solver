# Requirements: Boss round required button (rule 11)

**Status:** Complete (2026-10-24). Tested in-game, Test K (challenge 4): K1–K5 confirmed.

## Goal

Some boss rounds say: `You must use button "<name>", otherwise the answer will not be counted.` The mod doesn't publish that button today, so the app's Required box stays empty in those rounds. Publish it through the same `required` field that Market Demand uses. Unlike Market Demand, the app then shows **only** lines that press it, and skips search phases that can't press it.

## Why it wasn't scanned

It is a **different mechanism** from Market Demand, not the same one hidden behind the `challenge == 8` check. Removing that check alone would not have helped:

| | Market Demand | Boss rule 11 |
|---|---|---|
| When | Every round of challenge 8 | A boss round (`round % 5 == 0`, or every round in challenge 4) whose boss rule is 11 |
| Button | `StartLvl.challengeButtonName` (private string, the `GameObject.name`) | `StartLvl.must_butt` (**public** `GameObject`) |
| Flag | `StartLvl.challengeButtonUses` (private int) | `StartLvl.must_butted` (public bool) |
| Picked by | `BeginChallengeButtonRound`, which **returns at once unless challenge is 8**, so `challengeButtonName` stays null in every other challenge | `NextLvl`: `rule = SelectBossRule(...)`; for `rule == 11`, a random `operator_tag` button with `uses != 0` (glass allowed), then `must_butted = false` |
| Penalty | Round reward is `$0`; the round still ends | **The answer is not counted**: `StartLvl.Update` doesn't end the round while `must_butted` is false, even with the goal on the display |
| Pressed | `RecordChallengeButtonUse` (name match) | `CountUses` press handler: `rule == 11 && IsBossRound && name == must_butt.name` sets `must_butted = true`. `MainCalc.Cancel` sets it back to false when a cancelled operator is the required one |
| Cleared | Each round | `ResetPreviousBossEffects`: `must_butt = null`, `must_butted = true` |

**They never happen together:** `SelectBossRule` leaves out rules 9 and 11 in challenge 8. Rule 11 can happen in every other challenge (normal runs too). So one `required` field is enough.

(ilspycmd on `Assembly-CSharp.dll`, 2026-10-24: `StartLvl.NextLvl`, `SelectBossRule`, `IsBossRound`, `ResetPreviousBossEffects`, `Update`; `CountUses` press handler; `MainCalc.Cancel`.)

## Rules

### Mod (stays read-only)

1. `required` in `puzzle.json` is the button id of:
   - the Market Demand button when challenge is 8 (unchanged), else
   - `must_butt` when `IsBossRound && rule == 11 && must_butt != null`: the id of the active button whose raw `GameObject.name` equals `must_butt.name` (as the game compares names), else
   - `null`.
2. New field `"requiredKind"` after `required`: `"market"`, `"boss"`, or `null` (when `required` is null).
3. `state.json` gets `requiredKind`, plus diagnostics `bossRule` (`rule` when `IsBossRound`, else `0`) and `mustUsed` (`must_butted`).
4. The settle fingerprint already includes `required`, so a boss pick holds the publish until it is stable. Add the kind to the fingerprint as well.
5. The log line shows `required=<id> (boss)` for a boss pick.
6. Only public fields and the `IsBossRound` getter (it reads `round` and `GamePrefs.GetInt`) are read. No new patch, nothing written.
7. `version` stays 1. An older app reads `required` and works as for Market Demand.

### App

8. `GameLink.Parse` reads `requiredKind` into `GamePuzzle.RequiredKind` (`"boss"`, `"market"`, or null; missing is null).
9. Follow game fills the Required box exactly as today.
10. **Market Demand stays as it is:** lines without the button are kept (marked `no <token>`, ranked last), because the penalty is only the round's money.
11. **Boss: only lines that press the required button are shown.** A line without it can't win (the game doesn't accept the answer), so it is dropped, not ranked last. Lines dropped this way aren't counted as `rejected` (that count stays for charge rejections).
12. **Boss mode** is on when the puzzle's kind is `boss` and the Required box still holds the token Follow game filled in. A hand edit of the box (another token, or empty) turns it off and the search behaves as Market Demand / no required button.
13. **Search shortcuts in boss mode** (every search keeps its own logic; these only skip work that can't give a line with the button):
    - The required button is an **operator** (`÷`, `mod`, `+`, `-`, `×`, `%`, `a->c`, `b->c`). Every line has exactly one operator, so only the phase that uses it runs:
      - `+`, `-`, `×`, `%`, `a->c`, `b->c`: the arithmetic phase only, with only that operator. Its edits-only lines are skipped. Division and modulo don't run.
      - `÷`: the division phase only. Arithmetic and modulo don't run.
      - `mod`: the modulo phase only, with the cap it uses when there is no `÷` today (12).
    - The required button is a **left-side** button (`flip`, `X8`, `8X`, a transform, `last`, `reset`, …). Any phase can press it inside its edits, so all phases run, and lines without it are dropped as they are found. The 100-line limits of `ArithmeticSearch` and `ModuloSearch` count only lines that press it, so lines without it can't crowd them out.
    - The caps already count only lines that press the button (`market-demand/` rule 6), so a cheap line without it never prunes a dearer line with it.
14. **No line uses it:** the list is empty and the done status says `0 use <token>`.
15. **The solver doesn't support it** (an unmapped button such as `money`, or a token not in Buttons): solve as if there were no required button. Every phase runs, every line is shown, nothing is filtered or marked `no <token>`. The status keeps today's note (`no line can use <token>` or `<token> isn't in Buttons`), as Market Demand does.
16. **Status** in boss mode: the same `K use <token>` as Market Demand (an extra "boss" note was tried and removed). `K` is the number of lines shown, so it can be capped by the 100-line limit.

## Examples

| Game state | `required` | `requiredKind` |
|------------|------------|----------------|
| Challenge 8, round 3, button `÷` named | `operator/` | `market` |
| Normal run, round 10, boss rule 11 names `flip` | `operator_reverse` | `boss` |
| Normal run, round 10, another boss rule | `null` | `null` |
| Normal run, round 11 (after the boss) | `null` | `null` |
| Challenge 4 (every round a boss), rule 11 names `X8` | `operator_x8` | `boss` |

Puzzle 41 → 48, Buttons `÷:3, 10:13, X8:2, 8X:2, flip:2`:

| Required | Kind | Lines shown |
|----------|------|-------------|
| `flip` | `market` | `8X ÷ 10 = flip` (4), then `÷ 10 = X8` (3, `no flip`) |
| `flip` | `boss` | `8X ÷ 10 = flip` (4) only |
| `÷` | `boss` | division lines only; arithmetic and modulo phases skipped |

## Out of scope

- Required-aware search for **left-side** buttons (building lines around the button: a "used it" bit in the `LeftSearch` / `LeftEdits` state, or padding a line with a press that doesn't change the result). Without it, a left-side button the cheapest edits don't need may give no line. Follow-up, shared with `market-demand/`; it matters more here, since in a boss round such a puzzle shows nothing.
- The other boss rules (`SelectBossRule` picks from 2 and 5–14). Rule numbers other than 11 are only reported as `bossRule` for later work.
- Re-solving after the button is pressed.

## Open questions

1. The user met this rule in which challenge? Any challenge except 8 can have it; Test K checks a normal run.
