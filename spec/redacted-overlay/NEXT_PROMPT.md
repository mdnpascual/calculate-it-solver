# Next prompt: Redacted challenge overlay (challenge 9)

**Status:** Complete (2026-10-24). Tested in-game (Test F).

Add an in-game overlay to the read-only MelonLoader mod for Calculate It. In challenge 9 (the game calls it *Alchemy*) every button's label is replaced by a random symbol. The mod still reads the real button ids. Draw a small calculator in the bottom-left corner of the game window (IMGUI, at most half the screen height), with every button in its real grid cell showing its solver symbol (`÷`, `X4`, `flip`, …) or `(<name>)` for ids the solver doesn't map. Also write each button's `col` and `row` to `puzzle.json` and `state.json`. The solver app doesn't change.

## Read first

1. `CLAUDE.md`: project rules. The mod must stay read-only (Steam achievements): only read fields, no presses, no charge changes, no game or save writes, no new patches, no game objects.
2. `spec/redacted-overlay/requirements.md` and `design.md`.
3. `GameHook.MelonLoader/CalcHookCore.cs` (`ButtonInfo`, `ReadButtons`, `Tick`, `CheckPuzzle`, `WritePuzzle`, `WriteJson`, `Safe` / `Report`), `GameHook.MelonLoader/Mod.cs`, `GameHook.MelonLoader/GameRefs.props`.
4. `CalculatorPuzzle/GameLink.cs` (`MapButton`): the mapping `ButtonSymbols` must mirror.
5. `docs/button-id.md`: the id list for the test.
6. `user-verification/` ("Test E") for the manual test format.
7. Optional, to check a game detail: `ilspycmd -t AlchemyChallenge "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"` (also `StartLvl` for `MiniCreate` / the buy code, `CreateCalc`, `Place`).

## Steps

1. Run `dotnet build`, `dotnet test` and `dotnet build GameHook.MelonLoader -c Release` first and note that everything passes.
2. `GameHook.MelonLoader/ButtonSymbols.cs` (`Of`, `Token`, `Bare`), linked into `CalculatorPuzzle.Tests`, with `ButtonSymbolsTests`.
3. `GameRefs.props`: add `UnityEngine.IMGUIModule` and `UnityEngine.InputLegacyModule`.
4. `CalcHookCore`: `Col` / `Row` in `ButtonInfo` from `anchoredPosition`; `col` / `row` in both JSON files.
5. `CalcHookCore`: overlay state, `UpdateOverlay` in `Tick` (with `CalcWidth`), `OnGUI` drawing (fixed `=` at column 4, row 3); `Mod.OnGUI`.
6. Add "Test F: Redacted overlay" as `user-verification/test-x.md` from the design. Then stop and ask the user to run it. You can't run the game. The user will check open question 2 (cells match the screen) with you.
7. After the user reports back, record the answer to open question 2, update the Status lines in all three spec files, add an "As built" section to the design, and update `CLAUDE.md` (the mod bullet and a `redacted-overlay/` entry under specs).

## Check the work

- `dotnet build GameHook.MelonLoader -c Release` and `dotnet build`, both with no warnings.
- `dotnet test`: all tests pass, old and new.
- Read the diff once more for anything that writes to the game: no `SetActive`, no `AddComponent`, no `GUI.Button`, no assignment to a game field.
