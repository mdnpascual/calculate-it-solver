# Design: blind passive in boss rule 14 (bugfix)

**Status:** Complete 2026-10-28.

Read `requirements.md` first.

## Overview

| Part | Change |
|------|--------|
| Mod (`GameHook.MelonLoader/CalcHookCore.cs`) | `blind` in `puzzle.json` / `state.json`; republish when `passive_blind` is picked |
| `GameLink.cs` | `GamePuzzle.Blind` |
| `EngineeringCalculator.cs` | `LineContext.Blind`; the simulator skips the goal raise (and the rule-10 coin) on the first non-glass press |
| `GoalStepSearch.cs` | `Raises`; target G + k − 1 with Blind |
| `SwapSearch.cs` | `FlippedStart`, `Flip`, `Check` use the free press |
| `MainForm.cs` | Blind checkbox, passed into `LineContext` |

The phase searches (`ArithmeticSearch`, `TwoOperatorSearch`, `DivisionShortcut`, `ModuloSearch`) don't change. Only the target and the `accept` filter change, as for Near 1.

## Mod

Read-only: one `GamePrefs.GetInt`, one `GameObject.Find`. Both are already in `GameStubs/Game.cs` (`GamePrefs.GetInt(string, int)`, `UnityEngine.GameObject.Find`), so no stub change.

- `static bool Blind() => GamePrefs.GetInt("blind") == 1;` (the flag: a free press is waiting).
- `static bool BlindOwned() => GameObject.Find("passive_blind") != null;` (the passive is owned).
- `CheckPuzzle`: republish on a change of **`BlindOwned()`**, not of `Blind()`. The flag drops to 0 on the round's first press, and that must not republish (it would restart the app's search in the middle of a line). Same pattern as `near1`: `_publishedBlindOwned`, a `|blind:` part in the fingerprint (using `BlindOwned()`).
- `WritePuzzle` and the `state.json` writer: `"blind": true|false` from `Blind()`, next to `near1`. Log line: `, blind` when true.
- `GameHook.MelonLoader/README.md`: the field.

Timing: a rule-14 round publishes once, after the settle. If the player presses before the publish, the file already has `blind` false and a raised goal. That is the same limit rule 14 has today.

## App

### `GameLink.cs`

`GamePuzzle` gets `bool Blind = false` (last parameter). `Parse`: `root.TryGetProperty("blind", out var b) && b.ValueKind == JsonValueKind.True`. Older mod files give false.

### `EngineeringCalculator.cs`

- `LineContext` gets `bool Blind = false` (last parameter). Doc: the round's first non-glass press is free (`passive_blind`).
- `Run`, in the charged-press block: keep a `bool freeLeft = context?.Blind == true`. For a non-`=` press that is not glass and `freeLeft`: set `freeLeft = false`, don't add to `goalRaises`, and skip the rule-10 coin. The `DrainFromThird` drain and `charged++` stay (the game still counts `press_count` there). Glass presses are as today.

### `GoalStepSearch.cs`

```csharp
/// <summary>How much the goal rises over a line: its charged presses, less the free one with the blind passive.</summary>
public static int Raises(IReadOnlyList<Press> presses, Buttons buttons)
{
    int k = Charged(presses, buttons);
    return buttons.Context?.Blind == true && k > 0 ? k - 1 : k;
}
```

In `Search`: `int free = baseButtons.Context?.Blind == true ? 1 : 0;`, then for each k use `goal + k - free` where it now uses `goal + k` (`WithGoal` and `NearGoals.Targets`). `accept` still checks `Charged(p, buttons) == k`. Update the class summary (`goal + k`, `− 1 with the blind passive`).

### `SwapSearch.cs`

- `FlippedStart`: the rule-14 `+ 1` applies only when swap isn't glass **and** Blind is off (with Blind, the swap is the free press).
- `Flip(buttons, newGoal)`: if swap isn't glass, the flipped leg's context gets `Blind = false` (the swap used it). If swap is glass, Blind stays for the rest.
- `Check`: `target = start + Raises(rest, flipped)` in rule 14, in place of `Charged`.

### `MainForm.cs`

- `readonly CheckBox blindBox = new() { Text = "Blind", AutoSize = true };` in `followPanel` after `near1Box`. `CheckedChanged` calls `MarkDivisionStale()`.
- `ApplyGamePuzzle`: `blindBox.Checked = puzzle.Blind;`.
- Where the `LineContext` is built: `Blind: blindBox.Checked`.
- The same-round rule-14 guard in `OnGameFileChanged` is unchanged (`blind` can't change mid-round: the picker is between rounds).
- No status change and no line tag (requirement 6).

Outside rule 14 the checkbox changes only the rule-10 coin in the simulator (`goalRaises` is read only with `GoalStep`). The free charge is a follow-up.

## Tests (`CalculatorPuzzle.Tests/BlindTests.cs`)

1. `Run` with `LineContext(46, GoalStep: true, Blind: true)`: `near` as the first press reads 46; as the second press (after `X++`) it reads 47.
2. Glass first, then a normal press: the glass press doesn't use the free press; the normal one does (goal after both = G).
3. `Raises`: 3 charged → 2 with Blind; 0 charged → 0.
4. `GoalStepSearch.Search` S 41, G 46, `÷:9, 10:9, X8:9, =`, Blind: finds `÷ 10 = X8` with `Reaches` 48. Without Blind it doesn't (it finds it at G 45).
5. S 47, G 48, `X++:1, =`, Blind: the 1-press line `X++`, `Reaches` 48.
6. Glass `X8:1g`: S 41, G 47 → `÷ 10 = X8`, `Reaches` 48.
7. Swap: `FlippedStart` with Blind = G; a glass swap with Blind = G + 1 and the flipped leg keeps Blind. `Check` on a flipped line uses `Raises`.
8. `GameLink.Parse`: `blind` true / false / absent.
9. Existing `GoalStepTests` and `SwapTests` pass unchanged (Blind off).

## Manual test: Test AB

`user-verification/test-ab.md` (take the next free letter if AB is used):

- AB1: own `passive_blind`; at the start of a round `puzzle.json` has `"blind": true`; after the first press `state.json` has `false`; `puzzle.json` is not republished.
- AB2: pick the passive at a picker: the puzzle is republished and the app ticks Blind.
- AB3 (rule 14 with the passive; challenge 4 helps): play the top line; the round is won, and the goal shown after the first press is still G.
- AB4 (rule 14, with a glass button pressed first, if met): the goal stays the same on the glass press and on the next press.
