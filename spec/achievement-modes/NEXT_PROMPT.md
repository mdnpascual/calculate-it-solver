# Next prompt: implement Achievement modes (School mathematics, Just a one)

**Status:** Done (2026-10-25). Test T confirmed in game except T3 (untested).

Implement the spec in `spec/achievement-modes/`.

## Read first

1. `spec/achievement-modes/requirements.md` (game facts: how `SCHOOL` and `UNIT` trigger, the button id table) and `design.md`.
2. `CalculatorPuzzle/README.md` ("The tricks", "Search order", "Files").
3. Code: `CalculatorPuzzle/EngineeringCalculator.cs` (`Key`, `Press`, `Apply`, `Run`, `OneSend`, `Buttons.Fits`), `LeftSearch.cs` (the left-side move function and limits), `TwoOperatorSearch.cs` (`Operands`), `ArithmeticSearch.cs` (`Find`, `BuildYs`), `DivisionShortcut.cs` (`TypeOperand`, `CheapestDivisor`, `Search`), `ModuloSearch.cs` (`Find`), `ResultLine.cs`, then `MainForm.cs` (`StartDivisionSearch`, `OnSearchTick`, `OnStepTick`, `Redraw`, `StopDivisionSearch`).
4. Optional, to check a game detail: `ilspycmd -t MainCalc "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"` (`Send`, `Count`), `-t StartLvl` (`Update`: the `SCHOOL` check), `-t DifferentOperatorsCount`, `-t CountUses` (`SubUse`: `LastButId`).

## Steps

1. `AchievementMode.cs`: the enum, `OpBit`, `AllFour`, `FourOps`, `Credit`. Design tests 1–2.
2. `JustOneSearch.cs`: `OneEqualsAtEnd`, `Search` (arithmetic, division, modulo for S + 1 with the filter), `Order`. Tests 9–14. This one reuses the phases, so do it before the new search.
3. `ResultLine.cs`: `School`, `Credit`, `From(SchoolSearch.Line, …)`, the `[credit]` tag in `ToString`.
4. `SchoolSearch.cs`: the best-first search over (display, mask, segments) with labels for charges, the closing segment by formula, the 12-operator cap, the state cap and cancel. Tests 3–8. Time test 3 and 4 in Release and write the times in the design's status line.
5. `MainForm.cs`: the Achievements header and panel, the two radio buttons (`AutoCheck = false`, click toggles), the dispatch in `StartDivisionSearch`, the tick handling, `Redraw` order for Just a one, Esc / stop / close, status texts.
6. Add Test T as `user-verification/test-t.md` (steps T1–T5 from the design) and a row in `user-verification/README.md`.
7. Update `CalculatorPuzzle/README.md` (file notes for the new and changed files; a short "Achievement modes" part under "Search order"), the spec line in `spec/README.md`, and the spec files' status lines.

## Check

- `dotnet build` and `dotnet test` (all existing tests must still pass unchanged).
- Run the app (`dotnet run --project CalculatorPuzzle`):
  - S 41, G 48, Buttons `+:9, -:9, ×:9, ÷:9, 0:9, 1:9, 7:9`, School mode → top line costs 8 (`+ 7 = × 1 = ÷ 1 = - 0 =` or another 8-press order), status starts `School mathematics:`.
  - S 4, any G, Buttons `5X:9, ÷:9, 10:9, +:9, 1:9`, Just a one → lines `+ 1 = … [digit]` and `5X ÷ 10 = … [÷]` both in the top lines, status lists both credits.
  - Click the active radio again → normal search, header `▸ Achievements` when collapsed.
  - School mode with `÷` missing → the `needs + - × ÷ (missing: ÷)` status.
- In-game check is Test T, done by the user.
