# Next prompt: Digit keys work on the right side only (bugfix)

**Status:** Done.

Fix a wrong rule in this repo's Engineering Calculator model (C#, .NET 9, xUnit): digit keys (`10` and `0`–`9`) only work on the right side, after an operator. The code lets them append to the left side too. Make the simulator, the left-edit search and the Division Shortcut follow the real rule.

## Read first

1. `CLAUDE.md`: project rules and the button table (already corrected: digit keys are "Right side only").
2. `spec/digit-keys-right-side-only/requirements.md`: the rule, what must change, the before/after examples, and the 4 tests known to break.
3. `spec/digit-keys-right-side-only/design.md`: the exact code changes and the test plan.
4. `CalculatorPuzzle/EngineeringCalculator.cs`, `LeftEdits.cs`, `DivisionShortcut.cs` and the three matching test files in `CalculatorPuzzle.Tests/`.

## Steps

1. Run `dotnet test` first and note that everything passes (90 tests), so later failures are yours.
2. `EngineeringCalculator.cs`: guard `Key.Ten` and `Key.Digit` with `pending`, drop their left-side branches, update `Run`'s doc comment. Update the two simulator tests and add the new ones from the design.
3. `LeftEdits.cs`: remove the digit-key move and the `10` moves, update the summary comment. Update and add the left-edit tests.
4. `DivisionShortcut.cs`: remove `DigitKeys` from `prefixDigits`. Update `Digit_example_41_to_48_with_digit_8`, add `Digit_key_and_X8_41_to_48`, and extend the "own side" test as the design says.
5. Update the docs listed under "Docs to update after the fix" in the design, and set the Status line in all three files of this spec to Done.

## Check the work

- `dotnet build` with no warnings.
- `dotnet test`: all tests pass. The only existing tests whose assertions change are the 4 named in the requirements (plus the renamed/extended ones the design lists). If any other existing test fails, stop and report it rather than changing its expected values.
- `dotnet run --project CalculatorPuzzle`:
  - S = 41, G = 48, Buttons `4X, X4, 10, 8, ÷`, Enter: the status says "No division line fits the available buttons."
  - S = 41, G = 48, default buttons, Enter: `8X ÷ 10 = flip   (4 presses)` is still near the top.
  - S = 531, G = 11, Buttons `1X, 10, 4, 5, ÷`, Enter: `÷ 4 5 =   (3 presses)` is first.
  - No listed line has `10` or a single digit before `÷` or after `=`.
