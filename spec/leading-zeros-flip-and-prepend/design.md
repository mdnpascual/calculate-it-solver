# Design: `flip` and `dX` drop leading zeros

**Status:** Done (2026-10-24).

**Corrected 2026-10-25:** "at most 18 long" below is the solver's limit, not the game's: the game's display holds 6 characters and results clamp at 999 999. See [`display-cap/`](../display-cap/).

## Overview

The left side stays a display string; that's still right for `Xd` and the digit maps, which edit the text. Two shared helpers on `EngineeringCalculator` give the number-reading behaviour of `flip` and `dX`. The simulator and `LeftSearch` (forward) call them, so there is one definition. `LeftEdits` works on pieces of the target, not strings, so it gets the same rule in its own terms. `ModuloSearch`, `ModuloShortcut`, `DivisionShortcut` and `ArithmeticSearch` need no code change.

## 1. `EngineeringCalculator.cs`

Add next to `Cost`:

```csharp
/// <summary>dX: the game prepends to the number, so leading zeros drop first ("04" + 1X = "14"; "0" + 1X = "10").</summary>
public static string Prepend(string display, int digit) => digit + long.Parse(display).ToString();

/// <summary>flip: the game reverses the number and the result is a number, so leading zeros drop on both sides ("120" → "21", "04" → "4").</summary>
public static string Flip(string display) =>
    long.Parse(new string(long.Parse(display).ToString().Reverse().ToArray())).ToString();
```

In `Run`:

- `Key.Prepend`: `display = Prepend(display, press.Digit);` (was `press.Digit + display`)
- `Key.Flip`: `display = Flip(display);` (was the string reverse)

`long.Parse` is safe here: the left-side display is always digits, at most 18 long (`LeftSearch` caps the length at `MaxDigits` ≤ 18, and `Run` stops at max digits).

## 2. `LeftSearch.cs`

### Forward

```csharp
Add(new Press(Key.Prepend, d), w => [EngineeringCalculator.Prepend(w, d)]);
...
Add(new Press(Key.Flip), backward ? FlipPreimages : w => [EngineeringCalculator.Flip(w)]);
```

### Backward

- **`dX`**: w comes from `dX` only if `w[0] == d` and the rest is a plain number (no leading zero, or exactly `0`). The preimage is the rest:
  `w => w.Length >= 2 && w[0] == c && IsPlain(w[1..]) ? [w[1..]] : []`
- **`flip`**: `flip` always returns a plain number that doesn't end in 0 (except `0`). So:

```csharp
/// <summary>
/// Every display that flip turns into w, as far as the backward search lists them. flip's result is a
/// number that doesn't end in 0 (except "0"), so w must be plain and not end in 0. Only reverse(w) is
/// listed: reverse(w) followed by 0s also flips to w, but listing those fills the state limit (Old
/// Calculator benchmark 0.4 s → 16 s), so flips that drop zeros are found by the forward half only.
/// Like the number transforms, no leading-zero displays are listed; Join matches those by value.
/// </summary>
static IEnumerable<string> FlipPreimages(string w)
{
    if (!IsPlain(w) || (w != "0" && w[^1] == '0'))
        yield break;
    yield return w == "0" ? "0" : new string(w.Reverse().ToArray());
}

/// <summary>No leading zero ("0" itself is plain).</summary>
static bool IsPlain(string w) => w.Length == 1 || (w.Length > 1 && w[0] != '0');
```

(`LeftTransforms` has a private `IsPlain` with the same body. Making that one `internal` and reusing it is fine too.)

### Join by value

Backward preimages are plain numbers only, as the number transforms' `Preimages` already are. A forward display with leading zeros (`05`) must still meet a backward path that starts with a number-reading press (`5X` from `5`). `Join` gets a second lookup:

