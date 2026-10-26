# Design: Prime button (`operator_prime`)

**Status:** Complete (2026-10-24). Test I confirmed in-game.

**Corrected 2026-10-25:** the `p < Limit` (10^18) bound below doesn't match the game, which has no `max digits reached`. `p` isn't clamped (`999999` → `1000003`); other transforms clamp at 999 999. See [`display-cap/`](../display-cap/).

## Overview

One new entry in the transform catalogue (`LeftTransforms`), one new mapping (`operator_prime` → `p`) in the app and the mod, and a live `p<number>` symbol in the calculator overlay. The search code doesn't change: `LeftSearch` already uses any transform through `Apply` and `Preimages`.

## App

### `CalculatorPuzzle/Primes.cs` (new)

```csharp
namespace CalculatorPuzzle;

/// <summary>Primality and nearest prime for 0 ≤ v &lt; 2^63, as the game's operator_prime button computes it.</summary>
public static class Primes
{
    /// <summary>Deterministic Miller–Rabin (bases 2..37 cover every 64-bit number).</summary>
    public static bool IsPrime(long v);

    /// <summary>MainCalc.FindNearestPrime: v &lt; 2 → 2; else check v−d before v+d for d = 0, 1, 2, … (ties go down).</summary>
    public static long Nearest(long v);

    /// <summary>The largest prime below p, or null for p ≤ 2.</summary>
    public static long? Previous(long p);

    /// <summary>The smallest prime above p.</summary>
    public static long Next(long p);
}
```

- `IsPrime`: small cases (`< 2`, even, divisible by a prime ≤ 37), then Miller–Rabin with bases 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, using `UInt128` for the modular multiply.
- `Nearest` follows the game's loop order exactly (lower candidate first).
- Prime gaps below 10^18 are under 1 500, so each call tests at most a few hundred numbers.

### `LeftTransforms.Create`

```csharp
case "p":
    return Number("p", v => Primes.Nearest(v) is var p && p < Limit ? p : null, PrimePreimages, ascending: true);
```

```csharp
/// <summary>Every v whose nearest prime (ties down) is p: ((q + p) / 2, (p + r) / 2], or 0..(2 + 3) / 2 for p = 2.</summary>
static IEnumerable<long> PrimePreimages(long p)
{
    if (!Primes.IsPrime(p))
        yield break;
    long from = Primes.Previous(p) is { } q ? (q + p) / 2 + 1 : 0;
    long to = (p + Primes.Next(p)) / 2;
    for (long v = from; v <= to && v < Limit; v++)
        yield return v;
}
```

`Number(...)` already rejects a display with a leading zero for preimages and parses the display as a number for `Apply` (leading zeros drop).

Check the tie rule on both ends: `v = (q + p) / 2` exactly (even sum) is a tie between `q` and `p` and goes to `q`, so it is excluded; `v = (p + r) / 2` is a tie between `p` and `r` and goes to `p`, so it is included.

### `GameLink.MapButton`

Add `"operator_prime" => "p"`.

`Buttons.Parse` needs no change: `p` reaches the `LeftTransforms.Parse(token)` branch like `round`.

## Mod

### `GameHook.MelonLoader/ButtonSymbols.cs`

