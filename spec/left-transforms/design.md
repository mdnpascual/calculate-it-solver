# Design: Left-side transforms

**Status:** Complete (2026-10-23). See "As built" at the end.

**Corrected 2026-10-25:** `Limit = 10^18` and `max digits reached` below don't match the game. The game clamps `X+k`, `X*2`, `<<`, `sqr` (and `+`, `×`) at 999 999 and buttons keep working; `p` and `round` aren't clamped; `Xd` / `dX` stop at 6 characters. See [`display-cap/`](../display-cap/).

## Overview

| Part | File | Change |
|------|------|--------|
| Transform catalogue | new `LeftTransforms.cs` | `LeftTransform` (token, apply, preimages) and `LeftTransforms.Parse` |
| Keys and buttons | `EngineeringCalculator.cs` | `Key.Transform`; `Press.Transform`; `Buttons.Transforms`; `Parse` tokens |
| Simulator | `EngineeringCalculator.cs` | `Key.Transform` case in `Run` |
| Game feed | `GameLink.cs` | `MapButton` for the new ids |
| Left-side search | new `LeftSearch.cs` | Forward and backward BFS over displays, and `Connect` (meet in the middle) |
| Arithmetic phase | `ArithmeticSearch.cs` | Edits-only lines from `Connect`; extra X and Y candidates from the BFS maps |
| UI | `MainForm.cs` | No change needed beyond what `Buttons.Parse` accepts |

## 1. Transform catalogue (`LeftTransforms.cs`)

Buttons are data: each new button is one `LeftTransform`. The simulator and the search never switch on the token.

```csharp
/// <summary>
/// A left-side button that maps the display to a new display. Apply returns null when the result
/// is 10^18 or more (max digits). Preimages lists every display that Apply maps to the given one,
/// used by the backward search (see "Preimages" below for what it leaves out). Equality is by Token.
/// </summary>
public sealed class LeftTransform : IEquatable<LeftTransform>
{
    public string Token { get; }                                   // "X++", "X+16", "2->4", "to25", ...
    public Func<string, string?> Apply { get; }
    public Func<string, IEnumerable<string>> Preimages { get; }
    // Equals / GetHashCode / ToString by Token.
}

public static class LeftTransforms
{
    /// <summary>The transform for a token (any case, spaces removed, "−" read as "-"), or null.</summary>
    public static LeftTransform? Parse(string token);
}
```

Canonical tokens (these are also the charge keys and `Press.ToString`): `X++`, `X--`, `X+k`, `X-k` (k without leading zeros, k ≥ 1), `X*2`, `X/2`, `<<`, `>>`, `sqr`, `sqrt`, `a->b` (a ≠ b), `to25`, `round`. `Parse` matches the lowercased token against `^x\+\+$`, `^x--$`, `^x\+(\d+)$`, `^x-(\d+)$`, `^x\*2$`, `^x/2$`, `^<<$`, `^>>$`, `^sqr$`, `^sqrt$`, `^(\d)->(\d)$`, `^to25$`, `^round$`. Reject k = 0, k with a leading zero, k > 10^9, and a = b. Cache the instances in a dictionary so `Parse` returns the same object per token.

### Apply

