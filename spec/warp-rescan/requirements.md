# Requirements: Warp re-scan

**Status:** Implemented 2026-10-25; in-game complete; Test S done, S4 and S5 (boss rules 14 and 2) skipped. The investigation is done: the mod **can** detect the press.

## Why

The `operator_warp` button (the d20) replaces every button on the calculator with random buttons in the middle of a round. Today the mod publishes `puzzle.json` once per puzzle (round, goal). A warp keeps the round and the goal, so the mod doesn't publish again. The app keeps solving with the old buttons, and its lines can't be pressed any more.

After a warp, the mod must publish the new button set and the app must solve again.

## Game facts (static analysis, 2026-10-25)

Sources: `Assembly-CSharp.dll` (SHA-256 `0ec2d076…981862`, MVID `378f7def-c5c7-4e39-81b2-b5ba87031b9c`) decompiled with ilspycmd 9.1; REA `inspect_managed_members` on `StartLvl` (Evidence `ev_55c4502c1cc9320ea47f563607ba3b82989dde1f81220f0a17edf3d52c544fda`); UnityPy read of the `operator_warp` object's `Button.m_OnClick` in `level1`.

| What | Where | Detail |
|------|-------|--------|
| What a click calls | `operator_warp` → `Button.m_OnClick` (scene data, `level1`) | 1. `CountUses.SubUse()`, 2. `StartLvl.D20()`, 3. two `AudioSource.PlayOneShot`. The call is wired in the scene, not in C#: no C# method calls `D20` (whole-assembly search). |
| Charge | `CountUses.SubUse` | A normal charged press: `uses - 1` (`- 2` in boss rule 6), and in boss rule 14 the goal goes up by 1, **before** `D20` runs. At 0 charges the button gets `inf_blocked` and can't be pressed again. |
| The re-roll | `StartLvl.D20()` (public, no parameters) | Deactivates (`active = false`, not destroyed) every `number_tag` object and every `operator_tag` object except `operator_warp` and `operator_coinsub`. Then creates `count − 2` new buttons, `count` = number of buttons before. |
| New buttons | `D20` → `GenerateUniqueNumbers(0, buttons_tr.Length − 1, count, 30, 33, 43)` → `MiniCreate` | Random distinct entries of `StartLvl.buttons_tr` (the collection list in `docs/button-id.md`), without slots 30 (`operator_coinsub`), 33 (second `operator_round`) and 43 (`operator_resurrect`). Each is instantiated under `calc_transform`, gets a free grid cell (`PlaceFind(1)`, can be -1 when the grid is full: then it is skipped) and **`uses = 1`**. A new number key gets `_blocked = true` (the usual left-side lock). |
| Then | `MainCalc.InitButts()`, `StartLvl.Reblock()` | `InitButts` re-reads the button arrays. `Reblock` (boss rules 1–4 only, in practice rule 2) picks two new blocked buttons (`inf_blocked`). |
| Not changed | `D20` | Round, goal (except the rule 14 `+1` from the press), `MainCalc.total` (left side), `operand`, `currentoperand`, `last`. |
| Infos | `operator_warp`'s `Infos` | A leftover copy (`name` "Cash", `id` 31). Don't identify the warp by `Infos`; use the object name. |

Consequences:

- `buttons_tr` has only one plain digit key (`number1`), plus `number_quest`, `number_charge` and `number_mw`, and no `number10`. After a warp the right side often has few or no digit keys, many new buttons are unmapped (`operator_random`, `operator_bomb`, …, they go to `Ignored`) and every new button has 1 charge. "No line found" will be a common, correct answer.
- The mod reads active buttons only (`GetComponentsInChildren<CountUses>(false)`), so the deactivated old buttons already drop out of its read.

## Can the mod detect the press?

**Yes.** Two ways, the first one is chosen:

1. **Harmony postfix on `StartLvl.D20`** (chosen). `D20` is public and has a body in `Assembly-CSharp.dll`. Harmony patches the method itself, so the postfix runs however `D20` is called, including through the `UnityEvent` in the scene. It runs right after the new buttons exist. It only sets a flag (the same pattern as the `NextLvl` postfix), so the mod stays read-only.
2. Without a patch: notice that the set of button ids changed while round and goal stayed. Rejected: a shop purchase also changes the set, and `game-state-scan/` says purchases must not re-trigger a solve.

Not observed yet (needs the in-game test): that the postfix fires once per click, and that 50 ms settle is enough after the re-roll.

## What the solver must do

1. **The mod re-publishes after a warp.** When `D20` runs, the current puzzle becomes pending again. It is published with the normal settle rules (not restoring, not transitioning, left ≠ goal, fingerprint stable for 50 ms). One publish per warp.
2. **A warp counter.** `puzzle.json` and `state.json` get `"warp": n`, the number of warps in this round (0 when none). It resets at `NextLvl`. The log line says `puzzle ready (warp n): …` when n > 0.
3. **The app solves again.** A new `seq` already makes the app apply the file, so in a normal round nothing else is needed. In boss rule 14 the app ignores a second file from the same round (`appliedStepRound`); it must accept it when `warp` changed. The mod's rule-14 "once per round" rule must also let the warp publish through.
4. **Status.** When `warp > 0`, the status prefix says `Game: round R, S → G, warp n.` so the user can tell the re-solve came from the warp.
5. **Lock.** While `Lock` is checked, a warp file is ignored like any other file (today's behaviour).
6. **Older mod.** No `warp` field → 0 (today's behaviour).

Side effects that come for free:

- Boss rule 2: `Reblock` sets new blocked buttons, and the new file carries them (`blocked`). This closes the "d20 re-roll is not followed" follow-up of `boss-blocked-buttons/`.
- The calculator overlay (`redacted-overlay/`) already reads the buttons live.

## Examples

Round 5, S 41, G 48, buttons `÷:3, 10:4, X8:2, flip:1`, plus `operator_warp` with 2 charges.

| Event | `puzzle.json` | App |
|-------|---------------|-----|
| Round loads | `seq` 7, `warp` 0, the four buttons | solves, top line `÷ 10 = X8` |
| Warp pressed | `seq` 8, `warp` 1, start 41, goal 48, the new buttons each with `uses` 1, `operator_warp` with `uses` 1 | solves the new set; status `Game: round 5, 41 → 48, warp 1.` |
| Warp pressed in boss rule 14 | `seq` 9, `warp` 2, goal 49 (the warp press itself raised it) | solves from goal 49 (accepted although the round is the same) |

## Out of scope

- Predicting the warp (which buttons come) or using a warp as a move in a line. The solver never presses `operator_warp`; it stays unmapped.
- (Test S6: the game disables the warp button while an operator is pending, so this can't happen.) Warp pressed while an operator is pending (right side): the left side and the pending operator are kept by `D20`, but the app solves from the left side as if no operator were pending (today's behaviour for any publish). Checked in the in-game test; a follow-up if it matters.
- Bomb mode (`StartLvl.destroing`): `SubUse` returns early but `D20` still runs. Not handled specially.

## Open questions

- Does the warp's own charge drop show in the new file? Expected yes (`SubUse` runs before `D20`).
- When `PlaceFind` returns -1 the game makes fewer buttons. The mod just reports what is active; nothing to do unless the test shows a gap.
