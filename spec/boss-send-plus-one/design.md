# Design: Boss rule 12, every `=` adds 1

**Status:** Complete (2026-10-25). Test N: N1–N3 confirmed in game, N4 skipped.

## Overview

| Part | Change |
|------|--------|
| `EngineeringCalculator.cs` | `Run(..., bool plusOne = false)`: each `=` adds 1; a bare `=` as the last press. New `Sent`, `SendPlusOneRule = 12` |
| `ArithmeticSearch.cs` | `plusOne` flag: target G − 1, mid-line `=` results 1 lower, the bare-`=` line, check with `Sent` |
| `TwoOperatorSearch.cs` | `plusOne` flag: M is the first result + 1, op2 aims at Y2 − b, check with `Sent` |
| `DivisionShortcut.cs` | `plusOne` flag: quotient pieces of G − 1, divide for q − b, check with `Sent` |
| `ModuloSearch.cs` | `plusOne` flag: solve for G − 1, check with `Sent` |
| `MainForm.cs` | rule 12 passes `plusOne` to the four phases; status; `not supported` note without 12 |

No mod change: the mod already publishes `bossRule`, and rule 12 doesn't move the goal, so the normal one-solve-per-puzzle rule fits.

## Terms

- `plusOne`: boss rule 12 is on.
- `t = goal − 1` with `plusOne`, else `goal`: the display just before the send. With `plusOne` and `goal < 1`, every phase returns no lines (a sent number is at least 1).
- `b = plusOne && post.Count > 0 ? 1 : 0`, for a segment `op D =` followed by the edits `post`: the operation must give `y − b`, where `y` is the display the edits start from. When `post` is empty the `=` is the send itself, so the operation gives `t` (b = 0). `a->c` / `b->c` have no `=`, so they never get the +1.

## EngineeringCalculator

```csharp
/// <summary>Boss rule 12: every = adds 1 to its result.</summary>
public const int SendPlusOneRule = 12;

public static IReadOnlyList<string>? Run(long start, IReadOnlyList<Press> presses, Buttons? buttons = null, bool plusOne = false)

/// <summary>
/// The number a line sends: the last display, plus 1 with plusOne when the line doesn't end with = (the user presses
/// one more = to send). Null when Run fails or ends on max digits.
/// </summary>
public static long? Sent(long start, IReadOnlyList<Press> presses, Buttons? buttons = null, bool plusOne = false)
```

`Run`:

- `foreach` becomes a `for` loop (the bare `=` needs to know it is last).
- `case Key.Equals when op != null && operand.Length > 0`: after `Apply`, with `plusOne` the result gets `+ 1` before the `Limit` check (`result + 1 >= Limit` → max digits, as today). Once [`display-cap/`](../display-cap/) is in: the +1 comes after the 999 999 clamp with no size check (999 999 + 1 = 1 000 000).
- New `case Key.Equals when op == null && plusOne && i == presses.Count - 1`: `display = (long.Parse(display) + 1).ToString()` (the game reads the number, so leading zeros drop: `04` → `5`), logged. Only reachable as the last press; without `plusOne` a bare `=` is rejected as today.
- Remorph keys (`a->c`, `b->c`) don't change: no `=`.

`Sent`: `Run`; null → null. Value = `start` when the log is empty, else the last log entry (null if it is `MaxDigits`). With `plusOne` and a last press that isn't `=`, add 1.

`Cost` doesn't change: `=` is free, so the bare-`=` line costs 0.

## Phases

Each phase gets a last optional parameter `bool plusOne = false`. Without it nothing changes (all existing tests pass as they are). With it:

### ArithmeticSearch.Find

- The goal string `g` (used by `BuildYs`, `LeftSearch.Backward`, `LeftSearch.Join`, `Plan(s, g)`) becomes `t.ToString()`.
- `start == t` with `plusOne`: return the single line `[=]` (op null, cost 0) when `accept` passes, through `Keep` (so it is checked like the others). Keep the edits-only search too: there is none cheaper, so just return.
- Per `(x, y, post)`: `long want = y - b` (`b` from `post`); skip `want < 0`. The `+`, `-`, `×` operand is `want − x`, `x − want`, `want / x`; `%` uses `want` in the range formula (`ceil(100·want / x) ≤ D ≤ floor((100·want + 99) / x)`).
- Remorph lines: unchanged (no `=`; the send `=` comes after `post`).
- `Keep`: the check becomes `EngineeringCalculator.Sent(start, presses, buttons, plusOne) == goal` (`goal` is the real G). `Line.Result` stays `y`, the display after the `=`.

### TwoOperatorSearch.Find

