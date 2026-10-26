# Next prompt: implement Boss rule 14 (goal +1 per charge)

Implement the spec in `spec/boss-goal-per-charge/`.

## Read first

1. `spec/boss-goal-per-charge/requirements.md` and `design.md`.
2. `docs/boss-rules.md` (rule 14 row and the rule pool).
3. Code: `CalculatorPuzzle/EngineeringCalculator.cs` (`Buttons.Parse`, charges), `CalculatorPuzzle/MainForm.cs` (`StartDivisionSearch`, `OnSearchTick`, `OnGameFileChanged`, `ApplyGamePuzzle`), `RequiredPlan.cs`, `ResultLine.cs`, `GameLink.cs`, and the `Find` / `Search` signatures in `ArithmeticSearch.cs`, `DivisionShortcut.cs`, `ModuloSearch.cs`. Mod: `GameHook.MelonLoader/CalcHookCore.cs` (`CheckPuzzle`, `WritePuzzle`, `MustUseRule`).
4. `spec/boss-required-button/design.md` for how the last boss feature wired `RequiredPlan` and `accept`.

## Steps

1. Mod: `bossRule` in `puzzle.json`; in a rule-14 round, ignore goal changes when deciding to publish. Build it: `dotnet build GameHook.MelonLoader -c Release`.
2. `Buttons.Parse`: the `:Ng` glass syntax and `Buttons.Glass`. Tests.
3. `GameLink`: `GamePuzzle.BossRule`, parsed from `bossRule` (0 when absent); glass buttons written as `:1g`. Update the three existing glass tests named in the design.
4. `ResultLine.Reaches` and its `goal N` text. Test.
5. `GoalStepSearch.cs` with `Charged` and `Search`, then the tests in the design (`GoalStepTests.cs`).
6. `MainForm`: Boss box, the same-round rule-14 guard in `OnGameFileChanged`, stepped search for rule 14, status, Esc.
7. Add Test L as `user-verification/test-l.md` and a row in `user-verification/README.md` (steps L1–L4 from the design).
8. Update `CLAUDE.md` (Status: new file, `:1g` in the `Buttons.Parse` note, spec entry) and the rule-14 row in `docs/boss-rules.md`.

## Check

- `dotnet build` and `dotnet test` (500 tests before this change; all must still pass).
- Run the app (`dotnet run --project CalculatorPuzzle`), with S 41, G 45, Buttons `÷:9, 10:9, X8:9, =`, Boss 14 → one line, `÷ 10 = X8   (3 presses, goal 48, …)`. Boss empty → the normal search finds nothing for 45 with these buttons. G 46 with `X8:1g` → the same line, `goal 48`.
- In-game check is Test L, done by the user.
