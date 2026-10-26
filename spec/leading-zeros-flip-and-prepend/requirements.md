# Requirements: `flip` and `dX` drop leading zeros (bugfix)

**Status:** Done (2026-10-24).

## Background

The solver keeps the left side as a digit string, so leading zeros can survive: a digit map makes `35` → `05`, `reset` then `X4` makes `04`, and `X0` then `flip` makes `120` → `021`. Until now it treated every left-side button as a string edit, so `flip` and `dX` kept those zeros:

- `flip`: `120` → `021`, then `1X` → `1021`; `05` → `50`.
- `dX`: `04` → `1X` → `104`. The Modulo Shortcut relies on this to pad S with zeros (`0X 0X 2X` on `5` → `2005`).

The game doesn't work that way. It keeps two values for the left side: the display text (`MainCalc.output.text`) and the number (`MainCalc.total`, a `long`). Some buttons read the text and some read the number. A button that reads the number never sees leading zeros.

This spec fixes `flip` and `dX`, the two left-side edits that read the number while the solver treated them as text. Other leading-zero problems (see "Out of scope") are separate specs.

## The rule

| Button | Game method | Reads | Effect on a display with leading zeros |
|---|---|---|---|
| `flip` (`operator_reverse`) | `MainCalc.Reverse` | the number | reverses `total`, and the result is a number: `04` → `4`, `50` → `5`, `120` → `21` |
| `dX` | `MainCalc.AddToBegin` | the number | `d` + `total`: `04` → `1X` → `14`; `0` → `1X` → `10` |
| `Xd` | `MainCalc.AddToEnd` | the text | text + `d`: `04` → `X1` → `041` (unchanged) |
| digit map `a->b` | `MainCalc.Convert_to` | the text | text replace: `35` → `3->0` → `05` (unchanged) |
| number transforms (`X++`, `X+k`, `sqr`, …) | `PlusNumber` and others | the number | `05` → `X++` → `6` (unchanged; already modelled) |
| operators `÷ mod + - ×` | | the number | `05` → `÷` → left side `5` (unchanged) |

So:

1. **`flip`**: drop the display's leading zeros, reverse the digits, and drop leading zeros again. `flip` never returns a display with a leading zero, and its result never ends in 0 unless it is `0`.
2. **`dX`**: drop the display's leading zeros, then put d in front. An all-zero display counts as `0` (`00` → `1X` → `10`).

## Evidence

- **In the game (2026-10-24):**
  - `flip`: `53` → `3->0` → `50` → `flip` → `5` (the solver said `05`). `34` → `3->0` → `04` → `flip` → `4` (the solver said `40`).
  - `dX`: `47` → `4->7` → `77` → `7->3` → `33` → `3->0` → `00` → `8X` → `80` (the solver said `800`). `76` → `7->3` → `36` → `3->0` → `06` → `8X` → `86` (the solver said `806`).
- **Static analysis of `Assembly-CSharp.dll`** (sha256 `0ec2d076…`, REA evidence `ev_7ead2560…`):
  - `Reverse`: `total = total.ToString().Reverse().Aggregate(0, (b, x) => 10 * b + x - 48); output.text = total.ToString();`
  - `AddToBegin` IL: `output.get_text` → `get_Length` (only the `< 6` guard), then `Int32.ToString(digit)`, `ldflda total` → `Int64.ToString`, `String.Concat`, `output.set_text`, `total = Convert.ToInt32(output.text)`. The value comes from `total`, not the text.
  - `AddToEnd` IL: `output.get_text`, `Int32.ToString(digit)`, `String.Concat`, `output.set_text`, so it extends the text.
  - `Convert_to`: `output.text = output.text.Replace(a, b); total = Convert.ToInt32(output.text);`
- The link from `operator_dx` to `AddToBegin(d)` lives in the Unity scene, not the code; it fits the button names and every in-game result above.

## What must change

1. **Simulator** (`EngineeringCalculator.Run`): `flip` and `dX` follow the rule above.
2. **`LeftSearch`**: the forward moves for `flip` and `dX` use the same rule as the simulator. The backward moves (preimages) list only displays that really map to the target. A forward display with leading zeros must still be able to join a backward path whose first press reads the number (`35` → `3->0` → `05` → `5X` → `55`).
3. **`LeftEdits.Plan`**: `dX` can't extend a piece that starts with `0` (except the single digit `0`), and `flip` drops zeros at both ends of the piece.
4. **Modulo Shortcut**: the math is unchanged. Lines whose left edits pad S with `0X` disappear, because `LeftEdits` can no longer build them. Padding through `flip X0 … flip` still works and stays (`31` → `flip X0 X4 flip` → `4031`).
5. **Division Shortcut, arithmetic phase**: no change of their own. They get their left edits from `LeftEdits` and `LeftSearch` and check every line in the simulator.
6. **Docs**: CLAUDE.md's `flip` row, the leading-zeros sentence below the table and the `ModuloShortcut.cs` bullet; the `ModuloShortcut` class comment ("leading zeros are kept").

## Examples

All were checked in a scratch copy with the fix applied (see "Checked").

