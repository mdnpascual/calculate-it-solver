# Next prompt: Game state scan

**Status:** Done. Kept for reference; nothing left to implement.

Make the solver app follow Calculate It. A read-only MelonLoader mod publishes each new puzzle (after the game settles) to a JSON file. The WinForms app picks it up, fills in S, G and the buttons with their charges, runs the Division Shortcut search, and hides lines the charges can't pay for. The repo is C#: .NET 9 WinForms + xUnit for the app, net472 for the mod.

## Read first

1. `CLAUDE.md`: project rules (the game must never be changed: read-only, Steam leaderboards).
2. `spec/game-state-scan/requirements.md` and `design.md`.
3. `GameHook.MelonLoader/CalcHookCore.cs`, `Mod.cs`, `GameRefs.props`: the working mod (logs state once a second, writes `state.json`).
4. `CalculatorPuzzle/EngineeringCalculator.cs` (`Buttons`, `Buttons.Parse`, `Press`), `DivisionShortcut.cs` (`Search`), `MainForm.cs` (`StartDivisionSearch`, timer pattern).
5. `CalculatorPuzzle.Tests/` for the test style.
6. `user-verification/`: how the mod is installed and tested in the game.

## Steps

1. Run `dotnet build` and `dotnet test` first and note that everything passes.
2. App, charges: extend `Buttons` with `Charges`, the `:n` syntax in `Parse`, and `Fits`. Add `ButtonsParseTests` and `FitsTests`. Make sure every existing test still passes unchanged.
3. App, search: add `onRejected` to `DivisionShortcut.Search` and the `Fits` check. Add the `DivisionShortcutTests` cases from the design.
4. App, feed: add `GameLink.cs` (`MapButton`, `Parse`, `DefaultPath`) and `GameLinkTests`.
5. App, UI: add the `Follow game` checkbox, the watcher with its 100 ms timer, and the status text and rejected count in `MainForm`.
6. Mod: add the per-frame settle state machine and `puzzle.json` publishing to `CalcHookCore`, as in the design. Keep the mod read-only: no new patches, no calls that change game state. Build with `dotnet build GameHook.MelonLoader -c Release`.
7. Add a "Test C: game state scan" section to `user-verification/` covering the four manual checks from the design's test plan. Then stop and ask the user to run it. You can't run the game.
8. After the user reports back, update the Status lines in all three spec files, add an "As built" section to the design, and update `CLAUDE.md`.

## Check the work

- `dotnet build` (solution) and `dotnet build GameHook.MelonLoader -c Release`, both with no warnings.
- `dotnet test`: all tests pass, old and new.
- `dotnet run --project CalculatorPuzzle`, with no game running: write a sample `puzzle.json` by hand into `%LOCALAPPDATA%\CalcHook\`.
  - The boxes fill in, A goes back to 1 (raise it first to check), and a search starts.
  - Saving the same file again with the same `seq` does nothing.
  - Bumping `seq` restarts the search.
- In-game checks come from the user (step 7).
