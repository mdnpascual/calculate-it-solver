# Design: Digit keys work on the right side only (bugfix)

**Status:** Done.

Three small code changes and a handful of test updates. No new types, no UI change.

## Where code goes

- `CalculatorPuzzle/EngineeringCalculator.cs`: `Run` rejects `10` and digit keys on the left side.
- `CalculatorPuzzle/LeftEdits.cs`: drop the digit-key and `10` moves.
- `CalculatorPuzzle/DivisionShortcut.cs`: drop `DigitKeys` from the prefix digits.
- Tests in `EngineeringCalculatorTests.cs`, `LeftEditsTests.cs` and `DivisionShortcutTests.cs`.

`Key`, `Press`, `Buttons`, `Buttons.Parse`, `TypeOperand`, `CheapestDivisor`, `MainForm` and `ModuloShortcut` don't change.

## Simulator (`EngineeringCalculator.Run`)

Guard both cases with `pending`, the same way the left-only cases guard with `!pending`. A press that matches no case falls to `default: return null`, so a left-side digit key becomes invalid with no extra code:

```csharp
case Key.Ten when pending && (buttons == null || buttons.Ten):
    operand += "10";
    log.Add(operand);
    break;
case Key.Digit when pending && press.Digit is >= 0 and <= 9
        && (buttons == null || buttons.DigitKeys.Contains(press.Digit)):
    operand += press.Digit;
    log.Add(operand);
    break;
```

The `else` branches that appended to `display` go away. Update the doc comment's list of invalid presses: "a left-side-only button on the right side, `10` or a digit key on the left side, ÷ by 0, or = with nothing pending".

## Left edits (`LeftEdits.Plan`)

- Delete the `Key.Digit` move (the `rightDigit is { } rd && !buttons.AppendDigits.Contains(rd) && buttons.DigitKeys.Contains(rd)` block).
- Delete the whole `if (buttons.Ten) { ... }` block (both the plain and reversed `10` moves).
- Summary comment: "Fewest left-side presses (dX, Xd, flip) ...".

The state space and BFS are otherwise unchanged. The "appends are tried first" comment still holds.

## Division Shortcut (`DivisionShortcut.Solve`)

```csharp
IReadOnlySet<int> prefixDigits = buttons.Flip
    ? buttons.PrependDigits.Union(buttons.AppendDigits).ToHashSet()
    : buttons.PrependDigits;
```

That's the only change. Pre and post edits come from `LeftEdits.Plan`, so they're fixed by the change above. `AddLine` already runs every line through `Run` with the puzzle's buttons, so even if a left-side digit slipped through, the corrected simulator would drop it. Validation, Path A's `10` divisors, Path B's `DigitKeys.Count > 0` gate, `TypeOperand` and `CheapestDivisor` stay as they are: they're all about the right side.

## Test plan

Existing tests that encode the old rule (these are the 4 that fail once the code is fixed; all others pass unchanged):

| Test | Change |
|---|---|
| `EngineeringCalculatorTests.Digit_key_appends_on_the_left` | Rename `Digit_key_is_invalid_on_the_left`; assert `Run(41, [Digit 3])` is null. |
| `EngineeringCalculatorTests.Missing_digit_key_is_invalid` | Press the keys after `÷`: with `4, 10, ÷`, `Run(41, [÷, Digit 3])` is null and `Run(41, [÷, Digit 4])` is not null. |
| `LeftEditsTests.Digit_key_alone_appends` | Rename `Digit_key_is_not_a_left_edit`; assert `Plan("4", "48", B("8"))` is null. |
| `DivisionShortcutTests.Digit_example_41_to_48_with_digit_8` | Rename `Digit_example_41_to_48_with_digit_8_has_no_line`; assert the error "No division line fits the available buttons." |

Existing test to keep but rename for clarity:

- `LeftEditsTests.Xd_is_preferred_over_the_digit_key` → `Digit_key_is_ignored_when_Xd_exists` (same assertion: `X8`).

New tests:

`EngineeringCalculatorTests`:

- `Ten_is_invalid_on_the_left`: `Run(41, [Ten])` is null.
- `Digit_keys_are_invalid_after_equals`: `Run(41, [÷, Ten, =, Digit 8])` and `Run(41, [÷, Ten, =, Ten])` are null (the result is the left side).

`LeftEditsTests`:

- `Ten_is_not_a_left_edit`: `Plan("41", "4110", B("X1, X0, 10"))` is `X1 X0`, and with `B("10")` alone it's null.

`DivisionShortcutTests`:

- `Digit_key_and_X8_41_to_48`: with `X8, 8, 10, ÷`, the top line is `÷ 10 = X8` at 3 presses.
- Extend `Edit_buttons_are_never_pressed_on_the_right_side_and_all_presses_are_available`: also assert that no press outside `÷` … `=` is `Key.Ten` or `Key.Digit`, and add a button text with digit keys and `flip` to its loop (for example `DefaultText + ", 3"`). Also run it on 41 → 105 with `1X, X1, X0, X5, 10, 3, ÷, flip`, where G contains `10`, so the old code would have reached for a left-side `10`. Rename it to `Every_press_is_on_its_own_side_and_available`.

All of these were checked in a scratch copy of the fixed code (see [requirements.md](requirements.md#checked)).

## Docs to update after the fix

- `CLAUDE.md`: drop the two "Known bug" notes (on `EngineeringCalculator.cs` and `LeftEdits.cs`) and say `LeftEdits` uses `dX`, `Xd` and `flip`; mark `digit-keys-right-side-only/` implemented in the spec list.
- The "Correction (2026-10-22)" notes in `spec/division-shortcut/design.md`, `spec/digit-keys/requirements.md` and `spec/digit-keys/design.md` say "the code still does". Change them to say the code was fixed by this spec.
- `spec/digit-keys/design.md` "As built": the last bullet says including `DigitKeys` in A is a bug; change it to say it was removed.
- This spec: set all three Status lines to Done, and add an "As built" section here if anything differs.

## As built

Implemented as designed. `dotnet build` has no warnings; `dotnet test` passes 94 tests (90 before, plus the new ones). Only the 4 named tests changed their assertions.
