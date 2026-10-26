# Next prompt: implement the warp re-scan

**Status:** Complete (Test S done 2026-10-25).

Implement the spec in `spec/warp-rescan/`: after the `operator_warp` button re-rolls every button (`StartLvl.D20`), the mod publishes `puzzle.json` again and the app solves the new button set.

## Read first

1. `spec/warp-rescan/requirements.md` and `design.md`.
2. Mod: `GameHook.MelonLoader/Mod.cs` (`NextLvlPatch`), `GameHook.MelonLoader/CalcHookCore.cs` (`OnRoundLoaded`, `CheckPuzzle`, `WritePuzzle`, the `state.json` writer), `GameHook.MelonLoader/README.md`.
3. App: `CalculatorPuzzle/GameLink.cs` (`GamePuzzle`, `Parse`), `CalculatorPuzzle/MainForm.cs` (`OnGameFileChanged`, `appliedStepRound`, the `Game: round` status prefix).
4. `CalculatorPuzzle.Tests/GameLinkTests.cs` for the test style.

## Steps

1. Mod: `WarpPatch` postfix on `StartLvl.D20`, `CalcHookCore.OnWarp`, `_warps` (reset in `OnRoundLoaded`), `"warp"` in both JSON files, `warp N` in the log line. Build: `dotnet build GameHook.MelonLoader -c Release`. The mod must stay read-only: the postfix only sets mod fields.
2. `GameLink`: `GamePuzzle.Warp`, parsed from `warp` (missing → 0). Tests 1–3 from the design.
3. `MainForm`: `appliedStepWarp` in the rule-14 guard, `warp n` in the status prefix.
4. Add Test S as `user-verification/test-s.md` (steps S1–S6 from the design) and a row in `user-verification/README.md`. Q and R are reserved by `boss-double-charge/` and `boss-one-send/`; if `test-s.md` exists, use the next free letter and say so.
5. Docs: `GameHook.MelonLoader/README.md` ("What it writes": `warp`; "Read-only": the second postfix), the `GameLink.cs` and `MainForm.cs` notes in `CalculatorPuzzle/README.md`, the `operator_warp` row in `docs/button-id.md`, the rule 2 row in `docs/boss-rules.md` (the d20 re-roll is now followed), the out-of-scope line in `spec/boss-blocked-buttons/requirements.md`, and the `warp-rescan/` line in `spec/README.md`.

## Check

- `dotnet build` and `dotnet test` (all existing tests must still pass).
- By hand, no game: the rule-14 fake-file check in the design.
- In game: Test S, done by the user.
