# Design: the 999 999 cap and the 6-character display

**Status:** In testing (implemented 2026-10-25). Waiting for Test O (in-game).

## Overview

| Part | Change |
|------|--------|
| `EngineeringCalculator.cs` | `Max = 999_999`, `MaxDisplay = 6`, `MaxOperandTyping = 5`; `Apply` clamps `+` and `×`; `Run` drops `full` / `MaxDigits`, rejects `Xd` / `dX` at 6 characters and operand keys at 5 |
| `LeftTransforms.cs` | `Limit` goes; `X+k`, `X++`, `X*2`, `<<`, `sqr` clamp; `p`, `round` don't; preimages stay exact |
| `LeftSearch.cs` | `Append` / `Prepend` moves only below 6 characters; `MaxDigits` 18 → 8 |
| `LeftEdits.cs` | extend moves only while the piece is shorter than 6 |
| `DivisionShortcut.cs` | `TypeOperand` and `CheapestDivisor` follow the operand rule; Path A r ≤ 3; skip `built` above 6 characters |
| `ModuloSearch.cs` | stop the k loop when X can't fit in 6 characters |
| `TwoOperatorSearch.cs` | `Limit` goes; operands follow the operand rule; `×` backward bound uses `Max` |
| Tests | the ones that use 10^18 or 7+ digit displays change (list below) |

No mod or UI change. `MainForm` never shows `MaxDigits` itself.

## EngineeringCalculator

```csharp
/// <summary>The game's biggest result: +, ×, X+k, X*2, <<, sqr clamp to it; p, round, % don't.</summary>
public const long Max = 999_999;

/// <summary>Xd and dX do nothing once the display text has this many characters (leading zeros count).</summary>
public const int MaxDisplay = 6;

/// <summary>An operand key appends only while the operand has fewer characters than this; after that the game restarts the operand.</summary>
public const int MaxOperandTyping = 5;
```

- Remove `Limit`, `MaxDigits` and the `full` local in `Run` (and every `log.Add(MaxDigits)` branch).
- `Apply`: `Key.Add => Math.Min(left + d, Max)`, `Key.Multiply => d == 0 ? 0 : left > Max / d ? Max : Math.Min(left * d, Max)`. `%` keeps its overflow-safe form (`left ≤ long.MaxValue / d`) and the 2^24 rule. `Apply` no longer returns null for size; keep the nullable return for `÷` / `mod` by 0 (`invalid`).
- `Run`, `Key.Prepend` / `Key.Append`: add `&& display.Length < MaxDisplay` to the `when`. Otherwise the press falls to `default` and the line is rejected.
- `Run`, operand keys: add `&& (operand.Length == 0 || operand.Length < MaxOperandTyping)` (the first key always starts the operand). Remorph keys: their one key is the first, so no change.
- Remorph result: `morphed` is at most `left / 10 * 10 + 99`; no size check needed (remove the `Limit` one).
- Transforms: `t.Apply` returns non-null for every input now (see below), so the `MaxDigits` branch goes.
- Doc comments: replace "10^18 or more → max digits reached" with the clamp rule.

## LeftTransforms

