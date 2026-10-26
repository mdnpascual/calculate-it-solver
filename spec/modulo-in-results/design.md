# Design: Modulo lines in the results list

**Status:** Complete (2026-10-22).

## Overview

| Part | File | Change |
|------|------|--------|
| Simulator | `EngineeringCalculator.cs` | `Key.Mod`, `mod` in `Run`; `Buttons.Spare` |
| Charges | `EngineeringCalculator.cs` | `Buttons.Parse` requires `:n` on every button except `=`; `Buttons.Drained` |
| Modulo search | new `ModuloSearch.cs` | Walks A, k, pairs; builds, checks and costs each formula |
| Merged list | new `ResultLine.cs` | One row type for both kinds, and the merged ranking |
| UI | `MainForm.cs` | Second phase after the division search; merged list; status; default Buttons text; then remove the A, k, dropdown and Solution rows |

## 1. Simulator: `mod`

- Add `Mod` to `Key` (before `Equals`). `Press.ToString` gives `"mod"`, which matches the charge key `Buttons.Parse` already writes for `mod`.
- `Run` keeps the pending operator: replace `bool pending` with `Key? op` (null = left side). `Divide` and `Mod` share one case (`when op == null && allowed`), so both parse the left side, set `op` and clear the operand.
- `Equals`: operand 0 → null (÷ and mod by zero). `÷` gives `left / operand`, `mod` gives `left % operand`. Both are non-negative here, so `%` matches the game.
- All existing `pending` checks become `op != null` / `op == null`. `10` and digit keys stay right-side only; `dX`, `Xd`, `flip`, `reset` stay left-side only.

## 2. Charges required and `Spare`

`Buttons.Parse`:

- A token without `:n` (other than `=`) returns `Missing charge count in "<raw>" (write <TOKEN>:n)`.
- `=:n` is accepted and ignored, like `=`.
- Everything else is unchanged.

The `Buttons` constructor still allows an empty `ChargeMap` (`Buttons.All` and hand-built test buttons); `Fits` keeps its `Charges.Count == 0` shortcut.

New method on `Buttons`:

```csharp
/// <summary>Sum of the charges of the distinct buttons pressed (= not counted). A button with no charge entry adds 0.</summary>
public int Spare(IReadOnlyList<Press> presses) =>
    presses.Where(p => p.Key != Key.Equals).Select(p => p.ToString()).Distinct()
           .Sum(token => Charges.GetValueOrDefault(token));

/// <summary>Distinct buttons pressed as often as their charges (left at 0). Buttons without a charge entry never count.</summary>
public int Drained(IReadOnlyList<Press> presses) =>
    presses.Where(p => p.Key != Key.Equals).GroupBy(p => p.ToString())
           .Count(g => Charges.TryGetValue(g.Key, out var n) && g.Count() >= n);
```

## 3. `ModuloSearch.cs`

```csharp
public static class ModuloSearch
{
    public const int MaxLines = 100;
    public const int MaxA = 99;

    public record Line(long A, int K, long Value, long Modulus, IReadOnlyList<Press> Presses, int Cost, int Order);

    /// <summary>Lines found and the last A looked at (for the status).</summary>
    public record Result(string? Error, IReadOnlyList<Line> Lines, long LastA);

    public static Result Find(long start, long goal, Buttons buttons, int maxCost,
                              CancellationToken cancel = default,
                              int maxLines = MaxLines, long maxA = MaxA);

    /// <summary>Presses for one formula (left edits, mod, operand, =), or null when the buttons can't build it.</summary>
    public static IReadOnlyList<Press>? Presses(long start, int k, ModuloShortcut.Formula formula, Buttons buttons);
}
```

### `Presses`

1. `target = formula.Prepend.ToString() + start.ToString().PadLeft(k, '0')`.
2. `pre = LeftEdits.Plan(start.ToString(), target, buttons)`; null → null.
3. `operand = DivisionShortcut.TypeOperand(formula.Modulus.ToString(), buttons)`; null → null.
4. Return `pre + [mod] + operand + [=]`. Needs `buttons.Mod`; otherwise null.

Note `formula.Value` equals the value of `target` (leading zeros only appear after P), so the simulator result is `Value % Modulus`.

### `Find`

