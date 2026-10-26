# Design: Division Shortcut

**Status:** Implemented (v1), following this design. Known issue: `Solve` takes about a second on larger puzzles; speeding it up is a future ticket (see "As built").

**Correction (2026-10-22):** `10` works on the right side only. This design first had `10` append on the left side too, and the code did too. The text below is corrected; the code was fixed by [digit-keys-right-side-only](../digit-keys-right-side-only/requirements.md).

## As built

All files, types and the test plan below were implemented as written, with these differences:

- `EngineeringCalculator.Run` takes an optional `Buttons` argument, so a press whose button is missing returns null.
- `DivisionShortcut.PrefixRange` is public, so tests can check the formula's A range directly.
- `LeftEdits` tries appends before prepends, so a prepend/append pair comes out append first (`X1 8X`, as in the requirements' examples).
- The UI recomputes the Division Shortcut only on Enter (in the S, G or Buttons box) and once at startup, not on every `TextChanged`. `TextChanged` only sets the status label to "Press Enter to recalculate.". The Modulo Shortcut still recomputes on every change.
- Performance: `Solve` runs on the UI thread and takes about a second for larger inputs (the 12-press limit and the 111 suffixes × 9 divisors × up to 50 prefixes add up). Ideas for the future ticket: run it off the UI thread, prune by a lower bound on pre-edit cost, or lower the press limit.
- Not done (out of scope, v2): single digit keys, so the Buttons box rejects tokens like `9` or `0`.

## Where code goes

- `CalculatorPuzzle/DivisionShortcut.cs`: the formula and line search. No UI.
- `CalculatorPuzzle/EngineeringCalculator.cs`: a small press simulator for the early-game buttons, plus `Buttons.Parse`. The shortcut uses the simulator to check each line, and cost-per-line can reuse it later.
- `CalculatorPuzzle/LeftEdits.cs`: the shortest-path search for left-side edits (`dX`, `Xd`, `flip`).
- `CalculatorPuzzle/MainForm.cs`: a new Division Shortcut section (see "UI" below).
- `CalculatorPuzzle.Tests/DivisionShortcutTests.cs`, `CalculatorPuzzle.Tests/EngineeringCalculatorTests.cs` and `CalculatorPuzzle.Tests/LeftEditsTests.cs`.

Follow the style of `ModuloShortcut.cs`: a static class, records for results, `checked` arithmetic with `OverflowException` caught and reported.

## Types

```csharp
// EngineeringCalculator.cs
public enum Key { Prepend, Append, Ten, Reset, Flip, Divide, Equals }
public record Press(Key Key, int Digit = -1)   // Digit only for Prepend/Append
{
    public override string ToString();           // "4X", "X4", "10", "reset", "flip", "÷", "="
}
public record Buttons(IReadOnlySet<int> PrependDigits, IReadOnlySet<int> AppendDigits,
                      bool Ten, bool Reset, bool Divide, bool Mod, bool Flip)
{
    public static Buttons All { get; }           // every early-game button
    // Parses the UI's comma-separated list ("4X, 8X, X4, 10, ÷"). Returns null and an error
    // naming the first unknown token. Case and spaces are ignored; "/" means ÷; "=" is accepted
    // and ignored, since = is always available.
    public static (Buttons? Buttons, string? Error) Parse(string text);
}
public static class EngineeringCalculator
{
    // Runs presses from start. Returns the entry after each press, or null if a press is invalid
    // (missing button, ÷ by 0, = with nothing pending).
    public static IReadOnlyList<string>? Run(long start, IReadOnlyList<Press> presses);
    public static int Cost(IReadOnlyList<Press> presses);   // = is free
}

// LeftEdits.cs
public static class LeftEdits
{
    // Fewest left-side presses that turn the display `from` into `to`, where `to` contains
    // `from` (or, with flip, its reverse) as a substring. Null if the buttons can't do it.
    public static IReadOnlyList<Press>? Plan(string from, string to, Buttons buttons);
}

// DivisionShortcut.cs
public record Line(
    string Built,      // X as shown before ÷ (may start with 0 after a flip)
    long Value,        // X
    long Divisor,      // D, "10" repeated r times
    long Quotient,     // q
    IReadOnlyList<Press> Presses,
    int Cost);
public record Result(string? Error, IReadOnlyList<Line> Lines);
public static class DivisionShortcut
{
    public static Result Solve(long start, long goal, Buttons buttons, int maxPresses);
}
```

Simulator state: `Display` (string, the left side), `Pending` (whether `÷` was pressed, so the right side is active), `Operand` (string, empty right after `÷`).

- Left side (`Pending` false): `dX`, `Xd`, `reset` and `flip` change `Display`; `÷` starts the right side. `10` and `=` are invalid. `flip` reverses the string and keeps zeros (`120` → `021`). Leading zeros stay in `Display` until `÷` or `=`, which use the value (`021` = 21).
- Right side (`Pending` true): only `10` is valid, appending `10` to `Operand`. `dX`, `Xd`, `reset`, `flip` and a second `÷` are invalid. `=` with an empty operand is invalid.
- `=` sets `Display` to `floor(X / D)` as a plain string and clears `Pending`, so the result is the left side again.

## Left-edit search (`LeftEdits.Plan`)

While building toward a known target string W, the display is always a piece of W, either as-is or reversed. So a state is `(i, j, reversed)`, meaning the display equals `W[i..j)`, or its reverse when `reversed` is true. Breadth-first search, since every press costs 1:

- **Start:** each position where `from` occurs in W, with `reversed = false`. With `flip`, also each position where reverse(`from`) occurs, with `reversed = true`.
- **Goal:** `(0, len(W), false)`.
- **Moves when not reversed:** `dX` with d = W[i−1] → (i−1, j); `Xd` with d = W[j] → (i, j+1).
- **Moves when reversed:** `dX` with d = W[j] → (i, j+1); `Xd` with d = W[i−1] → (i−1, j).
- **`flip`:** toggle `reversed`, same i and j.

A move is allowed only if its button exists. Keep the parent of each state to rebuild the press list. There are at most 2·len(W)² states, so this is instant. Without `flip`, the result matches the old "prepends + appends" cost.

## Algorithm

1. **Validate.** S ≥ 0, G ≥ 0, `maxPresses` ≥ 1, and `buttons` has `Divide` and `Ten` (else the error "The Division Shortcut needs ÷ and 10."). If S = G, return no lines and no error.
2. **Divisors.** D = `10` repeated r times, for r = 1 up to `maxPresses − 1` (room for `÷`), stopping before D overflows `long` (r ≤ 9). Cost r.
3. **Quotients.** Candidate q strings are every substring of G's digits, plus (with `flip`) every reversed substring. Skip q with a leading `0`. Post edits = `LeftEdits.Plan(q, G)`; skip q if null.
4. **Cores.** The kept part of X is `core` = S, plus (with `flip`) reverse(S).
5. **Suffixes.** T ranges over every digit string up to 2 digits, including empty. Building T is checked in step 7, so no filter is needed here.
6. **Prefix candidates.** For each (q, core, T, D), let m = len(core) + len(T) and L = value of `core ‖ T`. Compute lo and hi from the formula and clamp lo to ≥ 0. If lo > hi, skip. If lo = 0, the candidate is A = empty. Otherwise collect A values in [lo, hi] with no leading zero whose digits are all in the `dX` set (or `dX` ∪ `Xd` with `flip`), shortest first, up to 50 of them. Cap the scan at 3000 values of the range so it stays fast.
7. **Assemble and check.** For each A, W = `A ‖ core ‖ T`. Pre edits = `LeftEdits.Plan(S, W)`; skip if null. Presses = pre edits, `÷`, `10` × r, `=`, post edits. Skip lines over `maxPresses`. Run the presses through `EngineeringCalculator.Run`, and drop the line if the result isn't G (this catches formula or overflow mistakes).
8. **Rank.** Sort by cost, then by the number of presses that aren't `flip`, then by D. Remove duplicate press lists. If nothing is left, return the error "No division line fits the available buttons."

The search size is small: O(len(G)²) quotients, 2 cores, ~111 suffixes, r ≤ 9, up to 50 prefixes, and an instant edit search for each. If it's ever slow, cache `Plan` results by (from, to).

## UI

Add rows to the existing `TableLayoutPanel` in `MainForm`, below the Modulo Shortcut rows, under a caption row "Division Shortcut":

- `buttonsBox`: a `TextBox`, default `5X, X1, X2, X7, X4, 4X, 6X, 3X, 8X, 10, /, X9, flip, mod, =, reset`, about 520 wide.
- `divisionStatusLabel`: a grey `Label` like `detailLabel`, for errors or "N line(s)".
- `divisionLinesBox`: a read-only, multiline `TextBox` with a vertical scrollbar, about 10 rows high and 520 wide, one line per row.

`RecalculateDivision()` runs from `startBox`, `goalBox` and `buttonsBox` `TextChanged`, and once at startup. The existing `startBox` handler calls both recalculations. Steps:

1. Parse S and G (same checks and message as `Recalculate`).
2. `Buttons.Parse(buttonsBox.Text)`; on error, show it in the status label and clear the lines.
3. `DivisionShortcut.Solve(start, goal, buttons, MaxPresses)` with `const int MaxPresses = 12`. Show the error or the line count, and the first 10 lines.

Format each line as presses joined by spaces, then the division, then the cost: `8X ÷ 10 = flip   841 ÷ 10 = 84 → 48   (4 presses)`. Leave out the `→ G` part when the quotient is already G. Put the formatting in a static `Describe(DivisionShortcut.Line)` next to the existing `Describe`.

Rename the window title to "Calculator Puzzle", since it now shows two shortcuts.

The UI isn't unit tested. Check it by running the app (see "Check the work" in [NEXT_PROMPT.md](NEXT_PROMPT.md)).

## Test plan

`ButtonsParseTests` (in `EngineeringCalculatorTests.cs`):

- `4X, 8X, X4, 10, ÷` gives prepend {4, 8}, append {4}, `Ten`, `Divide`, and no `Reset`, `Mod` or `Flip`.
- `4x,x4 , /, FLIP` works: case, spacing, `/` and `flip` are handled.
- Duplicates (`4X, 4X`) and `=` are accepted.
- The default text parses: prepend {3, 4, 5, 6, 8}, append {1, 2, 4, 7, 9}, `Ten`, `Divide`, `Mod`, `Flip`, `Reset`.
- `Y4` and `44X` give an error naming the token.
- Empty text gives an empty button set, not an error (`Solve` then reports that ÷ and 10 are missing).

`EngineeringCalculatorTests`:

- `41`, `X1`, `8X` → `8411`; `÷`, `10`, `10` → operand `1010`; `=` → `8`; `4X` → `48`.
- Leading zeros: `reset`, `X4` → `04`; `1X` → `104`.
- `flip`: `41` → `14`; `123` → `321`; `120` → `021`; `120`, `flip`, `1X` → `1021`; `120`, `flip`, `÷`, `10`, `=` → `2` (021 = 21, 21 ÷ 10 = 2).
- Whole-number division drops the fraction: `8411 ÷ 1010 = 8`; `41 ÷ 10 = 4`.
- `dX`, `Xd`, `reset` or `flip` after `÷` is invalid; `10` after `÷` is valid; `10` before `÷` is invalid.
- `dX`, `Xd` and `flip` work again after `=` (`8` → `4X` → `48`; `84` → `flip` → `48`).
- `=` with no operand (`÷`, `=`) is invalid.
- A missing button makes `Run` return null.
- `Cost` doesn't count `=`.

`LeftEditsTests`:

- Without `flip`, `Plan("41", "8411")` is 2 presses (`8X`, `X1` in either order), and `Plan("41", "48")` is null.
- `Plan("4", "48")` with only `4X`, `8X`, `flip`: `8X flip` (4 → 84 → 48).
- `Plan("7", "27")` with only `X2`, `flip`: `X2 flip`.
- Prepending a run without `dX` buttons: `Plan("45", "3245")` with only `X2`, `X3`, `flip` is `flip X2 X3 flip` (54 → 542 → 5423 → 3245).
- A single-digit core is its own reverse: `Plan("1", "321")` with only `X2`, `X3`, `flip` is `X2 X3 flip` (3 presses).
- Every returned plan, run through the simulator from `from`, ends on `to`.

`DivisionShortcutTests`:

- Each row of every example table in the requirements produces a line with that sequence and press count, using that row's buttons. The cheapest 41 → 48 line with every button except `flip` is `÷ 10 = X8` at 3 presses.
- With the default buttons, 41 → 48's cheapest lines cost 4 and include `8X ÷ 10 = flip` and `÷ 10 = 8X flip`; no line uses `X8`.
- With the default buttons minus `flip`, 41 → 48's cheapest lines cost 6 and include `X1 8X ÷ 10 10 = 4X`.
- 7 → 250 with `X0`, `X2`, `X5`, `flip`, `10`, `÷` costs 6; without `flip` it returns the "no line fits" error.
- 41 → 48 with only `4X`, `X4`, `10`, `÷` returns no lines and the "no line fits" error.
- No line presses `dX`, `Xd` or `flip` between `÷` and `=`, and every press in every line is in the given button set.
- S = G returns no lines and no error.
- No q has a leading zero (for example, G = 105 never uses q = `05`).
- Brute-force check: for random S, q, D and T, every A in the computed range gives floor(X / D) = q, and the range is non-empty when D ≥ 10^m. A scratch version of this check passed 20,000 cases.
- Every returned line, run through the simulator, ends on G.
