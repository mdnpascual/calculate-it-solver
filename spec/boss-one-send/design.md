# Design: Boss rule 13, one `=` per line

**Status:** Implemented 2026-10-25 (607 tests). Test R (in-game) R1–R4 confirmed.

## Overview

| Part | Change |
|------|--------|
| `EngineeringCalculator.cs` | `RerollOnMissRule = 13`; `OneSend(presses)`: no `=` before the last press |
| `RequiredPlan.cs` | `OneSend()`: the same plan with `OneSend` added to `Accept` and `TwoOperator = false` |
| `MainForm.cs` | rule 13 wraps the plan; status prefix and no-line text |

No change to the phases, the simulator or the mod. The mod already publishes `bossRule`, and a re-rolled goal is a new puzzle (same round, new goal), so Follow game re-solves after a re-roll.

## EngineeringCalculator

```csharp
/// <summary>Boss rule 13: an = that misses the goal re-rolls it.</summary>
public const int RerollOnMissRule = 13;

/// <summary>True when the line presses no = before its last press (boss rule 13: the only = is the one that sends).</summary>
public static bool OneSend(IReadOnlyList<Press> presses)
```

`OneSend`: for `i` in `0 .. Count − 2`, return false if `presses[i].Key == Key.Equals`. An empty line is true.

## RequiredPlan

```csharp
/// <summary>Boss rule 13: keep only lines whose only = is the last press; two-operator lines always have a middle =.</summary>
public RequiredPlan OneSend() => this with
{
    Accept = Accept == null ? EngineeringCalculator.OneSend : p => Accept(p) && EngineeringCalculator.OneSend(p),
    TwoOperator = false,
};
```

(Capture `Accept` in a local before the `with`, so the lambda doesn't read the new record's property.)

## How the phases use it

They already take `accept`:

- `ArithmeticSearch.Find(accept: plan.Accept)`: drops `op D = edits` lines before they count; edits-only and remorph lines pass.
- `TwoOperatorSearch`: not run (`plan.TwoOperator` false).
- `DivisionShortcut.Search`: has no `accept`; `MainForm`'s `onLine` callback already filters with `plan.Accept`. Only lines with no post-edits (`edits ÷ D =`, quotient = G) pass. The division cap comes from the kept arithmetic lines, as today.
- `ModuloSearch.Find(accept: searchPlan.Accept)`: modulo lines end with `=`, so they all pass.

`GoalStepSearch` (rule 14) isn't touched: rule 13 never comes with it.

## MainForm

In `StartDivisionSearch`, after `RequiredPlan.For(...)`:

```csharp
bool oneSend = searchOneSend = bossRule == EngineeringCalculator.RerollOnMissRule;
if (oneSend)
    plan = searchPlan = plan.OneSend();
```

- New field `bool searchOneSend; // boss rule 13: one = per line`.
- `DoneStatus`: with `searchOneSend`, prefix `Rule 13: one = per line: `.
- No line (in `OnSearchTick`, and after modulo if nothing was found): `No line reaches {G} with one = at the end.` The existing no-line check is `found.Count == 0 && !searchSameNumber && !searchBoss`; `searchBoss` is the rule-11 flag, so rule 13 isn't caught by it. Put the rule-13 text next to the rule-12 one.
- S == G (`searchSameNumber`) works as today.

## Tests (`CalculatorPuzzle.Tests/OneSendTests.cs`)

`OneSend`:

1. `[]`, `[X8]`, `[÷ 10 =]`, `[2X 3X mod 10 3 =]`, `[a->c 7 X8]` → true.
2. `[÷ 10 = X8]`, `[- 2 0 = + 1 =]`, `[= X8]` → false.

`RequiredPlan.OneSend`:

3. `RequiredPlan.For(null, false).OneSend()`: `TwoOperator` false, `Accept` drops `÷ 10 = X8`, keeps `÷ 10 =`.
4. `RequiredPlan.For("X8", true).OneSend()`: `Accept` needs both (`X8` alone true; `÷ 10 =` false: no X8; `÷ 10 = X8` false: middle `=`).

Phases with `accept: plan.Accept` (each line: `OneSend` true and `Run` reaches G):

5. Arithmetic, S 41, G 438, `+:9, 1:9, X8:9`: `+ 1 = X8` not among the lines.
6. Arithmetic, S 41, G 418, `X8:9, ÷:9, 10:9`: `X8` first.
7. Division through the `onLine` filter, S 41, G 4, `÷:9, 10:9`: `÷ 10 =` kept. S 41, G 48, `÷:9, 10:9, X8:9`: no line kept.
8. Modulo, S 41, G 48, `2X:9, 3X:9, mod:9, 10:9, 3:9`: `2X 3X mod 10 3 =` kept.
9. Arithmetic, S 59, G 40, `-:9, +:9`, keys `0`–`9`: `- 1 9 =` first.

The MainForm wiring (two-operator phase off, status) is checked by Test R.

## Manual test

Test R (`user-verification/test-r.md`), challenge 4 (every round is a boss) until the rule-13 text shows:

- R1: the Boss box shows 13, the status starts `Rule 13: one = per line:`, and no line has an `=` before its last press.
- R2: press a top line that ends with `op D =`: the round is won on that `=`.
- R3 (if one comes up): press a line of edits only, then `=`: the round is won, no re-roll.
- R4: press a line's middle `=` on purpose (any `op D =` that misses): the goal changes, and within a second the app shows lines for the new goal from the current display.
