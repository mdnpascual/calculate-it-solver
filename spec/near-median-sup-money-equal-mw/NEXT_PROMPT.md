# Next prompt: near, median, sup, money, equal, mw and complement buttons

**Status:** Done (2026-10-25). Nothing left to implement.

Add seven Calculate It buttons to the solver and the read-only MelonLoader mod: `near` (goal if within 10, else + 10), `median` (midpoint with the goal, rounded up), `sup` (digit sum, display under 6 characters), `money` (+ the player's coins, display under 5 characters), `==` (the game's `operator_equal`: operator plus one key, every digit becomes the key's first digit) and `mD` (the game's `number_mw` once it stores a number: an operand key that types it) and `AX` (the game's `operator_complement`: operator plus one key, display under 6 characters, prepends the key's whole number). The money amount changes during a line in some modes; the simulator models that. The repo is C#: .NET 9 WinForms + xUnit for the app, net472 for the mod.

## Read first

1. `CLAUDE.md`: project rules. The mod must stay read-only (Steam achievements).
2. `spec/near-median-sup-money-equal-mw/requirements.md` and `design.md`.
3. `CalculatorPuzzle/README.md` ("Files").
4. `CalculatorPuzzle/LeftTransforms.cs` (`Create`, `Number`, `Last`, the `p` entry), `CalculatorPuzzle/LeftSearch.cs` (how `Preimages` / `Ascending` are used).
5. `CalculatorPuzzle/EngineeringCalculator.cs` (`Key`, `Press`, `Buttons`, `Buttons.Parse`, `Run`, the remorph and quest handling), `CalculatorPuzzle/ArithmeticSearch.cs` (`RemorphKeys`, the remorph block), `CalculatorPuzzle/TwoOperatorSearch.cs` (`IsRemorph`, the operand list), `CalculatorPuzzle/DivisionShortcut.cs` (`TypeOperand`, `CheapestDivisor`, line 136), `CalculatorPuzzle/GoalStepSearch.cs`, `CalculatorPuzzle/RequiredPlan.cs`.
6. `CalculatorPuzzle/GameLink.cs`, `CalculatorPuzzle/MainForm.cs` (the Last box, `ApplyGamePuzzle`, the search tasks).
7. `GameHook.MelonLoader/CalcHookCore.cs` (button snapshot, `QuestDigit`, fingerprint, `WritePuzzle`, the `state.json` writer, `SymbolOf`), `GameHook.MelonLoader/ButtonSymbols.cs`, `GameHook.MelonLoader/README.md`.
8. Tests to change: `GameLinkTests.cs` and `BlockedButtonTests.cs` (use `operator_near` as the unmapped sample), `ButtonSymbolsTests.cs` (unmapped id list).
9. Optional: `ilspycmd -t MainCalc "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"` (`AddSome`, `Median`, `Sup`, `AddNumberMoney`, `MorphAll`, `ReplaceDigits`, `Complement`, `AddToBegin`, `Absorb`, `AddNumber`, `Send`), and `CountUses.SubUse`, `NumberReader`.

## Steps

1. `dotnet build` and `dotnet test`; note that everything passes.
2. Transforms: `Reads`, `Bind`, `Near`, `Median`, `Money`, and `sup` in `Create`, with preimages; tests.
3. `Buttons.Parse(text, last, goal, money)`, `WithGoal`, `AllDigits`, `Complement`, `MemoryValue`, `Key.AllDigits`, `Key.Complement`, `Key.Memory`; tests.
4. Simulator: `LineContext`, `MoneyRules`, the charged-press count, money drains and passives, goal rebinding, `==`, `AX`, mw typing; tests.
5. Phases: arithmetic (`==`, `AX`, mw in `RemorphKeys`), two-operator, division (`TypeOperand`, the path-B test), `GoalStepSearch` (`WithGoal`, context), `RequiredPlan`; the Examples table as tests.
6. `GameLink`: the seven ids, `stored`, `coins`, `diffType`, `coinPassives`; move the unmapped test samples to `operator_random`.
7. `MainForm`: Money box, context, status notes.
8. Mod: `stored`, `coins`, `diffType`, `coinPassives`, the mw fingerprint, overlay tokens, `ButtonSymbols`. Build with `dotnet build GameHook.MelonLoader -c Release`.
9. Docs: `docs/button-id.md` rows, `CLAUDE.md`, `CalculatorPuzzle/README.md`, `GameHook.MelonLoader/README.md`, `spec/README.md`.
10. Add the in-game test from the design ("Test V") as `user-verification/test-<next letter>.md` plus a row in `user-verification/README.md` (T and U may already be taken by other specs; use the next free letter). Then stop and ask the user to run it. You can't run the game.
11. After the report: set the Status lines, add "As built" to the design.

## Check the work

- `dotnet build` and `dotnet build GameHook.MelonLoader -c Release`, no warnings.
- `dotnet test`: all pass.
- `dotnet run --project CalculatorPuzzle`: S `483`, G `777`, Buttons `==:1, 7:1` gives `== 7` (2 presses). S `41`, G `48`, Buttons `near:1` gives `near` (1 press). S `36`, G `48`, Money `12`, Buttons `money:1` gives `money` (1 press). S `41`, G `741`, Buttons `AX:1, 7:1` gives `AX 7` (2 presses).