- Remove `Limit`. Transforms that clamp: `X+k` / `X++` `v => Math.Min(v + k, Max)`, `X*2` and `<<` `Math.Min(2v, Max)`, `sqr` `v > 999 ? Max : v * v` (999² = 998 001 fits; 1 000² is above `Max`, and the check also avoids overflow).
- No clamp: `p` (`Primes.Nearest(v)`), `round`. Their input is at most a few million in practice; keep them returning a value.
- `Apply` stays `string → string?`; null now only for a display that isn't a number (it can't happen after the change, but keep the type).
- Preimages: unchanged math, but every bound `< Limit` becomes `≤ Max` for the clamped transforms (a value above `Max` can't be the result of a clamped transform) and stays a sanity bound (say `≤ 10^8`, the largest `%` result 2^24 rounded up) for the others. Preimages of exactly `Max` don't list the clamp range (`X*2` from 500 000 … 999 999): requirements item 6.
- The class comment and `HundredUp` (`v < Limit` → `v ≤ 10^8`, it is lazy) follow.

## LeftSearch

- Forward moves `Append` / `Prepend`: only when `w.Length < MaxDisplay`.
- Backward moves `Append` / `Prepend` (undo): only when the result `w` has at most `MaxDisplay` characters (the display before was shorter than 6).
- `DefaultLimits`: `MaxDigits = min(8, max(len S, len G) + 3)` (8 because `%` can give up to 2^24 = 16 777 216; `p` and `round` give 7).

## LeftEdits

`Plan` builds `to` from a piece of it. Extend moves (`Xd`, `dX`) need the current piece shorter than `MaxDisplay`: add `s.J - s.I < MaxDisplay` to both. With `to` longer than 6 characters and a piece that has to grow, there is no plan (null), as the game can't type it either.

## DivisionShortcut

- `TypeOperand`: in the DP, a key may be pressed at position `i` only when `i == 0 || i < MaxOperandTyping` (so: `i < 5`). That gives at most 5 characters, or 6 when the last key types two (`10`, a two-digit charge value).
- `CheapestDivisor`: candidate D of more than 6 digits are skipped (its typing goes through `TypeOperand` or the same rule; check where it builds `path`).
- Path A: `r ≤ Math.Min(maxPresses - 1, 3)` (`101010` is the longest).
- Path B and Path A: skip `built` (X) with more than `MaxDisplay` characters before calling `Plan`. Prefixes and suffixes are short, so this is a cheap length check.
- `Search` input check: no change (S and G as given).

## ModuloSearch

`X = (M + L) ‖ padding ‖ S` is built with `dX` / `Xd`. The k loop already has `LowerBound(k) <= maxCost`; add `k + 1 <= MaxDisplay` (at least one prepended digit). Lines whose prepend is longer are rejected by the simulator anyway; this just stops the loop early. `ModuloShortcut` doesn't change.

## TwoOperatorSearch

- Remove `Limit`. `middle >= Limit` goes (Apply clamps); `middle == x` stays (a clamp that leaves 999 999 at 999 999 is skipped).
- Operand texts (line ~81, `text.Length > 17`): use the operand rule (at most 6 characters, and a key pressed at 5 or more characters is not allowed). Operands come from `TypeOperand`-like key lists, so apply the same `i < 5` rule when building them.
- Backward `×` bound (line ~347, `y <= (Limit - 1) / d`): `y <= Max / d`. Preimages of `Max` through a clamp are not listed (requirements item 6).

## The rule-12 spec

`spec/boss-send-plus-one/` (not implemented yet) adds 1 after the operation. With this change the order is: operation, clamp, then +1, so 999 999 + 1 = 1 000 000 is allowed. Whichever spec is implemented second adjusts to the other (its design already says so).

## Tests

Existing tests that change (find the rest with `dotnet test` after step 1; each one moves to the game rule, list them in an "As built" note here):

- `ArithmeticTests.cs` ~46–47: `999999999999 × 9999999 =` → expect a clamp instead. Rewrite: `999000`, `+ 5 0 0 0 =` ends at `999999`, and the same presses followed by `X1` → null (6 characters).
- `LeftTransformTests.cs` ~35 (`X++` on `999999999999999999` → null): becomes `X++` on `999999` → `999999`.
- `LeftTransformTests.cs` ~155–161 (`sqr` near the limit, `max digits reached`): `999` `sqr` → `998001`; `1000` `sqr` → `999999`, and a press after it works.
- `MoreButtonsTests.cs` ~205 (`%` above 2^24): unchanged rule, but S `999_999` stays valid.
- `PrimeTests.cs` (`999999 → 1000003`): unchanged (no clamp on `p`).
- Any test with a 7+ digit display built by `Xd` / `dX` (Modulo k ≥ 6 lines, long division lines) and any operand longer than the rule.

New tests (`CalculatorPuzzle.Tests/DisplayCapTests.cs`):

1. `Apply`: `999000 + 5000` = 999 999; `600 × 2` = 1 200 → no clamp; `1000 × 1000` = 999 999; `-` and `÷` unchanged.
2. `Run`: `600000` `X*2` → `999999`; then `X--` → `999998` (play goes on).
3. `Run`: `1000` `sqr` → `999999`; `999` `sqr` → `998001`.
4. `Run`: `999999` `p` → `1000003`; `999995` `round` → `1000000`.
5. `Run`: `12345` `X6` → `123456`; `123456` `X7` → null; `123456` `7X` → null; `000041`-style leading zeros count (`0` `X0 X0 X0 X0 X0` = `000000`, then `X1` → null).
6. `Run`: `41` `÷ 10 10 10 =` → `0` (101010); `÷ 10 10 10 10 =` → null; `÷ 9 9 9 9 9 =` → `0`; `÷ 9 9 9 9 9 9 =` → null.
7. `TypeOperand("101010")` = three `10`; `TypeOperand("1010101")` = null; `TypeOperand("99999")` = five `9`; `TypeOperand("999999")` = null; with `10` and `9`: `TypeOperand("999910")` = `9 9 9 9 10`.
8. `LeftEdits.Plan("41", "1234567", …)` = null; `Plan("41", "123441", …)` works (6 characters).
9. `LeftSearch.Forward` from `12345` with `X6`, `X7`: reaches `123456`, never `1234567`.
10. Division Path A never uses r = 4; division and modulo lines never have a display above 6 characters built by `Xd` / `dX` (check every reported line's log on a few puzzles, for example 41 → 483 with every button).
11. Two-operator: no operand of more than 6 characters in any line.

## Manual test

Test O (`user-verification/test-o.md`), any round with the buttons at hand:

- O1: a `+` or `×` (or `X*2`, `sqr`) press that goes above 999 999: the display shows `999999` and the next press works.
- O2: on a 6-character display, `Xd` does nothing.
- O3: after an operator, type 6 digit keys: the sixth restarts the operand.
- O4 (if `p` is owned): `p` on `999999` shows `1000003`.
- O5: the app on a puzzle that used to list a 7-digit line (for example a Modulo line with k = 6) no longer lists it.

## As built (2026-10-25)

- Code as designed. Two small differences: the `Run` operand-key rule is `operand.Length < MaxOperandTyping` (a remorph key always sees an empty operand, so no special case), and the two-operator `÷` backward bound is `y <= table.MaxKey / d` (exact, instead of `Max / d`, because `%`, `p` and `round` can put M above 999 999 in the table). No explicit `built` length check in division: `LeftEdits.Plan` already returns null above 6 characters, and the plan cache makes the check redundant.
- Existing tests that changed: `ArithmeticTests` (overflow test: `999000 + 5000 =` → `999999`, then `X1` → null), `LeftTransformTests` (`sqr` `1000` → `999999`, `999` → `998001`, `X++` on `999999` stays; the near-the-limit and over-the-limit tests), `LeftEditsTests` (`41` → `5485411` became `548541`, 6 characters). Nothing else failed.
- New tests: `DisplayCapTests.cs`, 12 tests (design tests 1–11; test 10 has two). Total 560 (547 before), all pass.
- Time, 41 → 483 with every button (`0X`–`9X`, `X0`–`X9`, `0`–`9`, `10`, `reset`, `÷`, `mod`, `flip`, `+`, `-`, `×`, 9 charges each), Release, caps as in the app (arithmetic 12, then +2 for each later phase): 0.85–0.99 s before, 0.77 s after. With the division cap raised to 7 and modulo to 8: 1.17 s before, 0.22 s after (2 944 division and 100 modulo lines both times).
- Checked by test, not by starting the WinForms app: every division, modulo and two-operator line for 41 → 483 with every button has no `Xd` / `dX` or operand result above 6 characters and no fourth `10`.
