# Next prompt: Boss round required button (rule 11)

**Status:** Done (2026-10-24); implemented and tested in-game. Kept for history.

In Calculate It, boss rule 11 says `You must use button "…", otherwise the answer will not be counted.` The read-only MelonLoader mod publishes the Market Demand button as `required` in `puzzle.json`, but not this one: it lives in different, public fields (`StartLvl.must_butt`, `rule`, `IsBossRound`). Publish it through the same `required` field with a new `requiredKind` (`market` / `boss`). The two never happen together (`SelectBossRule` excludes rule 11 in challenge 8). In the app, Market Demand stays as it is (lines without the button are kept, ranked last), but in boss mode only lines that press the button are shown, since the game won't accept the others. Filters go into `ArithmeticSearch.Find` and `ModuloSearch.Find` so their 100-line limits count only kept lines, and when the button is an operator only the phase that uses it runs. C#: .NET 9 WinForms + xUnit app, net472 mod.

## Read first

1. `CLAUDE.md`: the mod must stay read-only (Steam achievements).
2. `spec/boss-required-button/requirements.md` and `design.md`.
3. `spec/market-demand/requirements.md` and `design.md` (the feature this extends).
4. `GameHook.MelonLoader/CalcHookCore.cs`: `RequiredKey`, `CheckPuzzle`, `WritePuzzle`, `WriteJson`, `Poll`.
5. `CalculatorPuzzle/GameLink.cs` (`GamePuzzle`, `Parse`), `CalculatorPuzzle/MainForm.cs` (`ApplyGamePuzzle`, `StartDivisionSearch`, `OnSearchTick`, `DoneStatus`), `CalculatorPuzzle/ArithmeticSearch.cs` (`Find`, `ops`, `remorphs`, edits-only lines), `CalculatorPuzzle/ModuloSearch.cs` (`Find`), `CalculatorPuzzle/EngineeringCalculator.cs` (`Buttons.Token`, `Buttons.Uses`), `CalculatorPuzzle.Tests/GameLinkTests.cs`, `RequiredButtonTests.cs`.
6. Optional: `ilspycmd -t StartLvl "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"`: `NextLvl` (the `rule == 11` branch), `SelectBossRule`, `IsBossRound`, `ResetPreviousBossEffects`.

## Steps

1. `dotnet build`, `dotnet test`; note they pass.
2. App: `GamePuzzle.RequiredKind`, parsing, `GameLinkTests`.
3. App: `accept` / `onlyOps` parameters in `ArithmeticSearch.Find` and `accept` in `ModuloSearch.Find`, with tests (the line limit counts only accepted lines).
4. App: `Buttons.OperatorKey` and the phase-plan helper (`RequiredPlan.For`), with `RequiredPlanTests`.
5. App: `MainForm` boss mode: detect it, skip phases, filter division lines in the callback, the status note. An unsupported required button (unmapped or not in Buttons) turns boss mode off: a normal search. Then the end-to-end 41 → 48 test.
6. Mod: `RequiredKey(buttons, out kind)` with the boss branch, fingerprint, `requiredKind` in both JSON files, `bossRule` / `mustUsed` in `state.json`, log. Build with `dotnet build GameHook.MelonLoader -c Release`.
7. Add "Test K: Boss required button" as `user-verification/test-x.md` from the design, then stop and ask the user to run it.
8. After the report: Status lines to complete, "As built" in the design, `CLAUDE.md` (the mod bullet, the `market-demand/` entry's "boss rule 11 is a follow-up" note, and a `boss-required-button/` entry).

## Check the work

- Both builds with no warnings; `dotnet test` all pass.
- `dotnet run --project CalculatorPuzzle` with a hand-written `puzzle.json` (41 → 48, buttons `operator/`, `number10`, `operator_x8`, `operator_8x`, `operator_reverse`, `"required": "operator_reverse"`, `"requiredKind": "boss"`, bump `seq`): the Required box shows `flip`, every line uses `flip` (`÷ 10 = X8` is not shown), the status ends with `use flip (boss: other lines don't count)`. Change `requiredKind` to `market` and bump `seq`: `÷ 10 = X8` comes back with `no flip`. With `"required": "operator/"` and `boss`, only division lines appear.
