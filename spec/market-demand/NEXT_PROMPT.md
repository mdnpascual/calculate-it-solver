# Next prompt: Market Demand (required button)

**Status:** Done (2026-10-24). Nothing left to implement; kept as the record of the prompt.

Add Market Demand support to the Calculate It solver. In that challenge each round names one button; if the round ends without a press of it, the round reward is `$0`. The read-only MelonLoader mod must report the required button in `puzzle.json` and `state.json`. The WinForms app gets a Required box above Buttons, filled from the game when **Follow game** is on, and ranks lines that press the required button first. The repo is C#: .NET 9 WinForms + xUnit for the app, net472 for the mod.

## Read first

1. `CLAUDE.md`: project rules. The mod must stay read-only (Steam achievements): only read fields, no presses, no charge changes, no game or save writes, no new patches.
2. `spec/market-demand/requirements.md` and `design.md`.
3. `GameHook.MelonLoader/CalcHookCore.cs` (`ButtonInfo`, `ReadButtons`, `CheckPuzzle`, `WritePuzzle`, `WriteJson`, `Poll`).
4. `CalculatorPuzzle/GameLink.cs` (`GamePuzzle`, `Parse`), `CalculatorPuzzle/EngineeringCalculator.cs` (`Buttons.Parse`, `Charges`, `Press.ToString`), `CalculatorPuzzle/ResultLine.cs`.
5. `CalculatorPuzzle/MainForm.cs`: the layout rows, `ApplyGamePuzzle`, `StartDivisionSearch` (division cap), `OnSearchTick` (modulo cap), `DoneStatus`.
6. `CalculatorPuzzle.Tests/GameLinkTests.cs` for the test style; `user-verification/` ("Test D") for the manual test format.
7. Optional, to check a game detail: decompile with `ilspycmd -t StartLvl "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"` and look at `BeginChallengeButtonRound`, `RecordChallengeButtonUse`, `CanEarnRoundCoins`.

## Steps

1. Run `dotnet build` and `dotnet test` first and note that everything passes.
2. App: `GamePuzzle.Required` and its parsing in `GameLink.Parse`, with the `GameLinkTests` cases.
3. App: `Buttons.Token` and `Buttons.Uses`, with tests.
4. App: `ResultLine` (`Missing`, `Required`, ranking key, `ToString`), with tests, then the end-to-end 41 → 48 test.
5. App: `MainForm`: Required row above Buttons, Enter starts the search, `ApplyGamePuzzle` fills it, both caps count only lines that press it, status text.
6. Mod: reflection fields, `ObjectName`, `RequiredKey`, fingerprint, `puzzle.json` / `state.json` fields, log. Build with `dotnet build GameHook.MelonLoader -c Release`.
7. Add "Test E: Market Demand" as `user-verification/test-x.md` from the design. Then stop and ask the user to run it. You can't run the game.
8. After the user reports back, record the answers to the two open questions, update the Status lines in all three spec files, add an "As built" section to the design, and update `CLAUDE.md` (the mod bullet, `GameLink`, `MainForm` and a `market-demand/` entry under specs).

## Check the work

- `dotnet build` (solution) and `dotnet build GameHook.MelonLoader -c Release`, both with no warnings.
- `dotnet test`: all tests pass, old and new.
- `dotnet run --project CalculatorPuzzle` with no game running: write a `puzzle.json` by hand into `%LOCALAPPDATA%\CalcHook\` (41 → 48, buttons `operator/`, `number10`, `operator_x8`, `operator_8x`, `operator_reverse`, `"required": "operator_reverse"`) and bump `seq`. The Required box shows `flip`, the first line uses `flip`, `÷ 10 = X8` shows `no flip`. A file without `required` clears the box and gives today's results.
