# Requirements: coinsub button (fallback loop when no line is found)

**Status:** Complete 2026-10-28 (the presses are a loop outside the 12-press cap, label `$++`). Complete: Test AA done in game except AA2 and AA5, which wait for a round that plays a `$++ (n press)` line. Press counts leave `=` out, so the 39 -> 48 line is 4 presses.

## Why

`operator_coinsub` pays coins to add 1 to the display. The solver doesn't map it, so a round that has no line with the other buttons shows "No line fits…", even when a few coinsub presses at the start would make one. The user's rule: search with 0 coinsub presses; no line → press coinsub once and search again; no line → twice; and so on until a line is found or coinsub can't be pressed any more.

## Game facts (ilspycmd 9.1 on `Assembly-CSharp.dll`, UnityPy onClick in `level1`, 2026-10-28)

| What | Where | Detail |
|------|-------|--------|
| onClick | scene `level1`, `operator_coinsub` | `CountUses.SubUse`, then `MainCalc.UseCoins(-1)`, then a sound |
| Effect | `MainCalc.UseCoins(-1)` | Only if `StartLvl.coins > 0`: `ChangeCoins(-1)` and `PlusNumber(1)` |
| `PlusNumber(1)` | `MainCalc` | The same code as `X++`: `total + 1`, clamped to 999 999; reads the number (leading zeros drop). Left side only. |
| At 0 coins | `UseCoins` | Nothing happens, but `SubUse` already ran: the charge is used. |
| Charges | `CountUses` | A normal charged button (`uses`, glass possible). `SubUse` runs first: in boss rule 10 it takes a coin **before** `UseCoins` checks the coins, so a press costs $2, and with $1 it takes that coin and then does nothing. At `diff_type` ≥ 7 (from the round's 3rd charged press) `SubUse` drains a coin the same way. In boss rule 14 it raises the goal by 1. |
| Warp | `StartLvl.D20` | Warp keeps `operator_coinsub` (it isn't replaced). |
| Face | scene | The button's Text child is `11` (the face is likely an icon). |

`ChangeCoins(-1)` doesn't trigger `passive_cointobut` (it only acts on `amount > 0`).

## What the solver must do

1. **Token and label.** Game id `operator_coinsub` → internal token `coinsub` (in `GameLink.MapButton` and the mod's `ButtonSymbols`). Label `$++` in the app and the overlays (`button-labels/` style). The Buttons and Required boxes accept both (`coinsub:5`, `$++:5`, `:Ng` too).
2. **Simulator.** A `coinsub` press, left side only. First the usual charged-press effects (rule-10 coin, the `diff_type` ≥ 7 drain, the rule-14 goal raise, glass, the blind free press). Then: if money > 0, money − 1 and display + 1 (clamp 999 999, leading zeros drop); else nothing.
3. **The loop.** For n = 0, 1, 2, …:
   - press coinsub n times from S (in the simulator), giving a new state: display S′, coins left, the goal the game now shows (rule 14), the charged presses so far, the blind flag;
   - run the whole normal search (every phase, the 12-press cap, Required, Near 1, rules 11, 12, 13) from that state, with no more coinsub in the rest of the line;
   - a line found → stop after this n (finish it, so all its lines show). n = 0 is today's search.
4. **Outside the 12-press cap.** The n coinsub presses don't count toward `MaxPresses` (12): the rest of the line still gets all 12. The press count shown on the line includes them.
5. **When the loop stops without a line.** The next press would do nothing or can't be made:
   - coinsub has no charge left (n = its charges; a glass coinsub allows 1);
   - **not enough money** for the next press, checked before it is made:
     - money **< $1** outside boss rule 10;
     - money **< $2** in boss rule 10 (its coin is taken first, so with $1 the press does nothing);
     - at `diff_type` ≥ 7, from the round's 3rd charged press, the drain takes a coin first too, so that press also needs $2 (in rule 10, $3);
   - the press would add nothing for another reason (the simulator's check, a guard);
   - the display is 999 999;
   - Esc.
6. **Each loop changes the state.** What carries from one n to the next:
   - money: − 1 per press, − 2 in rule 10, and the `diff_type` ≥ 7 drain;
   - boss rule 14: each charged coinsub press raises the goal by 1, so the n-th search aims at G + n (+ k for the rest), less 1 for the blind free press and for a glass press (`spec/blind-passive-rule-14/`);
   - the blind flag: the first non-glass coinsub press uses it, so the rest runs without it;
   - charged presses so far, for the `diff_type` ≥ 7 drain.
7. **Only the direct leg after n ≥ 1.** `swap` must be the first press, so no swap leg after a coinsub press.
8. **Short text.**
   - Line: the n presses show as one group, `$++ (3 press) | / | 10 | = | X8   (6 presses, …)`. Ranking unchanged (only one n has lines).
   - Status while the loop runs (n ≥ 1): `Searching… $++ (3 press). Esc to stop.` The end status is the normal one (`DoneStatus` / `NoLineStatus`). No new parts.

## Examples

| S | G | Money | Rule | Buttons | Result |
|---|---|-------|------|---------|--------|
| 39 | 48 | 5 | — | `÷:1, 10:1, X8:1, coinsub:5, =` | n = 0: none (`÷ 10 = X8` → 38). n = 1: `$++ (1 press) \| / \| 10 \| = \| X8` (40 ÷ 10 = 4 → 48), 4 presses (`=` is free) |
| 41 | 44 | 5 | — | `coinsub:5, =` | n = 3: `$++ (3 press)` (the display is the goal after the presses) |
| 41 | 44 | 2 | — | `coinsub:5, =` | n = 1, 2 give 42, 43; n = 3 needs a 3rd coin: no line |
| 41 | 44 | 5 | 10 | `coinsub:5, =` | each press costs $2: n = 2 → 43 with $1 left; the 3rd press would do nothing: no line |
| 41 | 44 | 6 | 10 | `coinsub:5, =` | n = 3 → 44, $0 left: `$++ (3 press)` |
| 41 | 45 | 5 | 14 | `coinsub:5, X++:9, =` | n = 0 searches goal 45 + k from 41; n = 1 searches from 42 with goal 46 + k; … (no line here: every press raises both) |

When the display after the n presses is already the goal it must reach, the line is the n presses alone (in rule 12 the rest search adds the bare `=`, as it does today for S = G − 1).

## Out of scope (follow-ups)

- coinsub in the middle or at the end of a line (for example `÷ 10 = $++`), and coinsub as a normal left transform in `LeftSearch`.
- Using coinsub when a line already exists but coinsub would make a shorter one.
- `operator_coinadd`.
- The swap leg after coinsub presses.

## Open questions

- What the in-game button face shows (Test AA1). The label stays `$++` either way (user's choice).
