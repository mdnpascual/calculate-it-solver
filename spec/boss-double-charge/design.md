# Design: Boss rule 6, each press costs 2 charges

**Status:** Done 2026-10-25 (Test Q1–Q3 confirmed; Q4 not run).

## Overview

| Part | Change |
|------|--------|
| `EngineeringCalculator.cs` (`Buttons`) | new `ChargeStep` (1, or 2 in rule 6), `ChargeValue(used)`, `TakeTwo()`; `Run` uses `ChargeValue` |
| `DivisionShortcut.cs` | `ChargeValue` and `TypeOperand` use the step |
| `TwoOperatorSearch.cs` | operand typing uses `ChargeValue` |
| `MainForm.cs` | Boss 6 → `buttons.TakeTwo()`; status prefix; rule 6 out of "not supported" |

No mod change, no `GameLink` change. `Fits`, `Drained`, `Spare`, `ResultLine` and `Ranking` don't change: they read `Charges`, and in rule 6 `Charges` holds presses (ceil(n / 2)) instead of charges.

## Buttons

New constant beside `SendPlusOneRule`: `EngineeringCalculator.TakeTwoRule = 6`.

New primary parameter `int ChargeStep = 1` (last, so existing calls compile).

```csharp
/// <summary>The charge key's value on its (used+1)-th press in one line, or null when it would be below 1.</summary>
public int? ChargeValue(int used) =>
    ChargeStart is { } start && start - ChargeStep * used >= 1 ? start - ChargeStep * used : null;

/// <summary>
/// Boss rule 6: each press costs 2 charges (down to 0), so n charges give ceil(n / 2) presses.
/// Glass buttons keep their one press. The charge key types start, start - 2, ...
/// </summary>
public Buttons TakeTwo();
```

`TakeTwo` builds a **new** `Buttons` with the constructor, passing every parameter through, except:

- `ChargeMap`: for each entry, glass keys (`Glass`) keep their value, the others get `(n + 1) / 2`.
- `ChargeStep: 2`.
- `GlassKeys: Glass`, `TransformList: Transforms` (pass the current sets on).

Don't use `this with { ChargeMap = … }`: `Charges`, `Glass` and `Transforms` are get-only properties set from the primary parameters when the object is built, and a `with` copy keeps the old values. (Either build with the constructor, or turn those three into computed properties; the constructor is the smaller change.)

Calling `TakeTwo` twice would halve twice; `MainForm` calls it once per search on freshly parsed buttons.

## Charge key call sites

Every place that computes "start minus presses so far" switches to `buttons.ChargeValue(used)`:

| File | Today | After |
|------|-------|-------|
| `EngineeringCalculator.Run`, `TypedText` | `press.Digit == start - chargeUsed` | `buttons.ChargeValue(chargeUsed) == press.Digit` (keep the `buttons == null` branch: any digit ≥ 1) |
| `DivisionShortcut.ChargeValue` | `used >= start` → null; `start - used` | `buttons.ChargeValue(used)`, plus today's charge-limit check |
| `DivisionShortcut.TypeOperand` | `maxCharge = min(cs, limit)` | `maxCharge = min(ceil(cs / ChargeStep), limit)` (the number of presses whose value is ≥ 1) |
| `TwoOperatorSearch` `Next` | `c - charges >= 1` → `new Press(Key.Charge, c - charges)` | `buttons.ChargeValue(charges) is { } v` → `new Press(Key.Charge, v)` |
| `ArithmeticSearch.RemorphKeys` | first press, value `start` | unchanged (`ChargeValue(0) == start`) |

`OperandWidth` is unchanged (the first press is the widest). Grep for `ChargeStart` after the change: every remaining use should be the start value, a `!= null` check or `ChargeValue`.

## MainForm

In `StartDivisionSearch`, right after `Buttons.Parse` succeeds:

```csharp
// Rule 6: every press costs 2 charges, so the search counts presses (ceil(n / 2)), not charges.
if (bossRule == EngineeringCalculator.TakeTwoRule)
    buttons = buttons.TakeTwo();
```

Before the Required check, so "isn't in Buttons" still works (ceil(n / 2) > 0 exactly when n > 0).

- `searchTakeTwo` field (like `searchPlusOne`); `DoneStatus` / the status code at the `Rule 12:` prefix does not get a rule 6 prefix (dropped after Test Q1). The "no line" text: `No line fits the available buttons with 2 charges per press.`
- `if (bossRule is 2 or 6 or 9)` → drop 6 (`is 2 or 9`, or remove the line if `boss-blocked-buttons/` is done first).

## Tests (`CalculatorPuzzle.Tests/TakeTwoTests.cs`)

1. `TakeTwo` charges: `X8:1` → 1, `X8:2` → 1, `X8:3` → 2, `X8:4` → 2; glass `X8:1g` → 1; `=` not counted; `:0` buttons are still absent.
2. `TakeTwo` keeps the rest: `Glass`, `Transforms`, `ChargeStart`, `QuestDigit`, `Ten`, digit keys, operators are equal to the original's.
3. `Fits`: `X1 8X ÷ 10 10 = 4X` with `X1:2, 8X:2, 4X:2, ÷:2, 10:2` fits normally, not after `TakeTwo`; with `10:3` it fits after `TakeTwo`.
4. `Drained` after `TakeTwo`: `10:3` pressed twice → drained.
5. `ChargeValue`: start 12, step 2 → 12, 10, …, 2, then null at `used = 6`; start 13 → 13, …, 1, null at 7; step 1 unchanged (`more-buttons` tests still pass).
6. Simulator: `÷ c12 c10 =` on 8411 with `c12:12` after `TakeTwo` runs (operand 1210, 8411 ÷ 1210 = 6); `÷ c12 c11 =` is rejected after `TakeTwo` and accepted without it.
7. `DivisionShortcut.TypeOperand("1210", b.TakeTwo())` = `c12 c10`; `TypeOperand("1211", …)` = null (with no digit keys).
8. A search end to end: `DivisionShortcut.Solve` / `ArithmeticSearch.Find` with `TakeTwo` buttons never reports a line that fails `Fits` and `Run` on the same buttons.

## Manual test

Test Q (`user-verification/test-q.md`), challenge 4 until the text reads `Each button press consumes 2 charges`:

- Q1: the Boss box shows 6, no `not supported` note.
- Q2: press any button with an odd charge count (for example 3). Expected: 3 → 1 → 0, and the button can be pressed at 1 charge.
- Q3: press the top line. Expected: it never runs out of charges and wins the round.
- Q4 (if the charge key is there): after an operator, press it twice. Write down the operand shown (expected `1210` at 12 charges; `108` would mean the charge drops before typing — then the open question is answered the other way and `ChargeValue` must start at `start - 2`, in normal rounds too: `start - 1`).
