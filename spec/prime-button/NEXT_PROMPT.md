# Next prompt: Prime button (`operator_prime`)

**Status:** Done (2026-10-24).

Add the `operator_prime` button to the Calculate It solver. It sets the left side to the nearest prime, ties going down (`8084` → `8081`, `88` → `89`, `0` → `2`). The solver gets a left-side transform `p`; the read-only MelonLoader mod shows it as `p<nearest prime>` in the calculator overlay and `p` in the shop overlay. The repo is C#: .NET 9 WinForms + xUnit for the app, net472 for the mod.

If `spec/shop-charge-total/` is also pending, it touches the same mod file and test (Test I/J); it is fine to do both in one session and one in-game test pass.

## Read first

1. `CLAUDE.md`: project rules. The mod must stay read-only (Steam achievements).
2. `spec/prime-button/requirements.md` and `design.md`.
3. `CalculatorPuzzle/LeftTransforms.cs` (`Create`, `Number`, the `sqrt` and `round` entries as models), `CalculatorPuzzle/LeftSearch.cs` (how `Preimages` and `Ascending` are used).
4. `CalculatorPuzzle/GameLink.cs` (`MapButton`), `GameHook.MelonLoader/ButtonSymbols.cs`, `GameHook.MelonLoader/CalcHookCore.cs` (`SymbolOf`, `DrawOverlay`, `DrawShopCell`).
5. `CalculatorPuzzle.Tests/LeftTransformTests.cs`, `ButtonSymbolsTests.cs`, `GameLinkTests.cs` (uses `operator_prime` as the unmapped sample; switch it).
6. Optional: `ilspycmd -t MainCalc "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"` and look at `Prime`, `FindNearestPrime`, `IsPrime`.

## Steps

1. `dotnet build` and `dotnet test`; note that everything passes.
2. `Primes.cs` with `PrimeTests`.
3. The `p` transform in `LeftTransforms` and its preimages, with tests; then `MapButton`, and move the "unmapped" test samples to `operator_near`.
4. `LeftSearchTests`: 88 → 89, 120 → 113, 42 → 43.
5. Mod: `ButtonSymbols` (`Token`, `NearestPrime`, the `Of` overload), `SymbolOf` with the cached live prime. Build with `dotnet build GameHook.MelonLoader -c Release`.
6. Fill in `operator_prime` in `docs/button-id.md` (`p`: nearest prime, ties down, left side only).
7. Add "Test I: Prime button" as `user-verification/test-x.md` from the design, then stop and ask the user to run it. You can't run the game.
8. After the report: answer the open questions, set the Status lines to complete, add "As built" to the design, update `CLAUDE.md` (transform list, mod overlay, a `prime-button/` entry under specs).

## Check the work

- `dotnet build` and `dotnet build GameHook.MelonLoader -c Release`, no warnings.
- `dotnet test`: all pass.
- `dotnet run --project CalculatorPuzzle`: S `8084`, G `8081`, Buttons `p:1, X1:1` gives `p` (1 press).