```
if start == goal                                   → (null, [], 0)
if !buttons.Mod || (!buttons.Ten && no digit keys) → ("The Modulo Shortcut needs mod and 10 or a digit key.", [], 0)
seen = set of press strings; lines = []; planCache keyed by target string
for A in 1..maxA:
    for k = MinK(S); LowerBound(k) <= maxCost; k++:
        cancel.ThrowIfCancellationRequested()   (caught: return what's found, see below)
        r = ModuloShortcut.Solve(S, G, A, k)
        if r.Error != null: if it's an overflow, stop growing k; otherwise continue
        for pair in r.Pairs (small first):
            for f in [pair.First, pair.Second]:
                if !f.Valid → skip
                presses = Presses(S, k, f, buttons); null → skip
                cost = EngineeringCalculator.Cost(presses); cost > maxCost → skip
                if !seen.Add(join(presses)) → skip
                log = EngineeringCalculator.Run(S, presses, buttons); log?[^1] != G → skip
                if !buttons.Fits(presses) → skip
                lines.Add(new Line(A, k, f.Value, f.Modulus, presses, cost, lines.Count))
                if lines.Count == maxLines → return
return (null, lines, A reached)
```

`LowerBound(k)` = cheapest any line with this k could be, which only grows with k:
`(k − len(S))` padding presses `+ 1` (at least one prepended digit) `+ 1` (mod) `+` operand presses for k + 1 digits (`(k + 2) / 2` with `10`, `k + 1` without). Stop growing k as soon as it passes `maxCost`.

Cheap pre-check before `LeftEdits.Plan`: skip a formula when `(k − len(S)) + len(P) + 1 + operandLowerBound > maxCost`.

Cancellation: `Find` lets `OperationCanceledException` propagate; the UI treats a cancelled modulo task as "not finished" and adds nothing (requirements 4.4).

Cost: d ≈ A·10^k, the pair loop is O(√d). With cap ≤ 13 and short S, k stays under ~9, so the worst case is about 99 × 8 × 10^5 cheap steps, well under a second. No worker threads for now.

## 4. `ResultLine.cs`

```csharp
/// <summary>One row of the Lines box: a division or a modulo line, with its spare.</summary>
public record ResultLine(IReadOnlyList<Press> Presses, int Cost, int Drained, int Spare,
                         DivisionShortcut.Line? Division, ModuloSearch.Line? Modulo)
{
    public static ResultLine From(DivisionShortcut.Line line, Buttons buttons) =>
        new(line.Presses, line.Cost, buttons.Drained(line.Presses), buttons.Spare(line.Presses), line, null);
    public static ResultLine From(ModuloSearch.Line line, Buttons buttons) =>
        new(line.Presses, line.Cost, buttons.Drained(line.Presses), buttons.Spare(line.Presses), null, line);

    /// <summary>Cost (low first), drained (few first), spare (high first), division before modulo, then each kind's own order.</summary>
    public static IComparer<ResultLine> Ranking { get; }
}
```

Ranking tie-breaks: two division lines → `DivisionShortcut.Ranking`; two modulo lines → `Order`.

## 5. `MainForm`

State:

- `found` becomes `List<ResultLine>`. The division queue stays `ConcurrentQueue<DivisionShortcut.Line>`; `Drain` wraps each line with `ResultLine.From(line, searchButtons)` (keep the parsed `Buttons` of the running search in a field) and sorts with `ResultLine.Ranking`.
- `moduloTask: Task<ModuloSearch.Result>?`, sharing `searchCancel`.

Flow in `OnSearchTick` when the division task completes without error and without cancel:

1. `C = found.Min(Cost)` over division lines, or `MaxPresses` when there are none; `cap = found.Count > 0 ? C + 1 : MaxPresses`.
2. If S ≠ G, start `moduloTask = Task.Run(() => ModuloSearch.Find(start, goal, buttons, cap, token))`, keep the timer running, status `Modulo Shortcut… Esc to stop.` (with the division count so far).
3. On a later tick, when `moduloTask` completes: if it succeeded, add its lines (wrapped) to `found`, sort, redraw; set the final status (requirements 6.4). If it returned an error (no `mod`), show the division status plus `Modulo: <error>`. If cancelled or faulted, add nothing.

`StopDivisionSearch` (Esc, new Enter, new puzzle, closing) cancels both tasks; it's called the same way as now. Esc while the modulo task runs keeps the division lines and says `Stopped. N line(s) found.`

`Describe(ResultLine)`: `$"{string.Join(' ', l.Presses)}   ({l.Cost} presses, spare {l.Spare}{(l.Drained > 0 ? $", empties {l.Drained}" : "")})"`.

Default Buttons box text gets counts, for example
`5X:9, X1:9, X2:9, X7:9, X4:9, 4X:9, 6X:9, 3X:9, 8X:9, 10:9, /:9, X9:9, flip:9, mod:9, =, reset:9`.

## 6. Remove the manual Modulo Shortcut rows (last step)

Done after sections 1–5 work. In `MainForm` remove:

