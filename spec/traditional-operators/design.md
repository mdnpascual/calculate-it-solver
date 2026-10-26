# Design: Traditional operators (+, −, ×)

**Status:** Complete (2026-10-22).

**Corrected 2026-10-25:** `Limit = 10^18`, `MaxDigits` and the `full` state below don't match the game. The game clamps `+` and `×` results above 999 999 to 999 999 and buttons keep working. See [`display-cap/`](../display-cap/).

## Overview

| Part | File | Change |
|------|------|--------|
| Keys and buttons | `EngineeringCalculator.cs` | `Key.Add`, `Key.Subtract`, `Key.Multiply`; `Buttons.Add/Subtract/Multiply`; `Parse` tokens |
| Simulator | `EngineeringCalculator.cs` | The three operators in `Run` |
| Game feed | `GameLink.cs` | `MapButton` for `operator+`, `operator-`, `operator*` |
| Search | new `ArithmeticSearch.cs` | One-operator and edits-only lines |
| Shared pieces | `DivisionShortcut.cs` | Expose the X and Y candidate builders so both searches use them |
| Merged list | `ResultLine.cs` | Third kind, ranked first among ties |
| UI | `MainForm.cs` | Arithmetic phase before the division search; queue of `ResultLine`; status |

## 1. Keys, buttons, parse

```csharp
public enum Key { Prepend, Append, Ten, Digit, Reset, Flip, Divide, Mod, Add, Subtract, Multiply, Equals }
```

`Press.ToString`: `Add` → `"+"`, `Subtract` → `"-"`, `Multiply` → `"×"`. These are also the charge keys.

`Buttons` gets three optional positional parameters after `ChargeMap`, so existing `new Buttons(...)` calls compile unchanged:

```csharp
public record Buttons(..., bool Flip,
                      IReadOnlyDictionary<string, int>? ChargeMap = null,
                      bool Add = false, bool Subtract = false, bool Multiply = false)
```

`Buttons.All` passes `true` for all three.

`Buttons.Parse`:

