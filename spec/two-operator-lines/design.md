# Design: Two-operator lines

**Status:** Complete (2026-10-24). Follow-up: after Test M2 passes, remove `MainForm.BossTestMarker`.

**Corrected 2026-10-25:** the 10^18 `Limit` doesn't match the game, which clamps `+` and `×` at 999 999 and keeps going. See [`display-cap/`](../display-cap/).

## Where the code goes

| Part | File | Change |
|------|------|--------|
| Search | `CalculatorPuzzle/TwoOperatorSearch.cs` (new) | `TwoOperatorSearch.Find` and `Line` |
| Row | `CalculatorPuzzle/ResultLine.cs` | `TwoOperator` field, `From(TwoOperatorSearch.Line, …)`, kind order, `TwoOperatorMarker` in `ToString` |
| Plan | `CalculatorPuzzle/RequiredPlan.cs` | `TwoOperator` flag, `RequiredOp` (the operator a pair must contain) |
| UI | `CalculatorPuzzle/MainForm.cs` | run the phase after arithmetic in the search task, division cap from both, `twoOperatorCount`, `DoneStatus` |
| Rule 14 | `CalculatorPuzzle/GoalStepSearch.cs` | run the phase per k; `Result` gets `TwoOperator` |
| Docs | `CalculatorPuzzle/README.md` | file note, "The tricks" section 4, "Search order" |
| Tests | `CalculatorPuzzle.Tests/TwoOperatorSearchTests.cs` (new), `RequiredPlanTests.cs`, `ResultLineTests` (or where `ToString` is tested) | see Test plan |

## Types

```csharp
public static class TwoOperatorSearch
{
    public const int MaxLines = 100;
    public const int MaxOperandPresses = 3;   // per segment
    public const int MaxMiddle = 200_000;     // distinct M values kept from the forward half

    /// <summary>Op1 D1 gives Middle; Op2 D2 gives Result (before the post edits).</summary>
    public record Line(Key Op1, long Operand1, long Middle, Key Op2, long Operand2, long Result,
                       IReadOnlyList<Press> Presses, int Cost, int Order);

    public static IReadOnlyList<Line> Find(long start, long goal, Buttons buttons, int maxCost,
                                           CancellationToken cancel = default, int maxLines = MaxLines,
                                           Func<IReadOnlyList<Press>, bool>? accept = null,
                                           Key? requiredOp = null);
}
```

`requiredOp` non-null: only pairs with `Op1 == requiredOp || Op2 == requiredOp` are built (rule 7). `accept` still filters (it is the same `RequiredPlan.Accept`).

`RequiredPlan` gets `bool TwoOperator` and `Key? RequiredOp`:

```csharp
null (left-side button) => new(accept, null, Arithmetic: true, Division: true, Modulo: true, TwoOperator: true, RequiredOp: null),
Key.Divide => new(accept, null, false, true,  false, true, Key.Divide),
Key.Mod    => new(accept, null, false, false, true,  true, Key.Mod),
_          => new(accept, [op], true,  false, false, true, op),
// no required / not boss: everything on, RequiredOp null
```

Fix the class comment: lines no longer have exactly one operator.

## Algorithm: meet in the middle on M

`M` is the value on the display after the first segment (a number, since `=` and remorph results read as numbers).

### Operand list

`Operands(buttons)`: every operand press sequence of 1..`MaxOperandPresses` operand keys (from `ArithmeticSearch.RemorphKeys`: digits, `10`, `c`, `q`), mapped to the number it types, keeping the cheapest sequence per number (ties by press text). `c` changes value per press and `q` can be pressed once per line: build the sequences through `DivisionShortcut.TypeOperand` rules, or simply build candidates and let the simulator decide (it already models `c` and `q`). Up to 13³ ≈ 2 200 sequences; cheap.

Skip no-op operands per operator: `+ 0`, `- 0`, `× 1`, `÷ 1`. (`× 0` stays: it reaches 0.)

### Forward half: S → M

1. `xs`: the X1 values and their pre-edits, built exactly as `ArithmeticSearch.Find` builds `xs` (cores, suffixes, prefixes, `LeftEdits.Plan`, plus `LeftSearch.Forward` displays when the puzzle has transforms). Factor that block into a shared `internal static` helper in `ArithmeticSearch` (`BuildXs`) rather than copying it; `ArithmeticSearch.Find` calls it too.
2. For each X1, each `op1` the buttons have, each operand: compute M with the simulator's own operator function (`EngineeringCalculator` has the `÷`/`mod`/`+`/`-`/`×`/`%` arithmetic; expose it as `internal static long? Apply(Key op, long left, long operand)` if it isn't already). Remorph: M from the formula.
3. Keep `F[M]` = the cheapest (pre + op1 + operand + `=`) presses, ties by press text, while `cost + 2 ≤ maxCost` (the second segment needs at least 2 presses). Stop adding new M at `MaxMiddle`; process X1 cheapest first so truncation drops dear ones.
4. With `requiredOp`, keep two maps: `Freq` (op1 == requiredOp) and `Fany` (all). A pair then needs `Freq × Ball` or `Fany × Breq`.

