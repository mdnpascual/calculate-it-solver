# Next prompt: implement Boss rule 12 (every `=` adds 1)

**Status:** Complete (2026-10-25). Kept for reference; the only follow-up is the bare `=` in the middle of a line (N5).

Implement the spec in `spec/boss-send-plus-one/`.

## Read first

1. `spec/boss-send-plus-one/requirements.md` and `design.md`.
2. `docs/boss-rules.md` (rule 12 row).
3. `CalculatorPuzzle/README.md` ("The tricks", "Files").
4. Code: `CalculatorPuzzle/EngineeringCalculator.cs` (`Run`, `Apply`, `Cost`), then the four phases: `ArithmeticSearch.Find` (`Keep`, the `(x, y)` loop, `%` range, the edits-only lines), `TwoOperatorSearch.Find` (forward half `middle`, backward half, `Keep`), `DivisionShortcut.SearchCore` (`AddLine`, Path A and Path B), `ModuloSearch.Find`. Then `MainForm.StartDivisionSearch` and the modulo `Task.Run` in the tick.

## Steps

1. `EngineeringCalculator`: `SendPlusOneRule`, `Run(..., plusOne)` (+1 on every `=`, bare `=` as the last press), `Sent`. Tests 1–5 of the design.
2. `ModuloSearch.Find(..., plusOne)`: the simplest phase (its lines end with `=`). Test 12.
3. `ArithmeticSearch.Find(..., plusOne)`: target `t`, `want = y − b`, the `=` line for S = G − 1, `Sent` check. Tests 6–9.
4. `DivisionShortcut.Search` / `Solve` (`plusOne`): pieces of `t`, `qd = q − b`. Tests 10–11.
5. `TwoOperatorSearch.Find(..., plusOne)`: M + 1, op2 for `Y2 − b`. Test 13. Then test 14.
6. `MainForm`: pass `plusOne` to all four phases (store it for the modulo task), status texts, drop 12 from the `not supported` note.
7. Add Test N as `user-verification/test-n.md` (steps N1–N5 from the design) and a row in `user-verification/README.md`.
8. Update `CalculatorPuzzle/README.md` (file notes for the changed files; a short rule-12 note in "Search order"), the rule-12 row and the notes in `docs/boss-rules.md`, and the spec line in `spec/README.md`. Set the spec files' status lines.

## Check

- `dotnet build` and `dotnet test` (547 tests before this change; all must still pass, and none of the existing tests change).
- Run the app (`dotnet run --project CalculatorPuzzle`): S 41, G 59, Buttons `÷:9, 10:9, X8:9, =`, Boss 12 → `÷ 10 = X8` (3 presses), status starts `Rule 12: every = adds 1:`. Boss empty → no line for 59 with those buttons. S 47, G 48, Boss 12 → the `=` line, 0 presses.
- In-game check is Test N, done by the user.