- `+` → `Add`; `-` or `−` → `Subtract`; `×`, `*` or `x` → `Multiply`. (`x` alone doesn't clash with `x4`: those match `^x\d$` first.)
- Charge keys are normalised to `+`, `-`, `×` (like `/` → `÷`), and `:0` removes the button.

Add a helper used by the simulator and the search:

```csharp
/// <summary>True when the buttons include this operator key (÷, mod, +, -, ×).</summary>
public bool Has(Key op) => op switch { Key.Divide => Divide, Key.Mod => Mod, Key.Add => Add,
    Key.Subtract => Subtract, Key.Multiply => Multiply, _ => false };
```

## 2. Simulator

In `Run`, the operator case becomes:

```csharp
case Key.Divide or Key.Mod or Key.Add or Key.Subtract or Key.Multiply
        when op == null && (buttons == null || buttons.Has(press.Key)):
```

`Equals`:

```csharp
if (!long.TryParse(operand, out var d)) return null;
if (d == 0 && op is Key.Divide or Key.Mod) return null;
long? result = op switch
{
    Key.Divide => left / d,
    Key.Mod => left % d,
    Key.Add => left + d < Limit ? left + d : null,          // both < 10^18, no overflow
    Key.Subtract => Math.Max(0, left - d),
    Key.Multiply => d == 0 || left <= (Limit - 1) / d ? left * d : null,
    _ => null,
};
if (result == null)
{
    log.Add(MaxDigits);   // "max digits reached"
    full = true;          // any later press returns null
    break;
}
```

with `const long Limit = 1_000_000_000_000_000_000;` (10^18) and `public const string MaxDigits = "max digits reached";`. A `bool full` local makes every press after it return null (check it at the top of the loop). The line then can't end on G, so the searches drop it. Rename the local `divisor` to `d`. Update the `Run` doc comment (÷ and mod by 0, results ≥ 10^18 → `max digits reached`).

## 3. `GameLink.MapButton`

Add to the switch: `"operator+" => "+"`, `"operator-" => "-"`, `"operator*" => "×"`. The ids `operator_+2`, `operator_-4`, `operator_--` have an underscore and stay unmapped.

## 4. Shared candidate sets (`DivisionShortcut.cs`)

`SearchCore` builds the cores, the quotient pieces of G, the suffixes and the prefixes inline. Move the pieces both searches need into internal static helpers in `DivisionShortcut` (no behavior change for division):

```csharp
/// <summary>S, and reverse(S) when flip is available and it differs.</summary>
internal static List<string> Cores(string s, Buttons buttons);

/// <summary>Every piece of G (and its reverse with flip) without a leading 0.</summary>
internal static HashSet<string> Pieces(string g, Buttons buttons);

/// <summary>"" and every 1–2 digit string (suffixes; Plan rejects the ones the buttons can't add).</summary>
internal static List<string> Suffixes();

/// <summary>"" and 1–2 digit prefixes from the prepend digits (and append digits with flip), no leading 0.</summary>
internal static List<string> Prefixes(Buttons buttons);
```

`Reverse` becomes `internal` too. Path B in `SearchCore` uses `Prefixes`; the rest uses the others.

## 5. `ArithmeticSearch.cs`

```csharp
/// <summary>
/// One-operator lines with +, - and ×: edit S into X, press the operator, type D, =, edit the
/// result Y into G. D is fixed by X and Y (Y − X, X − Y, Y / X). Also edits-only lines (S → G
/// with left edits alone). Every line is checked in the simulator and against the charges.
/// </summary>
public static class ArithmeticSearch
{
    public const int MaxLines = 100;

    /// <summary>Op is null for an edits-only line; then Built = S, Operand = 0, Result = S.</summary>
    public record Line(Key? Op, string Built, long Operand, long Result,
                       IReadOnlyList<Press> Presses, int Cost, int Order);

    public static IReadOnlyList<Line> Find(long start, long goal, Buttons buttons, int maxCost,
                                           CancellationToken cancel = default, int maxLines = MaxLines);
}
```

### `Find`

```
if start == goal → []
s, g = text of start, goal
plan(from, to) = LeftEdits.Plan cached by (from, to)
candidates = []                 (presses, op, built, D, Y)

// Edits only.
if plan(s, g) is { } direct and direct.Count <= maxCost: candidates.Add(direct, null, s, 0, start)

ops = [Add, Subtract, Multiply] where buttons.Has(op)
if ops empty or (!buttons.Ten and no digit keys): skip to "keep"

ys = for each piece y in Pieces(g): post = plan(y, g); keep (long y, post) when post != null and 2 + post.Count <= maxCost
xs = for each core in Cores(s), t in Suffixes(), a in Prefixes(buttons):
        built = a + core + t; long.TryParse; pre = plan(s, built); keep when pre != null and pre.Count + 2 <= maxCost
     (cancel.ThrowIfCancellationRequested() once per core and suffix)

for (x, pre, built) in xs, (y, post) in ys, op in ops:
    d = op switch { Add => y - x, Subtract => x - y, Multiply => x >= 1 && y % x == 0 ? y / x : 0 }
    if d < 1 or (op == Multiply and d < 2): continue
    budget = maxCost - pre.Count - 1 - post.Count
    if budget < 1 or digitCount(d) > (buttons.Ten ? 2 * budget : budget): continue   // quick bound
    typed = TypeOperand(d.ToString(), buttons)         (cache by d)
    if typed == null or typed.Count > budget: continue
    candidates.Add(pre + [op] + typed + [=] + post, op, built, d, y)

keep:
lines = []; seen = set of press text
for c in candidates ordered by (Cost, press text):
    if !seen.Add(text) continue
    log = Run(start, presses, buttons); log?[^1] != g → continue
    if !buttons.Fits(presses) → continue
    lines.Add(new Line(..., Order: lines.Count)); stop at maxLines
return lines
```

Notes:

- X with a leading 0 (from `flip` of a number ending in 0, or padding) is parsed as a number, matching the simulator (leading zeros drop when the operator is pressed).
- For Subtract, Y ≥ 1 unless G = 0 (pieces have no leading 0, and G = 0 is allowed). With Y = 0 the search uses D = X − Y = X, so the clamp to 0 never adds a candidate beyond that; it only matters in the simulator.
- Size: about (1 + 9 + 90) prefixes × 2 cores × 111 suffixes ≈ 22 000 X, at most ~20 Y and 3 ops: under 1.5 million cheap checks, most cut by `pre == null` or the quick bound. No threads.
- Candidates are small structs (X index, Y index, op, D, cost), not press lists: with every button there can be about a million of them. Sort them by cost, then build presses, sort by press text and check (simulator, `Fits`) one cost level at a time, stopping once 100 lines are kept. Only the cheap levels are ever turned into presses.

## 6. `ResultLine.cs`

- Add `ArithmeticSearch.Line? Arithmetic` as the last positional parameter (default `null`) and `From(ArithmeticSearch.Line, Buttons)`.
- `Ranking` after cost, drained, spare: kind order arithmetic (0) < division (1) < modulo (2); same kind uses the existing tie-breaks, arithmetic uses `Order`.

## 7. `MainForm`

- `searchQueue` becomes `ConcurrentQueue<ResultLine>`. `Drain` adds queued items as they are (no wrapping); the division `onLine` wraps with `ResultLine.From(line, buttons)` before enqueueing.
- `StartDivisionSearch` runs one background task:

  ```csharp
  searchTask = Task.Run(() =>
  {
      foreach (var line in ArithmeticSearch.Find(start, goal, buttons, MaxPresses, token))
          queue.Enqueue(ResultLine.From(line, buttons));
      return DivisionShortcut.Search(start, goal, buttons, MaxPresses,
          l => queue.Enqueue(ResultLine.From(l, buttons)), token,
          onRejected: _ => Interlocked.Increment(ref rejectedLines));
  }, token);
  ```

  A cancel during `ArithmeticSearch.Find` throws `OperationCanceledException`; the task then counts as cancelled, which `StopDivisionSearch` already ignores.
- Counts: replace `divisionCount = found.Count` with counts by kind (`found.Count(l => l.Division != null)` and `l.Arithmetic != null`).
- Modulo cap: `found.Min(l => l.Cost) + 1` over everything in `found`; this now includes arithmetic lines without code change.
- `DoneStatus`: `"{A} arithmetic line(s), {N} division line(s)" + modulo part + ", {R} rejected"`. The "No division line fits…" status only shows when `found` is empty after the division phase (arithmetic found nothing either); reword to `No line fits the available buttons…`.
- Default Buttons text gains `+:9, -:9, ×:9`.

## Optional, not in v1: division cap

Requirements rule 7, deferred. If turned on later: in the background task, after `ArithmeticSearch.Find`, pass `Math.Min(MaxPresses, lines.Min(Cost) + 1)` (or `MaxPresses` when there are no arithmetic lines) as `maxPresses` to `DivisionShortcut.Search`. No other change is needed: the modulo cap already uses the cheapest line in `found`.

## Test plan

`EngineeringCalculatorTests` (new cases):

- `55`, `+ 2 2 =` → `77`; `91`, `- 8 4 =` → `7`; `5`, `- 9 =` → `0`; `12`, `× 4 =` → `48`.
- `+` on the right side → null; `+` missing from buttons → null; `+ 0 =` → the same value; `÷ 0 =` still null.
- Overflow: `999999999999`, `× 9 9 9 9 9 9 9 =` → last entry `max digits reached`; the same presses followed by `X1` → null.

Parse tests: `+:2, -:1, ×:3` parse with charges keyed `+`, `-`, `×`; `*:2` and `x:2` give `×`; `-:0` removes Subtract; `x4:1` is still an append.

`GameLinkTests`: `operator+` → `+`, `operator-` → `-`, `operator*` → `×`; `operator_+2` → null.

New `ArithmeticSearchTests` (each checks every returned line runs to G, fits charges, cost ≤ cap, distinct presses):

- Plain calculator: every line starts with an operator (no left edits, digit keys never on the left).
- Plain calculator, 55 → 77, digits `0`–`9:2`, `+:2, -:2, ×:2, ÷:2` → first line `+ 2 2 =`, cost 3.
- 41 → 48 with `+:1, 7:1` → `+ 7 =`.
- 91 → 7 with `-:1, 8:1, 4:1` → `- 8 4 =`.
- 12 → 48 with `×:1, 4:1` → `× 4 =`; 6 → 242 with `×:1, 4:1, X2:1` → `× 4 = X2`.
- 3 → 50 with `1X:1, +:1, 3:1, 7:1` → `1X + 3 7 =`.
- Edits only: 36 → 63 with `flip:1` → `flip`, Op null.
- Charges: 55 → 77 with `+:1, 2:1` → no `+ 2 2 =` (needs `2` twice).
- No operators and no edits path → empty. S = G → empty. `maxLines` 2 → at most 2. Cancelled token → throws.

`ResultLineTests`: arithmetic before division before modulo at equal cost, drained and spare.

`DivisionShortcutTests`: all pass unchanged after the helper refactor.

Manual:

- `dotnet run --project CalculatorPuzzle`, S 55, G 77, Buttons `1:2, 2:2, 3:2, 4:2, 5:2, 6:2, 7:2, 8:2, 9:2, 0:2, +:2, -:2, ×:2, /:2`, Enter: `+ 2 2 =` appears at the top right away, then the division and modulo phases run.
- With the game feed: a plain calculator round fills `+`, `-`, `×` with charges.
- S 36, G 63, `flip:2` among the buttons: `flip` (1 press) is the top line.

## As built

Built as designed. Differences and notes:

- `Parse` maps the aliases (`−`, `*`, `x` → `-`, `×`) right after the charge count is split off, before the token is matched, so `x4` still matches the append pattern first.
- `ArithmeticSearch.Find` returns its lines sorted by cost, then press text, with `Order` set to the position in that list (the edits-only line is merged in by cost, not always first).
- `PrefixDigits` is a private helper in `DivisionShortcut` shared by `Prefixes` and the Division Shortcut's Path A.
- `Find` with all buttons and 41 → 483: about 180 ms (Debug), 100 lines.
- The manual GUI checks (55 → 77, 36 → 63, Esc) were not run by the agent that built this; the logic behind them is covered by `ArithmeticSearchTests`.
- Tests added: `ArithmeticTests.cs` (simulator, parse, search, ranking) and `ArithmeticMapButtonTests` in `GameLinkTests.cs`. 207 tests pass.
- **Caps (changed after playing, 2026-10-22):** the arithmetic phase runs first; if it finds lines, the division press limit is `min(12, cheapest arithmetic cost + 2)`. The modulo cap is `min(12, cheapest cost among all lines + 2)`. Both use `MainForm.CostSlack = 2`. This replaces the "optional division cap" section above (which was +1) and the modulo "+ 1" in §7.
- **Display (2026-10-23):** the Lines box shows `÷` as `/`, because `÷` was easy to mistake for `+`. Only `ResultLine.ToString` changed; `Press.ToString` and the charge keys still use `÷`.