### Backward half: M → G

1. `ys`: the Y2 values and their post-edits, as `ArithmeticSearch` builds `ys` (`DivisionShortcut.Pieces(g)`, `LeftEdits.Plan`, plus `LeftSearch.Backward` with transforms). Shared helper `BuildYs`.
2. For each Y2, each `op2`, each operand D2, the set of M that give Y2:

| op2 | M that gives Y2 | Lookup |
|-----|-----------------|--------|
| `+` | `Y2 − D2` | point |
| `-` | `Y2 + D2`; if Y2 = 0 also every M in `[0, D2]` | point (+ range) |
| `×` | `Y2 / D2` when D2 divides Y2 (D2 ≥ 2); D2 = 0 and Y2 = 0: every M | point (+ all) |
| `÷` | `[Y2·D2, Y2·D2 + D2 − 1]` | range |
| `mod` | `Y2 + j·D2`, j ≥ 0, only when D2 > Y2 | progression: step j while ≤ max M in F, at most 1 000 steps |
| `%` | `[ceil(100·Y2 / D2), floor((100·Y2 + 99) / D2)]`, Y2 ≤ 2^24 | range |
| `a->c k` | M mod 10 = Y2 − 10k (must be 0..9) | scan F by last digit (index F by M mod 10 once) |
| `b->c k` | `[Y2 − k, Y2 − k + 9]` when Y2 mod 10 = k | range |

3. Point lookups use the `F` dictionary; ranges use a sorted array of F's keys (binary search, then walk); `a->c` uses a by-last-digit index. Each match gives a candidate `(M, op2, D2 presses, Y2 post-edits)` with cost `F[M].Count + 1 + |D2| + post.Count` (remorph: no `=`).

### Build, check, keep

As in `ArithmeticSearch`: collect candidates as small structs (indexes, no press lists), group by cost, build presses one cost level at a time cheapest first (ties by press text), `Keep` = `accept` → dedupe by text → `EngineeringCalculator.Run` last display == G → `Buttons.Fits`. Stop at `maxLines` kept lines. Return ordered with `Order`. Throw `OperationCanceledException` every 1 024 outer iterations.

Lines that are also one-operator lines can't appear (they'd need a no-op segment, which is skipped).

## Wiring

### MainForm search task

```
arithmetic (as today)                    → queue; cap2 = best kept arithmetic + CostSlack, else MaxPresses
if plan.TwoOperator: TwoOperatorSearch.Find(start, goal, buttons, cap2, token, accept: plan.Accept, requiredOp: plan.RequiredOp)
                                          → queue; divisionCap = min over kept arithmetic and two-operator lines + CostSlack
division (as today, with divisionCap)
modulo (as today; its cap already uses every kept line in found)
```

Rejected-by-charges count: `TwoOperatorSearch.Find` takes an `onRejected` callback like `DivisionShortcut.Search`, or returns the count; add it to `rejectedLines`.

`OnSearchTick`: `twoOperatorCount = found.Count(l => l.TwoOperator != null)`. `DoneStatus`: `"{arithmetic} arithmetic line(s), {two} two-operator line(s), {division} division line(s), …"`.

Today a boss `+` with no arithmetic line ends the task with nothing; after this change the two-operator phase still runs because `plan.TwoOperator` is true.

### GoalStepSearch

Inside the k loop, after arithmetic: `if (plan.TwoOperator) TwoOperatorSearch.Find(start, target, buttons, cap, cancel, accept: accept, requiredOp: plan.RequiredOp)`, lines get `Reaches = target`. Count them in `Result.TwoOperator`; the rule-14 status shows it via `DoneStatus`.

### ResultLine

```csharp
public const string TwoOperatorMarker = ", TEST M: report back";  // remove after Test M is confirmed
```

`From(TwoOperatorSearch.Line, Buttons, string?)`. `Kind`: arithmetic 0, two-operator 1, division 2, modulo 3; tie-break within two-operator by `Order`. `ToString` appends `TwoOperatorMarker` inside the brackets (after `no <token>`) when `TwoOperator != null`.

## Test plan

`TwoOperatorSearchTests`:

1. **The game puzzle:** 59 → 40, buttons `1:1, 2:2, 3:2, 4:2, 5:2, 6:2, 7:2, 8:3, 9:2, 0:2, +:3, -:2, ×:2, ÷:2, round:1g, 0->8:2`, `requiredOp = Add`, `accept = Uses(+)`, cap 12: non-empty, every line presses `+`, cheapest cost 5, contains `- 2 0 = + 1 =`.
2. Same puzzle through `RequiredPlan.For("+", boss: true)` and the MainForm order (arithmetic then two-operator): arithmetic gives nothing, two-operator gives the lines above. (An end-to-end helper test, like the 41 → 48 one for boss mode.)
3. 41 → 48, `+:2, -:2, 9:2, 2:2`: lines `+ 9 = - 2 =` and `- 2 = + 9 =`, cost 4. With `requiredOp = Subtract` both stay; with `requiredOp = Multiply` (not in Buttons) none.
4. Charges: 41 → 48, `+:1, -:2, 9:2, 2:2`: `+ 9 = + …` style lines that press `+` twice are rejected; the two lines above still come back.
5. Ranges: `÷` as op2 (e.g. 7 → 3 via `× 9 = ÷ 2 0 =`: 63 / 20 = 3, buttons `×, ÷, 9, 2, 0`, no `+`/`-`), `mod` as op2, `%` as op2, `a->c` and `b->c` as op2: one small case each, checked against the simulator.
6. No-op operands are never produced (`+ 0`, `- 0`, `× 1`, `÷ 1`).
7. `maxCost` 3 returns nothing (two segments need at least 4 presses).
8. `maxLines` counts only accepted lines (as `ArithmeticSearch`'s test does).
9. Cancel: a cancelled token throws.

`RequiredPlanTests`: every required operator sets `TwoOperator` and `RequiredOp`; left-side and none set `TwoOperator` with `RequiredOp` null.

`ResultLine` tests: a two-operator row's `ToString` ends with `, TEST M: report back)`; ranking puts arithmetic before two-operator before division at equal cost/drained/spare.

`GoalStepSearch`: a rule-14 case where only a two-operator line fits (pick a small puzzle, check `Reaches` and `Charged == k`).

Speed check (Release, record in "As built"): 41 → 483 with every button, before and after. The phase must not add more than about 0.5 s.

## Manual test: Test M (for `user-verification/test-m.md`)

`# Test M: Two-operator lines`

- **M1.** Play with Follow game on until a line in the Lines box ends with `TEST M: report back`. Any round counts (normal, boss rule 11, boss rule 14). Write down S, G, the buttons, the boss rule if any, and the line. Press the line in the game. **Expected:** each press gives the display the solver expects and the round ends (for rule 11: with the required button pressed; for rule 14: at the raised goal).
- **M2.** If you get a boss rule 11 round where the required operator points the wrong way (as 59 → 40 with `+`), check the Lines box isn't empty. **Expected:** two-operator lines with the marker, all pressing the required button.
- **M3.** A normal round where a 1–3 press arithmetic line exists. **Expected:** the search time feels the same as before; two-operator lines, if any, rank below the shorter lines.

Report: for each marked line you tried, whether it worked, and if not, the display after each press. After M1 passes: remove `TwoOperatorMarker`, mark Test M done in its heading.

## As built (so far)

- Built as designed. `ArithmeticSearch.BuildXs` / `BuildYs` hold the shared `xs` / `ys` code; `EngineeringCalculator.Apply` is the operator arithmetic (the simulator uses it too).
- The forward half keeps routes per press cost (one table per cost level, plus a second table of routes whose op1 is the required operator). The backward half groups its (Y2, op2, D2) items by cost and, for each total cost, looks only in the forward level that completes it, so every candidate has the exact total cost and the cheap levels are built first.
- `Find` also takes `onRejected` (lines that pass the simulator but not the charges); `GoalStepSearch.Result` has `TwoOperator`.
- Segments whose result equals their input (`mod 1000` on a small value) are skipped like the no-op operands.
- Test M confirmed in the game; `TwoOperatorMarker` and its two tests removed (547 tests).
- Tests: 549 pass (519 before). `TwoOperatorSearchTests` (16 cases; the design's test 2 is `Game_puzzle_end_to_end_in_the_search_order`), `BossRequiredTests` (plan flags), `TwoOperatorResultLineTests`, `GoalStepTests` (rule 14, 41 → 48 with `+:2, 7:2, 4:2` gives `+ 4 = + 7 =` at k = 4).
- Speed (Release, every button, 41 → 483): arithmetic 333 ms; the two-operator phase (cap 5) adds 134 ms and 44 lines. 59 → 40: 102 ms (cap 5), 41 → 48: 55 ms.
