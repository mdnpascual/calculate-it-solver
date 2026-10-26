# Design: Digit keys

**Status:** Done.

**Correction (2026-10-22):** digit keys work on the right side only. The left-side parts of this design (the simulator appending to the display, digit keys in `LeftEdits`, digit keys in A's digits with `flip`) were wrong and are struck below; the fix is specced in [digit-keys-right-side-only](../digit-keys-right-side-only/requirements.md).

## Where code goes

- `CalculatorPuzzle/EngineeringCalculator.cs`: a new `Key.Digit`, `Buttons.DigitKeys`, parsing of `0`–`9`, and digit keys in `Run`.
- ~~`CalculatorPuzzle/LeftEdits.cs`: digit keys as appends on the left side.~~ (Wrong: digit keys don't work on the left side.)
- `CalculatorPuzzle/DivisionShortcut.cs`: typing cost for D, and a second search path that picks D from X.
- `CalculatorPuzzle/MainForm.cs`: the shorter line format and no search at startup.
- Tests in the three existing test files (`EngineeringCalculatorTests.cs`, `LeftEditsTests.cs`, `DivisionShortcutTests.cs`).

## Types

```csharp
public enum Key { Prepend, Append, Ten, Digit, Reset, Flip, Divide, Equals }

public record Press(Key Key, int Digit = -1)   // Digit used by Prepend, Append and Digit
{
    // Key.Digit prints as the bare digit: "3".
}

public record Buttons(IReadOnlySet<int> PrependDigits, IReadOnlySet<int> AppendDigits,
                      IReadOnlySet<int> DigitKeys,
                      bool Ten, bool Reset, bool Divide, bool Mod, bool Flip)
```

- `Buttons.All` gets every digit key too.
- `Buttons.Parse`: a token matching `^\d$` adds to `DigitKeys`. `10` still sets `Ten`. Anything else that's all digits (`25`, `100`) is an unknown button, as now.
- Adding a positional parameter breaks callers that use `new Buttons(...)` directly. Fix those call sites (tests) by passing an empty set.

## Simulator (`EngineeringCalculator.Run`)

Add one case, mirroring `Key.Ten`:

```csharp
case Key.Digit when press.Digit is >= 0 and <= 9
        && (buttons == null || buttons.DigitKeys.Contains(press.Digit)):
    // append to operand when pending; invalid (return null) otherwise
```

## Left edits (`LeftEdits.Plan`)

~~The digit key `d` is an append on the left side. Where the search tries `Xd` for the right-end digit r, also try `Key.Digit` when `AppendDigits` lacks r but `DigitKeys` has it. Prefer `Xd` when both exist, so lines that worked before print the same.~~

~~Also, in `DivisionShortcut`, the digits allowed in A with `flip` become `PrependDigits ∪ AppendDigits ∪ DigitKeys`.~~

(Wrong: digit keys can't be pressed on the left side, so `LeftEdits` doesn't use them, and A's digits with `flip` are `PrependDigits ∪ AppendDigits`.)

## Typing D (`DivisionShortcut`)

```csharp
// Fewest presses of the operand keys (10 and DigitKeys) that spell `text`, or null.
static IReadOnlyList<Press>? TypeOperand(string text, Buttons buttons)
```

Left-to-right DP over the string: at position i, take one digit key if `text[i]` is in `DigitKeys`, or `10` if `text[i..i+2] == "10"` and `Ten`. Return the presses of the cheapest split. Rejects text starting with `0`.

```csharp
// The cheapest typeable D with low <= D <= high and D >= 2, or null. Ties go to the smaller D.
static (long Divisor, IReadOnlyList<Press> Presses)? CheapestDivisor(long low, long high, Buttons buttons, int maxCost)
```

Depth-first search over digit strings, by length from digits(max(low, 2)) up to digits(high):

- Build D one key at a time (a single digit key or `10`), so the cost is known as it grows.
- Prune a prefix when no number of the current length that starts with it lies in [low, high] (compare the prefix padded with 0s and with 9s against the bounds).
- Prune when the cost so far plus `ceil(remaining digits / 2)` can't beat the best found or exceeds `maxCost`.
- Stop trying longer lengths once `ceil(length / 2)` can't beat the best.

## Algorithm changes (`DivisionShortcut.Solve`)

1. **Validate.** The shortcut needs `÷` and at least one operand key: `10` or any digit key. New error text: "The Division Shortcut needs ÷ and 10 or a digit key."
2. **Path A, `10` divisors (unchanged).** Runs only if `Ten`. Keeps the existing loop over r, quotients, cores, suffixes and the formula's A range. With no digit keys, this is the only path, so the results match today.
3. **Path B, X first (new).** Runs only if `DigitKeys` is non-empty:
   - For each quotient q (with its post edits), core, suffix T (up to 2 digits, as now) and prefix A of 0 to `MaxFreePrefixDigits = 2` digits from the allowed prefix digits (no leading `0`):
     - W = A ‖ core ‖ T, X = value of W. Pre edits = `LeftEdits.Plan(S, W)`; skip if null.
     - Budget left for D = `maxPresses − pre − 1 − post`. Skip if below 1.
     - D range: low = floor(X / (q + 1)) + 1, high = floor(X / q). Skip if low > high.
     - `CheapestDivisor(low, high, buttons, budget)`; skip if null.
     - Presses = pre, `÷`, D's presses, `=`, post. Check with `Run` as before.
   - Why A is capped at 2 digits here: with free digit keys, a longer A only makes X and so D longer, which costs more. Path A still covers long A for `10`-only divisors (where the formula needs it, like 48541 ÷ 1010).
4. **Rank and dedupe** across both paths as now (cost, non-flip presses, D, then press text). The `Divisor` field holds D's value either way.

Path B's size: quotients × cores × 111 suffixes × up to 111 prefixes, each with a cached `Plan` and a small divisor search. Measure it on the requirements' examples. If it's much slower than Path A, cache `Plan` by W (it already is) and memoize `CheapestDivisor` by (low, high).

## UI (`MainForm`)

- **Line format.** `Describe(DivisionShortcut.Line)` returns `$"{presses}   ({line.Cost} presses)"`. Drop the `goal` parameter.
- **Startup.** Remove the `RecalculateDivision()` call from the constructor and call `MarkDivisionStale()` instead. Keep `Recalculate()` for the Modulo Shortcut.

The UI isn't unit tested. Check it by running the app.

## Test plan

`ButtonsParseTests`:

- `3, 7, 10, ÷` gives digit keys {3, 7}, `Ten`, `Divide`, and no prepends or appends.
- `X3` and `3` are different: `X3` adds to appends only, `3` to digit keys only.
- `25` and `100` give an unknown-button error.
- The default text parses as before, with no digit keys.

`EngineeringCalculatorTests`:

- Digit keys are invalid on the left: `41`, `3` makes `Run` return null.
- Digit keys build the operand: `531`, `÷`, `4`, `5` → operand `45`, `=` → `11`.
- Mixed: `÷`, `10`, `1` → operand `101`.
- A missing digit key makes `Run` return null.
- `Press(Key.Digit, 3).ToString()` is `"3"`.

`LeftEditsTests`:

- `Plan("4", "48")` with only the digit key `8` is null.
- With both `X8` and the key `8`, the plan is `X8`.

`DivisionShortcutTests`:

- Every row of the requirements' examples table, with that row's buttons, gives that cheapest line and press count. The "None" row returns the "no line fits" error.
- `TypeOperand`: `1010` with `10` is 2 presses; `100` with `10` and `0` is 2 (`10 0`); `45` without `5` is null; `05` is null.
- `CheapestDivisor`: (155, 176) with keys `1`, `7` and `10` is 171; a range with nothing typeable is null; a range containing 1 never returns 1.
- With no digit keys, the lines for every existing example match the existing tests exactly (the existing tests already cover this; they must still pass unchanged).
- Every returned line, run through the simulator with the given buttons, ends on G.
- Brute-force check: for random X, q and full digit keys, the chosen D gives floor(X / D) = q.

## As built

- `Solve` validation now says "The Division Shortcut needs ÷ and 10 or a digit key." Three existing assertions in `DivisionShortcutTests` that checked the old message text were updated to the new one. No other existing test changed.
- `CheapestDivisor` is a depth-first search that prunes by range and by cost, with a per-`Solve` cache keyed by (low, high, budget). Ties on cost go to the smaller D.
- Path B's prefix A is built from `PrependDigits` (plus `AppendDigits` and `DigitKeys` when `flip` exists), 0 to 2 digits. Including `DigitKeys` was a bug, removed by [digit-keys-right-side-only](../digit-keys-right-side-only/requirements.md).
- Timing (Debug build, all buttons, 12-press limit): 41 → 483 went from 12.4 s to 13.6 s, and 531 → 117 from 8.1 s to 10.7 s.