Number transforms read the display with `long.TryParse` (leading zeros drop; a display that doesn't fit in a `long` gives null, shown as max digits). Let `Limit = 10^18`.

| Token | Result (as text, no leading zeros) |
|-------|--------|
| `X+k` / `X++` | v + k, null if ≥ Limit |
| `X-k` / `X--` | max(0, v − k) |
| `X*2`, `<<` | 2v, null if ≥ Limit |
| `X/2`, `>>` | v / 2 |
| `sqr` | v², null if v > 999 999 999 (v² ≥ Limit) |
| `sqrt` | round(√v): r = floor(√v) (`Math.Sqrt` corrected by ±1), then r + 1 when v > r² + r, else r |
| `round` | v − v % 10, plus 10 when v % 10 ≥ 5; null if ≥ Limit |

Digit maps: `display.Replace(a, b)` on the text (leading zeros kept, and created ones kept). `to25`: `"25"`.

### Preimages

`Preimages(w)` gives the displays p with `Apply(p) == w`, used by the backward search. Number transforms always output text with no leading zeros, so for them a `w` with a leading zero (length > 1, starts with `0`) has no preimages. They return preimages **as plain numbers only** (no leading-zero variants such as `05`). The backward search can miss lines that need those; the forward half of `Connect` still finds them within its depth.

| Token | Preimages of v |
|-------|----------------|
| `X+k` / `X++` | v − k when v ≥ k |
| `X-k` / `X--` | v + k when v > 0 (and < Limit); when v = 0: 0, 1, …, k |
| `X*2`, `<<` | v / 2 when v is even |
| `X/2`, `>>` | 2v and 2v + 1 (when < Limit) |
| `sqr` | √v when v is a perfect square |
| `sqrt` | v² − v + 1, …, v² + v (when < Limit; just 0 for v = 0), yielded lazily upward |
| `round` | v − 5 … v + 4 (those ≥ 0) when v % 10 = 0, otherwise none |
| `a->b` | none if w contains a; otherwise every string made by changing any subset of w's b digits to a (2^count; yield none when count > 12) |
| `to25` | none (a constant: found by the forward search only, see §4) |

Test property (see test plan): for every transform and many w, every preimage p satisfies `Apply(p) == w`.

## 2. Keys, `Press`, `Buttons`, `Parse`

```csharp
public enum Key { Prepend, Append, Ten, Digit, Reset, Flip, Transform, Divide, Mod, Add, Subtract, Multiply, Equals }

public record Press(Key Key, int Digit = -1, LeftTransform? Transform = null)
```

`Press.ToString`: `Key.Transform => Transform!.Token`. Add a helper `Press.Of(LeftTransform t) => new(Key.Transform, Transform: t)`.

`Buttons` gets one more optional positional parameter at the end, so existing calls compile unchanged:

```csharp
..., bool Add = false, bool Subtract = false, bool Multiply = false,
IReadOnlyCollection<LeftTransform>? TransformList = null)
{
    /// <summary>The left-side transform buttons, in the order given.</summary>
    public IReadOnlyList<LeftTransform> Transforms { get; } = TransformList?.ToList() ?? [];
```

`Buttons.All` stays as it is (no transforms), so the existing tests and timings don't move. Tests that want everything add transforms explicitly.

`Buttons.Parse`: after the existing token checks and before `Unknown button`, try `LeftTransforms.Parse(token)`. Put the result in an ordered set (no duplicates). The charge key is `transform.Token`. `:0` removes it. Two details:

- The existing alias step maps the **whole** token (`"*"`, `"x"`, `"−"`). Also replace `−` with `-` inside the token before matching, so `x−4` works.
- `x+2` must not hit any existing pattern first. It doesn't (`^x(\d)$` needs exactly one digit after `x`).

## 3. Simulator

In `Run`, next to `Flip`:

```csharp
case Key.Transform when op == null && press.Transform is { } t
        && (buttons == null || buttons.Transforms.Contains(t)):
    if (t.Apply(display) is not { } next)
    {
        log.Add(MaxDigits);
        full = true;
        break;
    }
    display = next;
    log.Add(display);
    break;
```

Update the `Run` doc comment to mention the transforms as left-side buttons.

## 4. `GameLink.MapButton`

Before the switch:

```csharp
if (Regex.Match(id, @"^operator_(\d)to(\d)$") is { Success: true } map)
    return $"{map.Groups[1].Value}->{map.Groups[2].Value}";
if (Regex.Match(id, @"^operator_([+-])(\d+)$") is { Success: true } step)
    return $"X{step.Groups[1].Value}{step.Groups[2].Value}";
```

In the switch: `"operator_++" => "X++"`, `"operator_--" => "X--"`, `"operator_*2" => "X*2"`, `"operator_/2" => "X/2"`, `"operator_<" => "<<"`, `"operator_>" => ">>"`, `"operator_sqr" => "sqr"`, `"operator_sqrt" => "sqrt"`, `"operator_to25" => "to25"`, `"operator_round" => "round"`.

`operator_100-x` doesn't match `^operator_([+-])(\d+)$` (it starts with digits), so it stays null.

## 5. `LeftSearch.cs`

```csharp
/// <summary>
/// Fewest left-side presses between displays, with every left-side button (dX, Xd, flip, reset
/// and the transforms). Displays are strings, so leading zeros are kept. No path uses a button
/// more often than its charges.
/// </summary>
public static class LeftSearch
{
    public record Limits(int MaxPresses, int MaxDigits, int MaxStates);

    /// <summary>Every display reachable from `from`, with the fewest presses to get there.</summary>
    public static Dictionary<string, IReadOnlyList<Press>> Forward(string from, Buttons buttons, Limits limits,
                                                                    CancellationToken cancel = default);

    /// <summary>Every display from which `to` is reachable, with the fewest presses from there to `to`.</summary>
    public static Dictionary<string, IReadOnlyList<Press>> Backward(string to, Buttons buttons, Limits limits,
                                                                     CancellationToken cancel = default);

    /// <summary>Up to maxLines distinct shortest-first press lists from `from` to `to`, each checked in the simulator and with Fits.</summary>
    public static IReadOnlyList<IReadOnlyList<Press>> Connect(long start, string to, Buttons buttons, Limits limits,
                                                               int maxLines, CancellationToken cancel = default);

    /// <summary>MaxDigits = min(18, max(len S, len G) + 3), MaxStates = 200 000.</summary>
    public static Limits DefaultLimits(string s, string g, int maxPresses);
}
```

### Forward

Plain BFS over display strings. `parent: Dictionary<string, (string? Prev, Press? Press, int Depth)>`. Neighbours of a display w, in this order (so ties come out like `LeftEdits`: appends first):

1. `Xd` for each append digit: w + d
2. `dX` for each prepend digit: d + w
3. `flip`: reverse(w)
4. `reset`: "0"
5. each transform t: `t.Apply(w)` (skip null)

Skip a neighbour when it is longer than `MaxDigits`, already in `parent`, its depth would exceed `MaxPresses`, or the press would use a button more often than its charges **along this path**. To check charges cheaply, keep a per-state usage map only for buttons with charges < `MaxPresses` (in practice a small `Dictionary<string, int>` copied on enqueue; paths are at most 12 long). Stop when `parent.Count` reaches `MaxStates`. Check `cancel` every 4096 dequeues.

Known limitation: states are keyed by display only, so when the shortest route to a display spends a scarce button, a longer route that saves it isn't kept, and a line that needs the saved charge later can be missed. Accepted for v1. Possible fix later: keep up to K usage profiles per display.

### Backward

The same BFS from `to`, using predecessors instead of successors:

1. `Xd` d: if w ends with d and w.Length ≥ 2 → w[..^1]
2. `dX` d: if w starts with d and w.Length ≥ 2 → w[1..]
3. `flip`: reverse(w)
4. `reset`: none (a constant, like `to25`)
5. each transform: `t.Preimages(w)`, skipping any longer than `MaxDigits`

Paths are stored as "presses from this display to `to`" (prepend the press when moving back). The same charge check and caps apply. Constants (`reset`, `to25`) have no predecessors: any line that ends with "constant, then edits" is never cheaper than pressing that constant first, so the forward search covers them.

### Connect

Meet in the middle on the display:

```
f = Forward(S, limits with MaxPresses = ceil(c / 2))
b = Backward(G, limits with MaxPresses = c - ceil(c / 2))     // c = limits.MaxPresses
for each display w in both: candidate = f[w] + b[w]
also: if f contains G, candidate f[G]           (covers paths whose backward half was cut by MaxStates)
sort candidates by (cost, press text); drop duplicates by press text
keep the first maxLines that pass Run(start, presses, buttons)[^1] == G and buttons.Fits(presses)
```

The join can pair two halves that are each within charges but together aren't, which is why `Fits` is checked at the end. Add `LeftEdits.Plan(S, G)` as one more candidate, so `Connect` is never worse than today's edits-only line.

## 6. `ArithmeticSearch.Find` changes

When `buttons.Transforms.Count == 0`, nothing changes (same code path, same lines).

When there are transforms:

1. **Edits-only:** replace the `Plan(s, g)` block with `LeftSearch.Connect(start, g, buttons, limits, maxLines: 10, cancel)` and `Keep` each.
2. **X candidates:** after building `xs` as today, add every entry of `LeftSearch.Forward(s, buttons, limits with MaxPresses = maxCost - 2)` whose display parses as a number and whose path length + 2 ≤ maxCost. Keep at most `MaxBfsX = 3000` of them, cheapest first (ties by press text). Skip displays already in `xs` with a path that isn't longer.
3. **Y candidates:** after building `ys` as today, add every entry of `LeftSearch.Backward(g, buttons, limits with MaxPresses = maxCost - 2)` whose display is a plain number (no leading zero, since a result after `=` never has one) and whose path length + 2 ≤ maxCost. Keep at most `MaxBfsY = 3000`, cheapest first. Skip values already in `ys` with a path that isn't longer.
4. The pair loop, the candidate structs, the cost-level loop and `Keep` stay as they are. `Keep` already checks the simulator and `Fits`, which also catches a pre/post pair that together exceed a charge.

`xs × ys × 3` can now be about 3000 × 3000 × 3 ≈ 27 million cheap checks. That's fine single-threaded if the quick bound (`d` digit count vs budget) stays first. If it's too slow, lower `MaxBfsX` and `MaxBfsY` first.

Use `LeftSearch.DefaultLimits(s, g, maxCost)` for the limits.

## 7. `MainForm`

No change needed: `Buttons.Parse` accepts the new tokens and the game feed sends them. The default Buttons text stays as it is.

## Test plan

New `LeftTransformTests.cs`:

- Apply: `35` `3->0` → `05`; `05` `0->8` → `85`; `021` `X++` → `22`; `0` `X--` → `0`; `5` `X-10` → `0`; `55` `X/2` → `27`; `55` `>>` → `27`; `6` `<<` → `12`; `41` `X*2` → `82`; `9` `sqr` → `81`; `10` `sqrt` → `3`; `99` `sqrt` → `10`; `6` `sqrt` → `2`; `7` `sqrt` → `3`; `100` `sqrt` → `10`; `54` `round` → `50`; `55` `round` → `60`; `50` `round` → `50`; `4` `round` → `0`; `95` `round` → `100`; `41` `to25` → `25`; `1000000000` `sqr` → null; `999999999999999999` `X++` → null.
- Preimages property: for each transform in the catalogue (`X++`, `X--`, `X+16`, `X-4`, `X*2`, `X/2`, `<<`, `>>`, `sqr`, `sqrt`, `round`, `2->4`, `3->0`) and w in 0…2000 plus a few strings with leading zeros (`05`, `0042`), every preimage p satisfies `Apply(p) == w`. Spot checks: `X--` of `0` = {0, 1}; `round` of `60` = {55…64}; `2->4` of `44` = {44, 24, 42, 22}; `2->4` of `24` = {}.
- Parse: `X++`, `x++`, `X+16`, `x−4` (unicode minus), `2->4`, `TO25` parse to the canonical tokens; `X+0`, `X+04`, `3->3`, `X*3` → null.

`EngineeringCalculatorTests` (new cases):

- `35`, `3->0 5X` → `05`, `505`; `35`, `3->0 flip` → `05`, `50`; `120`, `flip X++` → `021`, `22`.
- `41`, `÷ X++` → null (right side). `X++` missing from buttons → null.
- `999999999`, `sqr` → `999999998000000001`; `1000000000`, `sqr` → `max digits reached`, then any press → null.

Parse tests: `X++:2, X--:1, X+16:1, X-64:1, X*2:1, X/2:1, <<:1, >>:1, sqr:1, sqrt:1, 2->4:1, to25:1, round:1` parse with charges keyed by the canonical tokens; `X++:0` leaves it out; `X++` without a count → error; `x4:1` is still an append; `+:1` is still the operator.

`GameLinkTests`: the ids in requirements' table map to the tokens; `operator_+10` → `X+10`; `operator_-64` → `X-64`; `operator_0to8` → `0->8`; `operator_last`, `operator_100-x`, `operator%` → null.

New `LeftSearchTests.cs`:

- `Forward` from `41` with `X++:9` and MaxPresses 2 → contains `42` (1 press) and `43` (2 presses), not `44`.
- `Backward` to `84` with `X*2:9` → contains `42` with `[X*2]`.
- Charges: `Forward` from `41` with `X++:1` never reaches `43`.
- `Connect`: 41 → 42 with `X++:1` → `[X++]`; 7 → 49 with `sqr:1` → `[sqr]`; 35 → 505 with `3->0:1, 5X:1` → `[3->0, 5X]`; 41 → 84 with `X++:1, X*2:1` → `[X++, X*2]`; 41 → 25 with `to25:1` → `[to25]`; 55 → 27 with `>>:1` → `[>>]`; 41 → 43 with `X++:1` → empty, with `X++:2` → `[X++, X++]`.
- Every returned line runs to G in the simulator and fits the charges. A cancelled token throws.

`ArithmeticSearchTests` (new cases):

- 12 → 97 with `×:1, 8:1, X++:1` → a line `× 8 = X++`, cost 3.
- 41 → 42 with `X++:1, +:1, 1:1` → the top line is the edits-only `X++` (cost 1).
- No transforms: all existing arithmetic tests pass unchanged.

Timing (manual, Release): `ArithmeticSearch.Find` for 41 → 483 with `Buttons.All` plus every transform. Target under 1 s. Record the number in "As built".

Manual (`dotnet run --project CalculatorPuzzle`):

- S 41, G 84, Buttons `X++:2, X*2:2`, Enter: `X++ X*2` (2 presses) at the top.
- S 12, G 97, Buttons `×:2, 8:2, X++:2`, Enter: `× 8 = X++` appears.
- With the game feed: a round that has `X--`, `X+2` or a digit map fills them in with charges.

## As built

Everything in the design was built as written, with these differences:

- **Limits.** `DefaultLimits` uses `MaxStates = 100 000` (design: 200 000), and `MaxBfsX = MaxBfsY = 200` (design: 3000). With the design values, 41 → 483 with every button plus every transform took about 5 s in Release (9950X3D); the pair loop dominated. Now it takes about 0.9 s (about 0.26 s without transforms), and 12 → 97 and 41 → 84 take about 0.85 s.
- **`Find` runs one forward and one backward search**, to depth `max(maxCost - 2, ceil(maxCost / 2))`. The same two maps give the edits-only lines (through the internal `LeftSearch.Join`, which `Connect` also uses) and the extra X and Y candidates, instead of running `Connect` and two more searches.
- **`LeftTransform.Ascending`** (new): true for `sqrt` and `X-k` preimages, which are yielded in ascending order. The backward search stops at the first one that is too long. Without it, `sqrt` preimages of a large value (about 2v numbers) hung the search.
- `Join` also drops empty lines (S = G).
- The old test `Unknown_plus_two_stays_unmapped` was removed: `operator_+2` now maps to `X+2` by design.
- Manual UI checks were not run in the window; the same cases are unit tests (`ArithmeticSearchTests`: 41 → 84, 12 → 97, 41 → 42).
