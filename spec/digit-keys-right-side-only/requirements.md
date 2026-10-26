# Requirements: Digit keys work on the right side only (bugfix)

**Status:** Done.

## Background

The [division-shortcut](../division-shortcut/requirements.md) and [digit-keys](../digit-keys/requirements.md) specs said digit keys work on **both sides**: on the left side, `10` appends `10` and a single key `d` does what `Xd` does. Checking in the game (2026-10-22) showed that's wrong. Digit keys work on the **right side only**, after an operator. The docs are corrected; the code still has the old rule.

## The rule

| Button | Where it works | Effect |
|--------|----------------|--------|
| `10` | Right side only | Append `10` to the operand: `÷` `10` `10` → operand `1010` |
| `d` (`0`–`9`) | Right side only | Append d to the operand: `÷` `4` `5` → operand `45` |

- On the left side (the start number, the number being built before an operator, or a result after `=`), a digit key does nothing. The solver treats it as an invalid press, like `dX` on the right side.
- After `=`, the result is the left side again, so digit keys are invalid until the next operator.
- On the left side, digits come only from `dX` and `Xd` (and `flip` / `reset`).
- Nothing else changes: `dX`, `Xd`, `flip` and `reset` still work on the left side only, `=` is still free, and the operand is still built from digit keys only.

## What must change

1. **Simulator** (`EngineeringCalculator.Run`): `10` or a digit key pressed on the left side is invalid, so `Run` returns null.
2. **Left edits** (`LeftEdits.Plan`): never uses `10` or digit keys. Only `dX`, `Xd` and `flip`.
3. **Division Shortcut** (`DivisionShortcut.Solve`): no line presses `10` or a digit key on the left side. With `flip`, the prefix A's digits come from `dX` and `Xd` only, not digit keys. The divisor D is still typed with `10` and digit keys.
4. **UI**: no change. The Buttons box still accepts `10` and `0`–`9` (they're still needed for the operand), and the default text is unchanged.
5. **Modulo Shortcut**: no change. It lists formulas, not press sequences, and never assumed left-side digit keys.

## Examples

| Case | Buttons | Before (wrong) | After |
|---|---|---|---|
| `Run(41, [3])` | any with `3` | `413` | invalid (null) |
| `Run(41, [10])` | any with `10` | `4110` | invalid (null) |
| `Run(41, [÷, 10, =, 8])` | any with `8`, `10`, `÷` | `48` | invalid (null): `8` after `=` is on the left side |
| `Run(531, [÷, 4, 5, =])` | `4`, `5`, `÷` | `11` | `11` (unchanged) |
| `LeftEdits.Plan("4", "48")` | `8` | `8` | none |
| `LeftEdits.Plan("41", "4110")` | `X1`, `X0`, `10` | `10` (1 press) | `X1 X0` (2 presses) |
| `LeftEdits.Plan("41", "4110")` | `10` | `10` | none |
| `LeftEdits.Plan("4", "48")` | `X8`, `8` | `X8` | `X8` (unchanged) |
| Solve 41 → 48 | `4X`, `X4`, `10`, `8`, `÷` | `÷ 10 = 8` (3) | no line ("No division line fits the available buttons.") |
| Solve 41 → 48 | `X8`, `8`, `10`, `÷` | `÷ 10 = X8` (3) | `÷ 10 = X8` (3, unchanged) |
| Solve 531 → 11 | `1X`, `10`, `4`, `5`, `÷` | `÷ 4 5 =` (3) | unchanged |
| Solve 7 → 250 | `X0`, `X2`, `X5`, `10`, `3`, `÷` | `X5 X0 ÷ 3 =` (4) | unchanged |
| Solve 1234 → 7 | `1`, `7`, `10`, `÷` | `÷ 1 7 1 =` (4) | unchanged |
| Solve 41 → 48 | default + `3` | `5X flip ÷ 3 =`, ties with `8X ÷ 10 = flip` (4) | unchanged |

Why 41 → 48 with `4X`, `X4`, `10`, `8`, `÷` has no line: X is built from `41` with only 4s added, so it starts with `4`. q can't be `4` (no way to append the `8` afterwards), so q is `8` or `48`. For q = 8, D lies in (X/9, X/8], so D starts with 4, 5 or 6, which `8` and `10` can't type. For q = 48, X starts `41` gives D starting `83`–`87`, and X starts `44` gives D starting `9`; `8` and `10` can only start D with `8 8`, `8 10` or `10`. The scratch run below agrees.

## Checked

Applied the fix to a scratch copy of the repo and ran `dotnet test`: 86 pass, and exactly these 4 fail, all because they encode the old rule:

- `EngineeringCalculatorTests.Digit_key_appends_on_the_left`
- `EngineeringCalculatorTests.Missing_digit_key_is_invalid` (its "valid" press is a digit key on the left side)
- `LeftEditsTests.Digit_key_alone_appends`
- `DivisionShortcutTests.Digit_example_41_to_48_with_digit_8`

Every "After" value in the table was checked in the same scratch copy. No line from Solve for 41 → 48 (default + `3`), 41 → 105 (`1X, X1, X0, X5, 10, 3, ÷, flip`) or 7 → 250 (`X0, X2, X5, 10, 3, ÷`) pressed `10` or a digit key on the left side.

## Edge cases

- A puzzle whose only way to make a left-side digit was a digit key now has fewer lines, or none. That's correct.
- `Buttons` with only digit keys and `÷` still passes validation ("needs ÷ and 10 or a digit key"): the keys can type D.
- `reset` on the right side is still untested in the game and still treated as invalid (unchanged).

## Open questions

None blocking. Pressing a digit key on the left side in the game either does nothing or is disabled; the solver treats both the same (invalid), since a wasted press is never part of a shortest line.

## Out of scope

- Speeding up `Solve` (it may get slightly faster, since `LeftEdits` has fewer moves; not measured).
- Press sequences for the Modulo Shortcut, and [cost-per-line](../cost-per-line/requirements.md).
- [streaming-results](../streaming-results/requirements.md). If it's implemented first, this fix still applies unchanged: it touches the search's inputs, not how results are shown.
