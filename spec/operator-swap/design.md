# Operator swap: design

Read `requirements.md` first.

## Idea

A puzzle with `swap` is two puzzles: S → G (direct) and F → S (flipped, F = G, + 1 in rules 12 and 14). The phases stay one-goal searches. The app calls each phase once per **leg** and wraps flipped lines with a leading `swap`. The same pattern as the target loop in `near-one-passive/design.md`.

## Model (`EngineeringCalculator.cs`)

- `Key.Swap` (add to the enum before `Equals`). `Press.ToString` gives `swap`; `ChargeKey` gives `swap`.
- `Buttons.Parse` accepts the token `swap` (`swap:n`, `swap:ng`). `Buttons.Token("swap")` returns it. `Buttons.OperatorKey("swap")` stays null (it is a left-side button).
- `Run(start, presses, …)` returns null for a `Key.Swap` press: it doesn't know the goal. Flipped lines go through `SwapSearch.Check` instead.
- `OneSend` doesn't need a change: rule 13 never runs the flipped leg.

## `SwapSearch.cs` (new)

```csharp
/// <summary>operator_swap: the display and the goal trade places, then the game sends. Only as a line's first press, at most once.</summary>
public static class SwapSearch
{
    public const string Token = "swap";

    /// <summary>One solve: start → goal. A flipped leg's lines get `swap` in front (cost + 1, Reaches = the puzzle's S).</summary>
    public sealed record Leg(long Start, long Goal, Buttons Buttons, RequiredPlan Plan, bool Flipped);

    /// <summary>The direct leg (unless boss rule 11 requires swap), then the flipped leg when swap has a charge, S ≠ G and not rule 13.</summary>
    public static IReadOnlyList<Leg> Legs(long start, long goal, Buttons buttons, RequiredPlan plan,
                                          string? required, bool boss, int bossRule);

    /// <summary>F: the display after the swap. G, + 1 in rule 12 (Send), + 1 in rule 14 unless swap is glass (SubUse runs first).</summary>
    public static long FlippedStart(long goal, Buttons buttons, int bossRule);

    /// <summary>True when presses[0] is swap, no other press is, the rest runs from F and sends S (Sent with plusOne), and the whole line fits the charges.</summary>
    public static bool Check(long start, long goal, IReadOnlyList<Press> presses, Buttons buttons, int bossRule);

    /// <summary>A flipped leg's line with swap in front: cost + 1, drained / spare / Missing on the whole line, Reaches = inner.Reaches ?? start.</summary>
    public static ResultLine Wrap(ResultLine inner, long start, Buttons buttons, string? required);
}
```

`Legs` details:

- Flipped leg buttons: the puzzle's buttons with `swap` removed (`Buttons.Without(Token)`, new, a copy with that charge set to 0). The phases don't generate `swap` anyway; removing it keeps the inner `Spare` / `Drained` from counting it twice. `Wrap` recounts both on the whole line with the full buttons.
- Flipped leg plan: `RequiredPlan.For(required == Token ? null : required, boss)`, plus `OneSend()` never (rule 13 has no flipped leg). With required `swap`, every inner line passes; the whole line presses swap.
- Boss rule 11 with required `swap`: no direct leg.
- Rule 13 (`EngineeringCalculator.RerollOnMissRule`): no flipped leg.
- F = S: no flipped leg for the phases; `MainForm` enqueues the 1-press line `[swap]` itself (rule 8). `Check` accepts it.

`Wrap` builds `new ResultLine(...)` from the inner row with `Presses = [Press(Key.Swap), ..inner.Presses]`, so the kind fields (`Arithmetic`, `Division`, …) stay the inner line's and `Ranking` needs no change: equal-cost ties still fall to drained, spare, kind.

## `ResultLine.cs`

- Add `bool Swapped = false` (last parameter). `ToString` is unchanged: the presses already start with `swap`, and `Reaches` prints `goal S`.
- Update the `Reaches` comment: "the goal the line ends at when it differs from G (boss rule 14, or a swap line, which ends at S)".
- `Ranking` unchanged.

## `MainForm.cs`

In `StartDivisionSearch`, build `legs = SwapSearch.Legs(...)` after the plan is set. Then loop the legs **inside each phase**, so every phase's cap counts both legs:

```
foreach leg: arithmetic(leg.Start, leg.Goal, leg.Buttons, cap(leg), leg.Plan) → enqueue Wrap if Flipped, update divisionCap with the wrapped cost
foreach leg: two-operator(…, cap(leg))                                        → same
foreach leg: division(…, cap(leg))                                            → keep the direct leg's error for the status
```

`cap(leg)` is `divisionCap - 1` for the flipped leg (`MaxPresses - 1` for arithmetic), so the wrapped cost stays under the shared cap. Inside each leg the order is the same as today: direct first, then flipped, as the user described.