```csharp
foreach (var (display, front) in f)
{
    if (b.TryGetValue(display, out var back) && front.Count + back.Count <= maxCost)
        candidates.Add([.. front, .. back]);
    // Backward lists plain numbers only, so a display with leading zeros also meets the entry
    // for its number; the simulator check below drops the joins where the next press reads the text.
    if (display.Length > 1 && display[0] == '0'
        && b.TryGetValue(display.TrimStart('0') is { Length: > 0 } plain ? plain : "0", out var byValue)
        && byValue.Count > 0 && front.Count + byValue.Count <= maxCost)
        candidates.Add([.. front, .. byValue]);
}
```

Every candidate already goes through `EngineeringCalculator.Run` and `Fits`, so a by-value join whose next press is `Xd` or a digit map (which read the text) is dropped there. That makes this lookup safe without telling text buttons apart from number buttons, so buttons stay data.

### Why not list leading-zero and trailing-zero preimages

Both were tried in the scratch copy:

| Backward `flip` / `dX` preimages | Old Calculator benchmark (Release) | 41 → 483 every button |
|---|---|---|
| plain only + Join by value (chosen) | 366 ms | 630 ms, 21 lines |
| + `reverse(w)` followed by one 0 | 4.4 s | 600 ms, 21 lines |
| + any number of trailing 0s and leading-zero variants | 17.8 s | 634 ms, 23 lines |
| before the fix | 341 ms | 660 ms, 21 lines |

All four give the same mean (4.58) and max (12) cost on the 208 Old Calculator pairs. The extra preimages multiply the backward states until the 100 000-state limit is hit. The gap they would close is narrow: a zero-dropping `flip`, or a leading-zero display followed by a number-reading press, both inside the backward half. That's a possible follow-up (for example, only generating them when the puzzle has `X0`, a `→0` digit map or `reset`).

Update the class summary: "Displays are strings, so leading zeros are kept" becomes "Displays are strings: `Xd` and digit maps keep leading zeros; `dX`, `flip` and the number transforms read the number and drop them."

## 3. `LeftEdits.cs`

States are pieces `W[i..j)` of the target (or their reverse). Two changes inside the BFS loop:

```csharp
// dX drops the display's leading zeros first, so it only extends a display without one (or "0").
bool leadingZero = s.J - s.I > 1 && (s.Reversed ? to[s.J - 1] : to[s.I]) == '0';
if (leftDigit is { } l && !leadingZero && buttons.PrependDigits.Contains(l))
    Go(...prepend as before...);
if (buttons.Flip)
    Go(Flipped(s), new Press(Key.Flip));
```

```csharp
// flip reverses the number: zeros at both ends of the piece drop (one 0 stays for an all-zero piece).
State Flipped(State s)
{
    int i = s.I, j = s.J;
    while (j - i > 1 && to[i] == '0') i++;
    while (j - i > 1 && to[j - 1] == '0') j--;
    return new State(i, j, !s.Reversed);
}
```

Why trimming both ends is right: the display is `W[i..j)` or its reverse. `flip` drops the display's leading zeros, reverses, and drops the new leading zeros (the old trailing ones). Either way, the zeros at both ends of the piece go, and the reversed flag toggles.

The seeds are unchanged: `from` is a start number or a quotient, so it never has a leading zero.

Update the method summary: dX can't extend a piece with a leading zero; flip drops zeros at the piece's ends.

## 4. Modulo Shortcut

- `ModuloShortcut.cs`: code unchanged. Fix the class comment: "Extra places come from prepending 0s to S first (leading zeros are kept)" becomes "Extra places (k > digits of S) need zeros between the prepended digits and S. `0X` can't make them (`dX` drops leading zeros); `flip X0 … flip` can (`31` → `13` → `130` → `1304` → `4031`)."
- `ModuloSearch.cs`: code unchanged. `Presses` builds the target `Prepend + S.PadLeft(k, '0')` and asks `LeftEdits.Plan`, which now returns null for `0X` padding and finds the `flip` route when the buttons allow it. `LowerBound(k)` = `(k − len S) + 1 + 1 + operand` still holds: the `flip` route costs `(k − len S)` zeros + the prepend digits + 2 flips.

