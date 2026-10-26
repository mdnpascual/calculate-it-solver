# Next prompt: Division Shortcut

**Status:** Done. The prompt below was carried out (see "As built" in [design.md](design.md)). Not needed again unless the feature is rebuilt. Open follow-ups: speed up `Solve`, and single digit keys (v2, needs its own spec).

Implement the Division Shortcut for the Engineering Calculator in this repo (C#, .NET 9, xUnit).

## Read first

1. `CLAUDE.md`: project rules and the Engineering Calculator button notation (`dX` prepends and `Xd` appends, on the left side only; `10` works on the right side only; `=` is free).
2. `spec/division-shortcut/requirements.md`: the formula, rules, examples and scope.
3. `spec/division-shortcut/design.md`: types, file layout, algorithm and test plan.
4. `CalculatorPuzzle/ModuloShortcut.cs` and `CalculatorPuzzle.Tests/ModuloShortcutTests.cs`: the style to match.

## Steps

1. Write `CalculatorPuzzle/EngineeringCalculator.cs` (the `Key`, `Press` and `Buttons` types, `Buttons.Parse`, `Run` and `Cost`) and its tests. Get those passing first, since the shortcut uses the simulator to check its lines.
2. Write `CalculatorPuzzle/LeftEdits.cs` (the left-edit search with `flip`) and `LeftEditsTests`.
3. Write `CalculatorPuzzle/DivisionShortcut.cs` following the design's algorithm, steps 1 to 8.
4. Write `CalculatorPuzzle.Tests/DivisionShortcutTests.cs` covering every item in the design's test plan, including each row of every example table in the requirements.
5. Add the Division Shortcut section to `CalculatorPuzzle/MainForm.cs` as described in the design's "UI" section. Leave the Modulo Shortcut rows working as they are.
6. Update the Status section of `CLAUDE.md` to list the three new files and the new UI section.

## Check the work

- `dotnet build` with no warnings.
- `dotnet test` with all tests passing, including the existing Modulo Shortcut tests.
- `dotnet run --project CalculatorPuzzle`: on startup (S = 41, G = 48, default Buttons) the Division Shortcut section lists 4-press lines `8X ÷ 10 = flip` and `÷ 10 = 8X flip` first. Removing `flip` from the Buttons box changes the cheapest lines to 6 presses, including `X1 8X ÷ 10 10 = 4X`. Typing `4X, X4, 10, ÷` shows the "no line fits" error, and typing `Y4` shows an unknown-button error.
- If an example row in the requirements turns out to be wrong, stop and report it instead of changing the expected values.