- `OnSearchTick` → the modulo task loops the legs with the shared cap the same way; `OnModuloDone` merges.
- Rule 12: pass `plusOne` to both legs (the flipped leg's start already has the +1).
- Rule 14 (`stepTask`): run `GoalStepSearch.Search` for the direct leg, then for the flipped leg (`F`, goal S, `Buttons.Without(swap)`, `wanted` minus the lines found so far, `maxSteps` `MaxPresses - 1`), with `onLine` wrapping. `GoalStepSearch` itself is unchanged.
- `searchSameNumber` is unchanged (S = G: no search, swap does nothing).
- Count flipped lines in a new `swapCount` (lines whose `Swapped` is true, counted where they are enqueued). `DoneStatus` adds `, M swap line(s)` when a flipped leg ran.

## `GameLink.cs` and the mod

- `GameLink.MapButton`: `"operator_swap" => "swap"`.
- `GameHook.MelonLoader/ButtonSymbols.cs`: the same case (it mirrors `MapButton`; `ButtonSymbolsTests` checks it). Remove `operator_swap` from the unmapped list in `ButtonSymbolsTests.cs`.
- No other mod change. After a swap the goal changes, so the mod already publishes the swapped puzzle (requirements, open question 1).

## Tests (`CalculatorPuzzle.Tests/SwapTests.cs`, new)

| Test | Checks |
|------|--------|
| `Parse_swap` | `swap:2` parses with 2 charges; `swap:1g` is glass; `Press(Key.Swap)` prints `swap` |
| `Run_rejects_swap` | `EngineeringCalculator.Run` returns null for a line with `swap` |
| `FlippedStart_rules` | G normally; G + 1 in rule 12; G + 1 in rule 14; G in rule 14 with glass swap |
| `Check_first_only` | `[swap, ÷, 10, =]` from 4 → 48 passes; `[÷, swap, …]` and two swaps fail; no swap charge fails |
| `Flipped_line_found` | S 4, G 48, `swap:1, ÷:1, 10:1`: the legs give `swap ÷ 10 =`, cost 3, `Reaches` 4; the direct leg gives nothing |
| `Edits_only_after_swap` | S 12, G 1, `swap:1, X2:1`: `swap X2`, cost 2 |
| `No_flipped_leg` | rule 13, no swap charge, S = G: `Legs` has only the direct leg (or none for S = G at the call site) |
| `Required_swap_boss` | boss rule 11 with `swap` required: `Legs` has only the flipped leg |
| `Market_swap` | Market Demand with `swap`: direct lines are `Missing`, flipped lines aren't |
| `Swap_alone` | rule 12, S 49, G 48: F = S; `Check([swap])` passes |
| `Drained_counts_swap` | a wrapped line with `swap:1` has `Drained` one higher than the inner row |
| `Rule14_flipped` | rule 14, `GoalStepSearch` from F = G + 1 to S: a line with k charged presses after swap ends at S + k |
| `GameLink_maps_swap` | `operator_swap` maps to `swap` |

`MainForm`'s leg loops have no unit test (the UI code has none); Test W covers them.

## Cost

The flipped leg is a second full solve, so up to 2× the search time when `swap` has a charge. The shared cap keeps the second leg short when the first finds a cheap line. Measure 41 → 483 with every button plus `swap:1`, Release, and write it in "As built".

## Docs to update when implemented

- `CalculatorPuzzle/README.md`: a "Swap" note under "Search order and truncation"; file notes for `SwapSearch.cs` (new), `EngineeringCalculator.cs` (`Key.Swap`), `ResultLine.cs`, `MainForm.cs`, `GameLink.cs`.
- `docs/button-id.md`: the `operator_swap` row (display ↔ goal, then sends; left side only; token `swap`).
- `CLAUDE.md`: a `swap` row in the Engineering Calculator button table.
- `GameHook.MelonLoader/README.md`: `swap` in the overlay token list, if it has one.
- `spec/README.md`: status line.

## As built

- Files: `SwapSearch.cs` (new), `Key.Swap` and `Buttons.Without` in `EngineeringCalculator.cs`, `ResultLine.Swapped`, `MainForm` legs (`searchLegs`), `GameLink.MapButton` and `ButtonSymbols`.
- Deviations from the plan above: `Wrap` returns null when the whole line doesn't fit the charges (the caller counts it as rejected); `SwapSearch.Has`, `Flip` (the flipped leg's buttons: swap at 0, context and near / median bound to goal S) and `SwapAlone` (the 1-press `swap` line) were added; the flipped leg uses only the target S (no Near 1 targets); the status shows `0 swap line(s)` when the flipped leg ran and found nothing.
- Tests: `SwapTests.cs` (15) and the `operator_swap` case in `ButtonSymbolsTests.cs`; 756 tests pass.
- Timing, `41 → 483` with every button (`X0`–`X9`, `0X`–`9X`, `10`, `0`–`9`, `÷ mod + - × flip reset`), Release, arithmetic + two-operator + division + modulo with shared caps, cold start: 0.74 s without `swap`, 0.92 s with `swap:1` (2 legs, 257 lines against 149).