| Case | Before (wrong) | After |
|---|---|---|
| `Run(120, [flip])` | `021` | `21` |
| `Run(120, [flip, 1X])` | `1021` | `121` |
| `Run(120, [flip, ÷, 10, =])` | `2` | `2` (unchanged) |
| `Run(41, [reset, X4])` | `04` | `04` (unchanged) |
| `Run(41, [reset, X4, X1])` | `041` | `041` (unchanged) |
| `Run(41, [reset, X4, 1X])` | `104` | `14` |
| `Run(41, [reset, 1X])` | `10` | `10` (unchanged) |
| `Run(41, [reset, X4, flip])` | `40` | `4` |
| `Run(35, [3->0, 5X])` | `05`, `505` | `05`, `55` |
| `Run(35, [3->0, flip])` | `05`, `50` | `05`, `5` |
| `Run(47, [4->7, 7->3, 3->0, 8X])` (in-game) | `77`, `33`, `00`, `800` | `77`, `33`, `00`, `80` |
| `Run(76, [7->3, 3->0, 8X])` (in-game) | `36`, `06`, `806` | `36`, `06`, `86` |
| `Run(120, [flip, X++])` | `021`, `22` | `21`, `22` |
| `LeftEdits.Plan("31", "4031")`, `flip, X0, X4` | not checked | `flip X0 X4 flip` |
| `LeftEdits.Plan("31", "4031")`, `0X, 4X` | `0X 4X` | none |
| `LeftEdits.Plan("41", "1041")`, `0X, 1X` | `0X 1X` | none |
| `LeftEdits.Plan("12", "210")`, `flip, X0` | `flip X0` | `flip X0` (unchanged) |
| `LeftSearch.Connect` 35 → 505, `3->0:1, 5X:1` | `3->0 5X` | no `3->0 5X` line (`5X 3->0` is valid: 35 → 535 → 505) |
| `LeftSearch.Connect` 35 → 55, `3->0:1, 5X:1` | none | `3->0 5X` (joined by value) |
| `ModuloSearch.Presses` 5 → 48, `2005 mod 103`, `0X, 2X, mod, 10, 3` | `0X 0X 2X mod 10 3 =` | none |
| Division 41 → 48, all `dX`/`Xd` but `8X`, `X8` | `0X 9X 4X ÷ 10 10 =` (6) | `1X 9X 4X ÷ 10 10 =` (6); no line starts with `0X` |
| Modulo 31 → 11, `flip, X0, X1, X4, X5, mod, 10, 3, 4, 5` | not checked | k = 3 and 4 lines through `flip X0 … flip`, such as `flip X0 X1 X1 flip mod 5 5 10 =` |

## Checked

Applied the fix to a scratch copy and ran `dotnet test` (308 tests). Exactly these 8 failed, all because they encode the old rule; with their expected values changed to the "After" column, all 308 pass:

- `EngineeringCalculatorTests.Leading_zeros_stay_while_editing` (`104`)
- `EngineeringCalculatorTests.Flip_reverses_and_keeps_zeros` (`021`, `1021`)
- `TransformSimulatorTests.Digit_map_keeps_leading_zero_then_prepends` (`505`)
- `TransformSimulatorTests.Digit_map_then_flip` (`50`)
- `TransformSimulatorTests.Flip_then_number_transform_drops_zero` (`021`)
- `LeftSearchTests.Connect_cheapest(35, 505, "3->0:1, 5X:1")`
- `ModuloSearchTests.Presses_5_to_48_pads_with_zeros`
- `DivisionShortcutTests.Example_41_to_48_without_8X_or_X8_picks_490`

Timing (Release, same machine, before → after): 41 → 483 with every button, arithmetic phase: 660 ms → 630 ms. Old Calculator benchmark (`X++, X--, X+10, X-10, flip, 1->0, 2->5, 7->3`, 208 start/goal pairs, cap 12): 341 ms → 366 ms, same mean cost (4.58) and max (12).

## Edge cases

- **All-zero display:** `00` reads as `0`, so `flip` → `0` and `1X` → `10`.
- **The start number and results** never have leading zeros; only left-side editing makes them.
- **Lines that lose a press:** a line that used `flip` or `dX` on a leading-zero display either gets a different (sometimes longer) line or none. That's correct.
- **`flip` before any edit** (`LeftEdits.Plan("120", "21")`) returns none, because `Plan` places `from` inside `to` as text. That's the same as before; `LeftSearch` finds it in the arithmetic phase.

## Open questions

None. Both rules are confirmed in the game.

## Out of scope

Other leading-zero and length problems found in the same code, each its own spec:

- **6-character display limit:** `AddToBegin` and `AddToEnd` do nothing when the display text already has 6 characters (leading zeros count). Number transforms cap at 999 999 (`PlusNumber`, `MultNumber`, `Sqr`), not 10^18. The solver allows both, so it can list lines the game can't play, such as the k = 4 Modulo line `flip X0 X0 X0 X4 X5 X1 flip …`. Now [`display-cap/`](../display-cap/).
- **Backward `flip` and leading-zero preimages in `LeftSearch`:** backward moves list plain numbers only (see the design), so a few lines whose zero-dropping `flip` falls in the backward half aren't found. The Old Calculator benchmark found no case where this costs a press.
- Win conditions the solver doesn't know: `passive_near1` (within ±1 wins), challenge mode (within ±10).
