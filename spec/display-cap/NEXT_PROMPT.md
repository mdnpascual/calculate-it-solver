# Next prompt: implement the 999 999 cap and the 6-character display

**Status:** In testing (implemented 2026-10-25). Kept for reference; waiting for Test O, done by the user.

Implement the spec in `spec/display-cap/`.

## Read first

1. `spec/display-cap/requirements.md` and `design.md`.
2. `CalculatorPuzzle/README.md` ("The calculator model", "Files").
3. Code: `CalculatorPuzzle/EngineeringCalculator.cs` (`Limit`, `MaxDigits`, `Run`, `Apply`), `LeftTransforms.cs` (`Limit`, `Number`, the preimage helpers), `LeftSearch.cs` (`DefaultLimits`, the move list), `LeftEdits.cs` (`Plan`), `DivisionShortcut.cs` (`TypeOperand`, `CheapestDivisor`, Path A loop, `AddLine`), `ModuloSearch.cs` (the k loop), `TwoOperatorSearch.cs` (`Limit`, operand list, forward `middle`, backward `×`).
4. `spec/boss-send-plus-one/design.md` if it is implemented by then (its +1 comes after the clamp).

## Steps

1. `EngineeringCalculator`: `Max`, `MaxDisplay`, `MaxOperandTyping`; clamp in `Apply`; remove `Limit`, `MaxDigits`, `full`; the `Xd` / `dX` and operand-key rules in `Run`. Run `dotnet test` and list the tests that now fail; fix each one to the game rule (design "Tests"), not the code back.
2. `LeftTransforms`: the clamps and bounds. `LeftSearch`, `LeftEdits`: the 6-character rule.
3. `DivisionShortcut`: `TypeOperand`, `CheapestDivisor`, Path A r ≤ 3, X length check. `ModuloSearch`: the k bound. `TwoOperatorSearch`: `Limit`, operands, `×` bound.
4. New tests `CalculatorPuzzle.Tests/DisplayCapTests.cs` (design tests 1–11).
5. Time 41 → 483 with every button (Release) before and after; note both in the design's "As built".
6. Add Test O as `user-verification/test-o.md` (steps O1–O5) and a row in `user-verification/README.md` (use the next free letter if O is taken).
7. Update `CalculatorPuzzle/README.md` (model and file notes), the spec line in `spec/README.md`, and the status lines of the three spec files.

## Check

- `dotnet build` and `dotnet test`: all pass. Write the new test count in `spec/README.md`.
- Run the app (`dotnet run --project CalculatorPuzzle`): S 41, G 483 with every button: no line shows a display above 6 characters built by `Xd` / `dX`, and no operand above 6 characters.
- In-game check is Test O, done by the user.
