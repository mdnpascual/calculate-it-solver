# Design: blind passive and the empty count

**Status:** Complete (2026-10-29). `Wrap` in both searches already got the round-start buttons (Blind on): `MainForm` passes `buttons` (unflipped) to `SwapSearch.Wrap` and `origButtons` to `CoinSubSearch.Wrap`, so no code change there.

Read `requirements.md` first.

## Overview

| Part | Change |
|------|--------|
| `EngineeringCalculator.cs` (`Buttons.Drained`) | Skip one charge for the free press when `Context?.Blind == true` |
| `ResultLine.cs`, `SwapSearch.cs` (`Wrap`), `CoinSubSearch.cs` (`Wrap`) | No code change if they pass the round-start buttons; check each (below) |

No mod, `GameLink` or UI change. `Blind` is already in `LineContext`.

## `Drained`

Today:

```csharp
presses.Where(p => p.Key != Key.Equals).GroupBy(p => p.ChargeKey)
       .Count(g => Charges.TryGetValue(g.Key, out var n) && g.Count() >= n);
```

New: find `freeToken` once, the `ChargeKey` of the first press that is not `=` and not in `Glass`, only when `Context?.Blind == true` (else null). Then per group: `used = g.Count() - (g.Key == freeToken ? 1 : 0)`; count the group when `used >= n`. Pass `Blind` through the existing `Context`; don't add a parameter, so the five `ResultLine.From` overloads stay as they are.

## Which `buttons` each caller passes

- `ResultLine.From(...)`: the buttons of the search that made the line. In rule 14 `GoalStepSearch` uses `WithGoal(...)`, which keeps the context, so `Blind` is there.
- `SwapSearch.Wrap`: recounts `Drained` over `[swap] + inner` with the `buttons` it is given. `Flip` clears Blind for the flipped leg, which is right for the inner search and wrong for the whole line. Check that `Wrap` gets the unflipped buttons (Blind on); if it gets the flipped ones, pass the original in.
- `CoinSubSearch.Wrap`: recounts over `Coins(n) + inner.Presses`. The rest buttons carry `Blind = BlindLeft`; the whole-line recount needs the round-start Blind (the first coinsub press is the free one when `n ≥ 1`). Check which instance `MainForm` passes and use the round-start one. A glass coinsub passes the free press to the next non-glass press; `Drained` handles that because it looks for the first non-glass press.

## Tests (`BlindEmptyTests.cs`, or in `BlindTests.cs`)

1. Blind off, `p:1`, line `p`: `Drained` 1 (unchanged).
2. Blind on, `p:1`, line `p`: `Drained` 0.
3. Blind on, `X++:2`, line `X++ X++`: 0. `X++:1, X8:1`, line `X++ X8`: 1.
4. Glass first: `X1:1g, X8:1`, line `X1 X8`, Blind on: `Drained` 1.
5. `Ranking`: two equal-cost lines, one whose only 1-charge press is the free one; it sorts first with Blind on, and ties as before with Blind off.
6. Swap: `swap:1, X++:1`, line `swap X++`, Blind on: `Drained` 1 (through `SwapSearch.Wrap`).
7. Coinsub: a line with a `coinsub` prefix, Blind on: the first coinsub press is free (through `CoinSubSearch.Wrap`).
8. `ToString` of the test 2 line has no `empty`.

`dotnet test`: all existing tests pass. Re-check any test that asserts `empty N` with Blind set instead of loosening it.

## Docs

- `CalculatorPuzzle/README.md`: the `EngineeringCalculator.cs` note (`Drained`) and, if changed, the `SwapSearch.cs` / `CoinSubSearch.cs` notes.
- `spec/README.md`: the line for this spec (status when done).
- `user-verification/test-ac.md` plus a row in `user-verification/README.md`: in game with the passive, a Buttons box where a 1-charge button solves the round, Blind ticked: the line has no `empty`; untick Blind: `empty 1`. Press the line and confirm the button keeps its charge.
