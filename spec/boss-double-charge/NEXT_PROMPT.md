# Next prompt: implement Boss rule 6 (each press costs 2 charges)

Implement the spec in `spec/boss-double-charge/`.

## Read first

1. `spec/boss-double-charge/requirements.md` and `design.md`.
2. `docs/boss-rules.md` (rule 6 row).
3. Code: `CalculatorPuzzle/EngineeringCalculator.cs` (`Buttons` record, `Fits`, `Drained`, `Spare`, `Run` and its `TypedText`), `DivisionShortcut.cs` (`ChargeValue`, `TypeOperand`), `TwoOperatorSearch.cs` (`Next`), `ArithmeticSearch.cs` (`RemorphKeys`), `MainForm.cs` (`StartDivisionSearch`, the `Rule 12:` status prefix, the `bossRule is 2 or 6 or 9` line).
4. `spec/more-buttons/design.md` for how the charge key was added.

## Steps

1. `Buttons`: `ChargeStep`, `ChargeValue`, `TakeTwo` (built with the constructor, not `with`), `EngineeringCalculator.TakeTwoRule`. Tests 1, 2, 4, 5.
2. Switch the charge key call sites in the design's table to `ChargeValue`. Tests 3, 6, 7, 8. Grep `ChargeStart` afterwards.
3. `MainForm`: `TakeTwo` for Boss 6 (before the Required check), the status prefix and "no line" text, rule 6 out of "not supported".
4. Add Test Q as `user-verification/test-q.md` and a row in `user-verification/README.md` (steps Q1–Q4 from the design). If `test-q.md` already exists, use the next free letter and say so.
5. Update the docs: the rule 6 row in `docs/boss-rules.md`, the `EngineeringCalculator.cs` / `MainForm.cs` notes in `CalculatorPuzzle/README.md`, and the `boss-double-charge/` line in `spec/README.md`.

## Check

- `dotnet build` and `dotnet test` (all existing tests must still pass; 560 when this spec was written).
- Run the app (`dotnet run --project CalculatorPuzzle`) with Follow game off: S 41, G 48, Buttons `X1:2, 8X:2, 4X:2, ÷:2, 10:2`. Boss empty → the line `X1 8X ÷ 10 10 = 4X` can show. Boss 6 → it can't (rejected or absent).
- In-game check is Test Q, done by the user.
