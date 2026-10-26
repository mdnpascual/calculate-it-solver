# Requirements: Boss rule 14, goal +1 per charge

**Status:** Implemented (2026-10-24). Tested in-game, Test L: L1–L3 confirmed (the goal rises before the check on `=`); glass press untested.

## Why

Boss rule 14 (see [`docs/boss-rules.md`](../../docs/boss-rules.md)) shows *"The needed number increases by 1 each time the charge is spent."* Every press that spends a charge adds 1 to the goal while the line is pressed. A line found for goal G is wrong: after k charged presses the game wants G + k, not G.

## Game facts (static analysis, `Assembly-CSharp.dll`)

| What | Where | Detail |
|------|-------|--------|
| Rule active | `StartLvl.IsBossRound && StartLvl.rule == 14` | `IsBossRound`: `round % 5 == 0`, or every round in challenge 4. Not in challenges 2 and 10 (`SelectBossRule`). Can happen in challenge 8 (Market Demand). |
| Goal raise | `CountUses.SubUse`, non-glass branch | `uses = max(0, uses - 1)`, then `number++` and the goal text is updated. One per press. |
| Glass | `CountUses.SubUse`, glass branch | A glass press skips that branch: **no** goal raise. |
| `=` | not a `CountUses` button | No charges (not in `puzzle.json`), so no goal raise. The solver's `Cost` already skips it. |
| Rule 6 together | never | One boss rule per round. |

So a line with **k charged presses** (every press except `=` and glass presses) must end at **G + k**.

## What the solver must do

1. **Know the rule.** The mod publishes `bossRule` in `puzzle.json` (0 outside boss rounds; it is already in `state.json`). The app gets a **Boss** box (a number, empty = none) above Buttons. Follow game fills it (empty for 0). It can also be typed by hand, and Enter in it runs the search like the other boxes.
2. **Stepped search (rule 14 only).** For k = 1, 2, 3, …:
   - target = G + k,
   - search every phase (arithmetic, division, modulo) for the target, with at most k charged presses,
   - keep only lines with **exactly k charged presses**,
   - stop after the k where the kept lines reach **10** (the Lines box shows 10, `MaxShownLines`), or after k = 12 (`MaxPresses`), or on Esc.

   k starts at 1 because a line needs at least one press. Finish the whole k before stopping, so all lines of that length are seen.
3. **Glass.** A glass button's press isn't charged. With glass, a line can have more presses than k (`÷ 10 = X8` with glass `X8` has 3 presses, k = 2). Today the solver doesn't know glass: `GameLink.Parse` only clamps its charge to 1, so `X8:1` looks like a normal button. New Buttons syntax: **`X8:1g`** marks a glass button (`g` after the count; any count is clamped to 1, like the game; `X8:0g` leaves the button out like `:0`). Follow game writes it, it can be typed by hand, and `Buttons` keeps the set of glass button keys (`Press.ChargeKey`: `X8`, `÷`, `c`, …). Outside rule 14 it changes nothing but the clamp.
4. **Lines show the goal they reach**: `÷ 10 = X8   (3 presses, goal 48, spare 26)`. Ranking is unchanged (cost first).
5. **Status**: `Rule 14: goal 45 + k, k = 1..3: 1 arithmetic line(s), 0 division line(s), 0 modulo line(s), 0 rejected` (plus the usual Required parts).
6. **One solve per rule-14 round.** Today a puzzle is (round, goal), so each press in a rule-14 round would publish a "new" puzzle and restart the app's search. Two guards:
   - **Mod:** in a rule-14 round it publishes once per **round** and ignores goal changes.
   - **App (backup):** when the last applied puzzle had `bossRule` 14 and a new file has `bossRule` 14 and the **same round**, the app ignores it: the S, G and Buttons boxes and the Lines box stay, and no search starts. A new round (or a rule other than 14) is applied as usual. The check is on the round, not only on the rule, so a second rule-14 round (possible after a save restore) still gets solved. Unchecking Lock or re-checking Follow game clears the guard (they mean "read the file again").
7. **Other boss values.** Rules 5, 7, 8, 10, 11, 13 and 0 give a normal search with no note (11 keeps its Required handling). Rules 2, 6, 9 and 12 give a normal search and the status note `boss rule N not supported`.
8. **Market Demand together.** Rule 14 can happen in challenge 8. Required (`market`) works as now: lines that don't press it stay, show `no <token>`, and rank last.

## Examples

S = 41, rule 14:

| G | Buttons | k tried | Line | Reaches |
|---|---------|---------|------|---------|
| 45 | `÷:9, 10:9, X8:9, =` | 1 (46): none, 2 (47): none, 3 (48) | `÷ 10 = X8` (3 presses) | 48 = 45 + 3 |
| 46 | `÷:9, 10:9, X8:1g, =` | 1 (47): none, 2 (48) | `÷ 10 = X8` (3 presses, 2 charged) | 48 = 46 + 2 |
| 46 | `÷:9, 10:9, X8:9, =` | 2 (48) | `÷ 10 = X8` is **not** kept (3 charged presses, so it reaches 49, not 48) | — |

Buttons syntax: `X8:1g` → glass `X8`, 1 charge. `X8:4g` → glass, clamped to 1. `X8:0g` → left out. `X8:g` and `X8:1x` → `Bad charge count`.

App guard: round 10 rule 14 applied → file for round 10 rule 14 with goal 49 is ignored → file for round 11 is applied.

## Out of scope

- Re-solving in the middle of a rule-14 round, for example after a wrong Send. The user types the current S and G and presses Enter (the Boss box stays 14). Unchecking Lock doesn't help here: it re-reads the file, which still holds the round-start puzzle.
- Rules 2, 6, 9, 12 (separate specs; see the doc).
- The case where G + k equals S: that k is skipped (no line of k presses that comes back to S is searched).
- Lines that hit the moving goal before their last press (the game would accept them early). They would have been found at a smaller k anyway.

## Open questions

- Does the game raise the goal before or after it checks the answer on `=`? Static reading says the raise happens in `SubUse` on the button press, and `=` has no charges, so the last charged press raises it before `=`. Confirmed in-game (Test L2): the raise happens before the check on `=`.
