# Next prompt: implement Boss rule 13 (one `=` per line)

**Status:** Implemented 2026-10-25 (607 tests). Test R (in-game) R1–R4 confirmed.

Implement the spec in `spec/boss-one-send/`.

## Read first

1. `spec/boss-one-send/requirements.md` and `design.md`.
2. `docs/boss-rules.md` (rule 13 row; rule 12 for how `=` sends).
3. `CalculatorPuzzle/README.md` ("Search order", "Files").
4. Code: `CalculatorPuzzle/RequiredPlan.cs`, `EngineeringCalculator.cs` (`Key`, `Press`, `SendPlusOneRule`), then `MainForm.StartDivisionSearch`, `OnSearchTick` (no-line status, modulo start) and `DoneStatus`.

## Steps

1. `EngineeringCalculator`: `RerollOnMissRule`, `OneSend`. Tests 1–2 of the design.
2. `RequiredPlan.OneSend()`. Tests 3–4.
3. Phase tests 5–9 through `accept` (no phase code changes; if one fails, find out why before changing a phase).
4. `MainForm`: wrap the plan for Boss 13, `searchOneSend`, status prefix and no-line text.
5. Add Test R as `user-verification/test-r.md` (steps R1–R4 from the design) and a row in `user-verification/README.md` (Test Q belongs to `boss-double-charge/`; if Q's file doesn't exist yet, still use R).
6. Update `CalculatorPuzzle/README.md` (file notes for `EngineeringCalculator.cs`, `RequiredPlan.cs`, `MainForm.cs`; a short rule-13 note in "Search order"), the rule-13 row and notes in `docs/boss-rules.md`, and the spec line in `spec/README.md`. Set the status lines of the three spec files.

## Check

- `dotnet build` and `dotnet test` (all existing tests must still pass, unchanged).
- Run the app (`dotnet run --project CalculatorPuzzle`): S 41, G 48, Buttons `÷:9, 10:9, X8:9`, Boss 13 → no line, status `No line reaches 48 with one = at the end.`; Boss empty → `÷ 10 = X8`. S 41, G 4, Boss 13 → `÷ 10 =`, status starts `Rule 13: one = per line:`.
- In-game check is Test R, done by the user.