- `g` and `ys` come from `t`.
- Forward half: `middle = Apply(op, x, d) + (plusOne ? 1 : 0)` for every operator with `=` (not remorph). `middle >= Limit` and `middle == x` are still skipped.
- Backward half: where it asks which `M` give `Y2` with `op2 D2 =`, it asks for `Y2 − b` (b from that `ys` entry's `Post`); skip when `Y2 − b < 0`. Remorph op2: unchanged.
- `Keep`: check with `Sent(..., plusOne) == goal`.
- The rule "operands that change nothing are skipped" (`+ 0`, `× 1`, …) stays, even though with `plusOne` they would add 1 (out of scope).

### DivisionShortcut.Search / Solve

- `g` (for `Pieces` and the `Plan(qText, g)` post-edits) becomes `t.ToString()`.
- Per quotient piece `q` with its `post`: `long qd = q - b`. Path B uses `qd` in `low = x / (qd + 1) + 1, high = x / qd` and needs `qd ≥ 1`. Path A uses `PrefixCandidates(qd, d, …)` and needs `qd ≥ 0`.
- `AddLine`: check with `Sent(..., plusOne) == goal`. `Line.Q` stays `q` (what the display shows after the `=`).
- The `start == goal` early return becomes `start == t` (the bare-`=` line comes from the arithmetic phase).

### ModuloSearch.Find

Modulo lines always end with `mod … =`, so the `=` is the send: call `ModuloShortcut.Solve(start, t, a, k)` and check with `Sent(..., plusOne) == goal`. `start == t` returns no lines.

### What doesn't change

`LeftEdits`, `LeftSearch` (they never press `=`; they are called with `t`), `GoalStepSearch` (rule 14 never meets rule 12), `RequiredPlan`, `ResultLine`, `Ranking`, charges.

## MainForm

- `bool plusOne = bossRule == EngineeringCalculator.SendPlusOneRule;` in `StartDivisionSearch`, kept in a field `searchPlusOne` for the modulo task that starts later.
- Pass `plusOne` to the four phase calls (arithmetic, two-operator, division, and the modulo `Task.Run` in the tick).
- The note: `bossRule is 2 or 6 or 9` (12 removed).
- Done status: prefix `Rule 12: every = adds 1: ` to the usual text. No line: `No line sends {G} with every = adding 1.`
- The S == G message (`searchSameNumber`) doesn't apply with `plusOne` (S = G is a normal search for G − 1; S = G − 1 gives the `=` line).

## Tests (`CalculatorPuzzle.Tests/SendPlusOneTests.cs`)

Simulator:

1. `Run(41, ÷ 10 = X8, plusOne)` ends at `58`; `Sent` = 59. Without `plusOne`: ends at `48`, `Sent` = 48.
2. `Sent(41, + 7 =, plusOne)` = 49 (the last `=` is the send; no extra +1).
3. `Sent(47, [=], plusOne)` = 48; `Run(47, [=])` without `plusOne` → null; `Run(47, [= X8], plusOne)` → null (bare `=` only as the last press).
4. `Run(59, - 6 0 =, plusOne)` gives `1` (`-` stops at 0, then +1).
5. `a->c 7` on 483 with `plusOne`: `Sent` = 74 (73 + the send).

Phases (each reported line: `Sent(start, presses, buttons, true) == G`, and it fits the charges):

6. Arithmetic, S 41, G 48, `+:9`, keys `0`–`9`: first line `+ 6 =`; `+ 7 =` is not among the lines.
7. Arithmetic, S 41, G 439, `+:9, 1:9, X8:9, =`: `+ 1 = X8` is among the lines.
8. Arithmetic, S 47, G 48: the `=` line, cost 0, first.
9. Arithmetic, S 59, G 40, `-:9`, keys `0`–`9`: `- 2 0 =` first; `- 1 9 =` not among the lines.
10. Division, S 41, G 59, `÷:9, 10:9, X8:9, =`: `÷ 10 = X8`. G 48 with the same buttons: `÷ 10 = X8` not reported.
11. Division, S 41, G 5, `÷:9, 10:9, =`: `÷ 10 =`.
12. Modulo, S 41, G 49, `2X:9, 3X:9, mod:9, 10:9, 3:9, =`: `2X 3X mod 10 3 =`.
13. Two-operator, S 59, buttons `-:9, +:9`, keys `0`–`9`, G 42: `- 2 0 = + 1 =` is among the lines (39 + 1 = 40, 41 + 1 = 42).
14. `plusOne` with G 0: every phase returns no lines and doesn't throw.
15. Every phase with `plusOne: false` gives the same lines as before (covered by the existing tests).

The MainForm wiring is checked by Test N.

## Manual test

Test N (`user-verification/test-n.md`), challenge 4 (every round is a boss) until the rule-12 text shows:

- N1: the Boss box shows 12, the status starts `Rule 12: every = adds 1:`, no `not supported` note.
- N2: press a top line that has an `=` in the middle and ends with edits, then `=`: the round is won (mid `=` +1, send +1).
- N3: press a top line that ends with `op D =`: the round is won on that `=`, with no extra press.
- N4 (if it comes up, or set up by hand with S = G − 1): pressing `=` first wins.
- N5 (follow-up info): after an `Xd` press, press `=`: does the display go up by 1, and can the next press keep editing? Write down what happens.