- `Token`: `case "operator_prime": return "p";`
- New `public static long NearestPrime(long v)`: same rule as `Primes.Nearest`. net472 has no `UInt128`, so write the modular multiply as double-and-add on `ulong` (operands below 2^63, so `a + a` can't overflow `ulong`). Plain C# so the test project can link it.
- New overload `public static string Of(string id, int uses, int digit, long left)`: `operator_prime` → `"p" + NearestPrime(left)`; otherwise `Of(id, uses, digit)`.

### `GameHook.MelonLoader/CalcHookCore.cs`

- `SymbolOf(ButtonInfo b)`: add `operator_prime` to the live keys. Pass the left side (`_calc.total`):

```csharp
// The charge, quest and prime keys show a live value ("c12", "q8", "p8081"), so they skip the per-id cache.
string SymbolOf(ButtonInfo b, long left) =>
    b.Key == "number_charge" || b.Key == "number_quest" || b.Key == "operator_prime"
        ? ButtonSymbols.Of(b.Key, b.Uses, b.Digit, left) : SymbolOf(b.Key);
```

- Cache the prime: `long _primeFor = -1, _prime;` and a helper so `OnGUI` (several calls per frame) computes it once per left value. Simplest: put the cache inside the overload's caller (`PrimeSymbol(long left)`), not in `ButtonSymbols` (keep that class stateless).
- `SymbolFontSize` already shrinks long text (`p1000003` is 8 characters → 11 pt).
- The shop overlay calls `SymbolOf(sl.Key)` (no live value), so an offer shows `p`.
- Read `_calc.total` once per `DrawOverlay`; `_calc` is already the `MainCalc` the mod reads.

## Tests

### `CalculatorPuzzle.Tests/PrimeTests.cs` (new)

| Test | Check |
|------|-------|
| `Nearest_matches_game_tests` | `[InlineData]` rows: 8084→8081, 41→41, 93→89, 33→31, 55→53, 88→89, 6→5, 80→79, 0→2, 1→2, 2→2, 999999→1000003 |
| `IsPrime_matches_trial_division` | every v in 0..100 000 against plain trial division |
| `IsPrime_matches_game_float_loop` | a port of the game loop (`(float)i <= MathF.Sqrt((float)n)`) for p² with p prime in 4 097..50 000, and p·(p+2) for twin primes in that range: same answer as `IsPrime` |
| `IsPrime_large` | known primes and composites near 10^12, 10^17, `long.MaxValue` region (for example 999 999 999 989 is prime, 10^17 + 3 is prime, 10^17 + 1 is not) |
| `Preimages` | `p` transform: `2` ← `0,1,2`; `3` ← `3,4`; `5` ← `5,6`; `89` ← `87..93`; `8081` ← `8076..8084`; `90` ← none; `089` ← none |
| `Apply_drops_leading_zero` | `"05"` → `"5"` |
| `Apply_preimage_roundtrip` | for every prime p ≤ 10 000, every preimage maps back to p, and v − 1 below the range and v + 1 above it don't |

### `LeftSearchTests`

- 88 → 89 with `p:1` finds `p` (1 press).
- 120 → 113 with `p:1` finds `p` (tie goes down).
- 42 → 43 with `p:1, X++:1`: the line is `X++`, and no line is the single press `p` (it gives 41). Tie regression test.

### `GameLinkTests` / `ButtonSymbolsTests`

- `MapButton("operator_prime")` is `p`.
- **Existing tests use `operator_prime` as their "unmapped button" sample** (`GameLinkTests` lines 17, 33, 49, 60, 102, 103). Switch them to another unmapped id, such as `operator_near` (`near`).
- `ButtonSymbolsTests`: the mirror test already lists `operator_prime`; it now expects `p`. Add `Of("operator_prime", 1, -1, 8084)` = `p8081`, and `NearestPrime` equals `Primes.Nearest` for every v in 0..20 000 and a few large values (the test project links `ButtonSymbols.cs`).

## Manual test plan (`user-verification/test-i.md`, new "Test I: Prime button")

1. Rebuild and reinstall the mod. A run with `operator_prime` (buy it, or use a run that has it).
2. **I1.** The calculator overlay (F9 if not in challenge 9) shows `p<number>`, and the number is the one in the game's hover text `(...)`. It changes when the left side changes.
3. **I2.** In challenge 9, a shop offer of `operator_prime` shows `p`.
4. **I3.** The app: Follow game fills `p:N` in Buttons; lines with `p` pass the simulator check, and following one (then `=`) wins the round.

Already confirmed by the user (2026-10-24), no test needed: `p` works on the left side only, and the round is won after `=`.

## Risks

- Very large left sides (10^17) cost a few hundred Miller–Rabin tests per `Apply` in the BFS. Fine at the BFS's 100 000-state cap.
- `PrimePreimages` for a large prime walks up to ~750 numbers; the backward search already handles wide preimage sets (`sqrt`, `100-X`).

## As built

- As designed: `Primes.cs`, the `p` entry and `PrimePreimages` in `LeftTransforms`, `operator_prime` → `p` in `GameLink.MapButton` and `ButtonSymbols.Token`, `ButtonSymbols.NearestPrime` (double-and-add `ulong` Miller–Rabin for net472) and the `Of(id, uses, digit, left)` overload.
- `CalcHookCore`: `SymbolOf(ButtonInfo, long left)` plus `PrimeSymbol`, which caches the prime per left value. The left side is stored in `_overlayLeft` when the overlay refreshes, because `DrawCalculator` has no `left` of its own.
- Tests: 483 pass (30 new): `PrimeTests`, `PrimeTransformTests`, preimage and `Apply` cases, `LeftSearchTests` (88 → 89, 120 → 113, 42 → 43), `ButtonSymbolsTests`. The `GameLinkTests` "unmapped" samples now use `operator_near`.
- Test I (I1–I3) confirmed in-game by the user, 2026-10-24.
