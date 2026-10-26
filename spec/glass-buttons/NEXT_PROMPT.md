# Next prompt: Glass buttons

**Status:** Complete (2026-10-23). Tested in-game; legendary not confirmed (the solver doesn't use it).

Make the solver aware of glass buttons in Calculate It. A glass button breaks after one press whatever its charge count says. The read-only MelonLoader mod must report each button's glass state (`CountUses.glassed`) and a diagnostic `legendary` flag in `puzzle.json` and `state.json`. The WinForms app clamps a glass button's charge to 1 when it reads `puzzle.json`. The repo is C#: .NET 9 WinForms + xUnit for the app, net472 for the mod.

## Read first

1. `CLAUDE.md`: project rules. The mod must stay read-only (Steam achievements): no presses, no charge changes, no game or save writes, no new patches.
2. `spec/glass-buttons/requirements.md` and `design.md`.
3. `GameHook.MelonLoader/CalcHookCore.cs` (`ButtonInfo`, `ReadButtons`, `CheckPuzzle`, `WritePuzzle`, `WriteJson`, `Poll`).
4. `CalculatorPuzzle/GameLink.cs` (`GamePuzzle`, `Parse`) and where `MainForm.cs` builds the `Game: round …` status.
5. `CalculatorPuzzle.Tests/GameLinkTests.cs` for the test style.
6. `user-verification/` ("Test C" shows the format for a manual test section).

## Steps

1. Run `dotnet build` and `dotnet test` first and note that everything passes.
2. App: add `Glass` to `GamePuzzle`, the clamp and the missing-field default in `GameLink.Parse`. Add the `GameLinkTests` cases from the design, including the `Fits` end-to-end check.
3. App: add `Glass: …` to the game status in `MainForm`.
4. Mod: add `Glass` / `Legendary` to `ButtonInfo`, read them in `ReadButtons`, add the glass flag to the fingerprint, write both fields in `puzzle.json` and `state.json`, and mark them in the log line. Build with `dotnet build GameHook.MelonLoader -c Release`.
5. Add "Test D: glass buttons" as `user-verification/test-x.md` from the design's manual test plan. Then stop and ask the user to run it. You can't run the game.
6. After the user reports back, update the Status lines in all three spec files, add an "As built" section to the design, and update `CLAUDE.md` (the mod bullet and a `glass-buttons/` entry under specs).

## Check the work

- `dotnet build` (solution) and `dotnet build GameHook.MelonLoader -c Release`, both with no warnings.
- `dotnet test`: all tests pass, old and new.
- `dotnet run --project CalculatorPuzzle` with no game running: write a `puzzle.json` by hand into `%LOCALAPPDATA%\CalcHook\` with one glass button at `uses` 4 and bump `seq`. The Buttons box shows it at `:1` and the status lists it under `Glass:`. A file without `glass` fields still loads as before.
