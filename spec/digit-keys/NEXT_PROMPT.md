# Next prompt: Digit keys

**Status:** Done. The spec is implemented; this prompt is kept for reference.

Add single digit keys (`0`–`9`) to the Engineering Calculator in this repo (C#, .NET 9, xUnit), use them in the Division Shortcut, and make two small changes to the Division Shortcut UI.

## Read first

1. `CLAUDE.md`: project rules and the button notation.
2. `spec/digit-keys/requirements.md`: what digit keys do, the examples, and the two UI changes.
3. `spec/digit-keys/design.md`: types, the D search, the second search path, UI changes and test plan.
4. `spec/division-shortcut/requirements.md` and `design.md`: the feature this builds on.
5. `CalculatorPuzzle/EngineeringCalculator.cs`, `LeftEdits.cs`, `DivisionShortcut.cs`, `MainForm.cs` and the three matching test files.

## Steps

1. Run `dotnet test` first and note that everything passes, so later failures are yours.
2. `EngineeringCalculator.cs`: add `Key.Digit`, `Buttons.DigitKeys`, parsing and the `Run` case. Fix any `new Buttons(...)` call sites. Add the parser and simulator tests.
3. ~~`LeftEdits.cs`: digit keys as left-side appends, preferring `Xd`. Add the tests.~~ (Wrong: digit keys work on the right side only. See [digit-keys-right-side-only](../digit-keys-right-side-only/requirements.md).)
4. `DivisionShortcut.cs`: `TypeOperand`, `CheapestDivisor`, the new validation message, and Path B. Leave Path A as it is. Add the tests, including every row of the requirements' examples table.
5. `MainForm.cs`: the shorter line format and no search at startup.
6. Update `CLAUDE.md`: the button table (digit keys), the Status section, and mark this spec implemented (set the Status lines in all three spec files, and add an "As built" section to the design if anything differs).

## Check the work

- `dotnet build` with no warnings.
- `dotnet test` with all tests passing, including the existing Division Shortcut tests, unchanged.
- `dotnet run --project CalculatorPuzzle`:
  - On startup the Lines box is empty and the status says "Press Enter to recalculate." The Modulo Shortcut rows are filled in.
  - Pressing Enter with S = 41, G = 48 and the default buttons lists `8X ÷ 10 = flip   (4 presses)` near the top. Rows show only presses and the count.
  - Adding `, 3` to the Buttons box and pressing Enter adds `5X flip ÷ 3 =   (4 presses)`.
  - S = 531, G = 11, Buttons `1X, 10, 4, 5, ÷` gives `÷ 4 5 =   (3 presses)` first.
  - Buttons `25` shows `Unknown button "25"`.
- Time a search with all buttons and a 3-digit G. If it's clearly slower than before, say so in the report with the numbers instead of quietly lowering limits.
- If an example row in the requirements turns out to be wrong, stop and report it instead of changing the expected values.
