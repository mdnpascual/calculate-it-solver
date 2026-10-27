# Next prompt: Passive picker overlay (challenge 9)

**Status:** Done (2026-10-27). Nothing left to implement; kept for history.

Extend the read-only MelonLoader mod for Calculate It. In challenge 9 (the game's *Alchemy*, "redacted" in this repo) the passive picker (before round 1 and after the round 5, 10, 15, … bosses) shows Latin **button** texts instead of the passives' own names and descriptions: the game's `TextsLoader` loads the Latin button file into the passive arrays. Draw a top-center IMGUI panel (75% of the screen width, at most 25% of its height; constants for playtesting) with 3 boxes, one per offered passive, in the game's order: the real title, then the real description, read from the game's own `textAsset_eng_p` / `textAsset_p`. Only in challenge 9; never in other challenges, even with F9 on. In challenge 9, F9 hides it with the other panels. The solver app doesn't change.

## Read first

1. `CLAUDE.md`: project rules. The mod must stay read-only (Steam achievements): only read fields, no presses, no `SetActive`, no new patches, no game objects, no writes to `Infos` / `TextsLoader`.
2. `spec/passive-picker-overlay/requirements.md` and `design.md` (open questions 1 and 3 are answered; 2 is checked by Test Z1).
3. `GameHook.MelonLoader/README.md` and the overlay section of `GameHook.MelonLoader/CalcHookCore.cs`: `UpdateOverlay`, `BuildStyles`, `DrawOverlay`, `DrawCell`, `DrawShop`, `Poll`, `WriteJson`, `Safe` / `Report`, `Challenge()`.
4. `GameStubs/README.md` (new game members need stubs).
5. Optional, to check a game detail: `ilspycmd -t PassivePool "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"` (also `RandomisatorPassives`, `TextsLoader`, `Infos`, `StartLvl`).

## Steps

1. Run `dotnet build`, `dotnet test` and `dotnet build GameHook.MelonLoader -c Release` and note that everything passes.
2. `GameHook.MelonLoader/PassiveTexts.cs` (plain C#), linked into `CalculatorPuzzle.Tests` like `ButtonSymbols.cs`; `PassiveTextsTests.cs` from the design's test plan.
3. Stubs in `GameStubs/Game.cs`; build with `-p:GameRefs=Stubs`.
4. `CalcHookCore`: `PassiveOffer`, `ReadPassivePicker`, text cache, refresh in `UpdateOverlay`, `DrawPassives` (with its own challenge 9 check and the 25% height cap: shrink fonts, then clip) and its styles, the `DrawOverlay` early-return change.
5. `Poll` / `WriteJson`: `passives=[...]` in the change line, `passivePicker` in `state.json`.
6. Add Test Z as `user-verification/test-z.md` from the design's test plan, and its row in `user-verification/README.md`. Then stop and ask the user to run it and playtest the size. You can't run the game.
7. After the report: apply the size changes, record the answers, update the Status lines in all three spec files, add an "As built" section to the design, and update `spec/README.md`, `GameHook.MelonLoader/README.md` (Overlays) and the `CLAUDE.md` mod bullet if needed.

## Check the work

- `dotnet build GameHook.MelonLoader -c Release`, the same with `-p:GameRefs=Stubs`, and `dotnet build`, all with no warnings.
- `dotnet test`: all tests pass.
- Read the diff once more: no `SetActive`, `AddComponent`, `GUI.Button`, no call to `PassivePool.CreatePassive` / `OnEnable`, `StartLvl.PassiveChoosed`, no assignment to a game field. The panel's draw path checks challenge 9 itself, so F9 in another challenge can't show it.