## 5. Docs

- CLAUDE.md, the Engineering Calculator table:
  - `flip` row: "Reverse the number: `41` → `14`, `123` → `321`, `120` → `21` (leading zeros drop; then `1X` → `121`)".
  - The sentence below the table: "Leading zeros on the left side stay while editing and drop when an operator or `=` is pressed (`021` becomes 21)" becomes "`Xd` and digit maps edit the display text, so leading zeros stay (`0` `X4` = `04`, `35` `3->0` = `05`). `dX`, `flip`, the number transforms and the operators read the number, so they drop them (`04` `1X` = `14`, `04` `flip` = `4`)."
- CLAUDE.md, the `ModuloShortcut.cs` bullet: drop "the display keeps leading zeros, so `0` `X4` = `04`, then `1X` = `104`" and say padding needs `flip X0 … flip`.
- CLAUDE.md, the `spec/` list: mark this spec complete.

## Test plan

Change the 8 tests that encode the old rule (new names in brackets):

| Test | New expectation |
|---|---|
| `EngineeringCalculatorTests.Leading_zeros_stay_while_editing` [`Append_keeps_leading_zeros_and_prepend_drops_them`] | `reset X4` = `04`; `reset X4 X1` = `041`; `reset X4 1X` = `14`; `reset 1X` = `10` |
| `EngineeringCalculatorTests.Flip_reverses_and_keeps_zeros` [`Flip_reverses_the_number`] | `41` → `14`, `123` → `321`, `120` → `21`, `120 flip 1X` = `121`, `120 flip ÷ 10 =` = `2`, `41 reset X4 flip` = `4` |
| `TransformSimulatorTests.Digit_map_keeps_leading_zero_then_prepends` [`Digit_map_keeps_leading_zero_and_prepend_drops_it`] | `35 3->0 5X` → `["05", "55"]` |
| `TransformSimulatorTests.Digit_map_then_flip` | `35 3->0 flip` → `["05", "5"]` |
| `TransformSimulatorTests.Flip_then_number_transform_drops_zero` | `120 flip X++` → `["21", "22"]` |
| `LeftSearchTests.Connect_cheapest` row `(35, 505, "3->0:1, 5X:1", "3->0 5X")` | becomes `(35, 55, "3->0:1, 5X:1", "3->0 5X")`; this exercises Join by value |
| `ModuloSearchTests.Presses_5_to_48_pads_with_zeros` [`Presses_5_to_48_cannot_pad_with_zeros`] | `Presses` returns null |
| `DivisionShortcutTests.Example_41_to_48_without_8X_or_X8_picks_490` [`…_prepends_three_digits`] | no line starts with `0X`; has `1X 9X 4X ÷ 10 10 =` at cost 6; cheapest cost still 6 |

Add:

- `TransformSimulatorTests`, the in-game cases: `47 4->7 7->3 3->0 8X` → `["77", "33", "00", "80"]`; `76 7->3 3->0 8X` → `["36", "06", "86"]`.
- `LeftEditsTests`: `Plan("31", "4031", "flip, X0, X4")` = `flip X0 X4 flip`; `Plan("31", "4031", "0X, 4X")` = null; `Plan("41", "1041", "0X, 1X")` = null; `Plan("12", "210", "flip, X0")` = `flip X0`.
- `LeftSearchTests`: Connect 35 → 505 with `3->0:1, 5X:1` finds no line.
- `ModuloSearchTests`: `Find(31, 11, "flip, X0, X1, X4, X5, mod, 10, 3, 4, 5", 12)` has a k = 3 line through `flip X0 … flip` (for example `flip X0 X1 X1 flip mod 5 5 10 =`), and no line contains `0X`.

Then run `dotnet test` (308 + the new tests) and the Release timing check from requirements.md ("Checked").
