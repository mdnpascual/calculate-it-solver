# Next prompt: implement Boss rules 2 and 9 (blocked buttons)

**Status:** Complete (2026-10-25). Kept for reference; Test P passed in game.

Implement the spec in `spec/boss-blocked-buttons/`.

## Read first

1. `spec/boss-blocked-buttons/requirements.md` and `design.md`.
2. `docs/boss-rules.md` (rule 2 and 9 rows and the rule pool).
3. Code: `CalculatorPuzzle/GameLink.cs` (`GamePuzzle`, `Parse`), `CalculatorPuzzle/MainForm.cs` (`ApplyGamePuzzle`, `StartDivisionSearch`: `requiredNote` and the `bossRule is 2 or 6 or 9` line). Mod: `GameHook.MelonLoader/CalcHookCore.cs` (`ButtonInfo`, `ReadButtons`, `WritePuzzle`, `WriteJson`).
4. `CalculatorPuzzle.Tests/GameLinkTests.cs` for the style of the parse tests.

## Steps

1. Mod: `ButtonInfo.Blocked` from `Blockirator.inf_blocked`, written as `"blocked"` in both JSON files. Build it: `dotnet build GameHook.MelonLoader -c Release`. Update `GameHook.MelonLoader/README.md`.
2. `GameLink.Parse`: blocked → `:0`, `GamePuzzle.Blocked`. Tests 1–7 from the design.
3. `MainForm`: `gameBlocked`, the `blocked: …` note (dropping tokens the user gave charges back), rules 2 and 9 out of the "not supported" list.
4. Add Test P as `user-verification/test-p.md` and a row in `user-verification/README.md` (steps P1–P5 from the design). If `test-p.md` already exists, use the next free letter and say so.
5. Update the docs: the rule 2 and 9 rows in `docs/boss-rules.md` (solver status), the `GameLink.cs` and `MainForm.cs` notes in `CalculatorPuzzle/README.md`, and the `boss-blocked-buttons/` line in `spec/README.md`.

## Check

- `dotnet build` and `dotnet test` (all existing tests must still pass; 560 when this spec was written).
- Run the app (`dotnet run --project CalculatorPuzzle`) with Follow game off: S 41, G 48, Buttons `÷:3, 10:4, X8:0, 8X:2, flip:1` → top line `8X ÷ 10 = flip` (4 presses).
- In-game check is Test P, done by the user.