- fields `iteratorBox`, `minusButton`, `plusButton`, `kBox`, `kMinusButton`, `kPlusButton`, `detailLabel`, `factorBox`, `line1Box`, `line2Box`;
- the rows `Iterator (A)`, `Places (k)`, the detail row, `Factors (M × N)`, `Solution 1`, `Solution 2`;
- `StepIterator`, `StepK`, `Recalculate`, `ShowSelectedPair`, `PairItem`, `Describe(ModuloShortcut.Formula)`;
- the `Recalculate()` calls and the k reset in the S / G `TextChanged` handlers (they keep `MarkDivisionStale()`), and `iteratorBox.Text = "1"` in `ApplyGamePuzzle`.

Keep `ModuloShortcut.cs` and `ModuloShortcutTests` (used by `ModuloSearch`). Rename the `Division Shortcut` row caption (the one with Follow game / Lock) to `Game`, since the list now holds both shortcuts.

## Test plan

`EngineeringCalculatorTests`:

- `mod` in `Run`: `41`, `2X 3X mod 10 3 =` with `Buttons.All` → last entry `48`; `mod` with operand 0 → null; `mod` on the right side → null; `mod` missing from buttons → null.
- `÷` cases still pass after the `pending` → `op` change.

`ChargesTests` / parse tests:

- `X1` without count → error naming `X1`; `X1:2, =` parses; `=:3` is ignored.
- `Spare`: `4X mod 10 5 =` with `4X:2, mod:3, 10:5, 5:1` → 11; a button pressed twice counts once; `=` adds nothing.
- `Drained`: the same line → 1 (`5:1`); `X1 X1` with `X1:2` → 1, with `X1:3` → 0; `Buttons.All` → 0.
- Existing tests that call `Buttons.Parse` without counts get `:9` (or a helper that adds it); the two tests for unlimited buttons (`"X1, 10:3"`, `"X1:1, X2"`) become tests for the new error.

New `ModuloSearchTests`:

- `Presses`: 31 → 11, A 1, k 2, pair 4 × 5 → `4X mod 10 5 =`; 5 → 48, `2005 mod 103` → `0X 0X 2X mod 10 3 =`; no `mod` → null; modulus 131 without digit key `3` → null.
- `Find`, 31 → 11 with `4X:2, 5X:2, mod:3, 10:5, 4:1, 5:1`, cap 4 → contains `4X mod 10 5 =` at cost 4; every line's presses run to G, cost ≤ cap, fits charges, presses are distinct.
- Stops at `maxLines` (pass a small value such as 3) and never goes past `maxA`.
- Cap below every line → empty, no error. No `mod` → error. S = G → empty.
- Cancelled token → throws `OperationCanceledException`.

New `ResultLineTests`:

- Cost before drained before spare; fewer drained first at the same cost even with a lower spare; higher spare first at the same cost and drained; division before modulo when all three are equal; modulo ties keep `Order`.

Manual:

- `dotnet run --project CalculatorPuzzle`, S 31, G 11, Buttons `4X:2, 5X:2, mod:3, 10:5, 4:1, 5:1, ÷:3, X1:2`, Enter: division lines stream in, then `Modulo Shortcut…`, then modulo rows appear in cost/spare order with `spare` in each row.
- Esc during each phase; a new Enter during the modulo phase restarts from the division phase.
- A token without a count shows the new error in the status.
- After section 6: the A, k, dropdown and Solution rows are gone, the form shrinks, and a game puzzle still fills S, G, Buttons and starts the search.

## As built

- Everything above was built as designed. `Buttons.Parse` error text uses the token as typed: `Missing charge count in "x1" (write x1:n)`.
- `ModuloSearch.Presses` has a private overload that takes a plan cache (keyed by target string), used by `Find`.
- `MainForm` reuses `searchCancel` for both phases; the cancellation source is disposed in `FinishSearch` when the modulo phase ends (or when the division phase ends with an error, S = G, or no parseable S/G).
- `ResultLine.ToString()` is the row text (`presses   (n presses, spare s[, empties b])`).
- Tests: `TestButtons.Parse` (test helper) adds `:9` to count-less tokens so older tests keep their button lists. New tests are in `ModuloSearchTests.cs` (`ModSimulatorTests`, `ModuloSearchTests`, `ResultLineTests`) and `ChargesTests.cs`.
- Not checked by hand in the GUI: Esc in each phase and restart during the modulo phase (checked only by reading the code). With the default Buttons text (no digit keys) the modulo search finds no lines for 41 → 48, in about 9 ms.
- Changes after first use: the status line is now `N division line(s), M modulo line(s), R rejected` (no round/start/goal text, no `Ignored:` note, no `up to A`). When the Division Shortcut can't run (no `÷`, or no `10` or digit key), the modulo search still runs with the cap set to the press limit (12).
