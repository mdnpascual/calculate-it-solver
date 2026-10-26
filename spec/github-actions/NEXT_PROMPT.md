# Next prompt: GitHub Actions for the solver and the mod

**Status:** Implemented locally 2026-10-26: stubs, stub mode, drift check and workflows written; both mod builds pass, 790 tests pass, `tools/check-stubs.ps1` reports no differences. Open: first push (workflows green), Test Y in the game.

Implement `spec/github-actions/`: CI for the solver (build + tests) and for the mod (built against a hand-written stub `Assembly-CSharp` and the `UnityEngine.Modules` NuGet package, because the game's DLLs can't be on GitHub), plus a draft release on `v*` tags.

## Read first

1. `spec/github-actions/requirements.md` and `design.md`.
2. `GameHook.MelonLoader/GameHook.MelonLoader.csproj`, `GameRefs.props`, `README.md` ("Build and install").
3. `GameHook.MelonLoader/Mod.cs` and `CalcHookCore.cs`: list every game type and member used (the stub's content).
4. `CalculatorPuzzle.sln`, `CalculatorPuzzle.Tests/CalculatorPuzzle.Tests.csproj` (links `ButtonSymbols.cs` from the mod; no game reference).
5. `.gitignore` (game DLLs are ignored by name; the stub builds into `bin/`, also ignored).

## Steps

1. Check the Unity references: restore `UnityEngine.Modules` 2022.3.32 from `https://nuget.bepinex.dev/v3/index.json` into a scratch folder; list which DLLs it has and for which TFM. Confirm `UnityEngine.UI` is or isn't there (decides requirement 5).
2. Add `nuget.config` (nuget.org + BepInEx feed, source mapping) and the `Stubs` mode in `GameRefs.props` (design "GameRefs.props").
3. Write `GameStubs/` from the member list in step 3 of "Read first", checking each signature against the real DLL with `ilspycmd -t <Type> "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"`. Copy signatures only, never bodies.
4. Build both ways: `dotnet build GameHook.MelonLoader -c Release` (game) and `dotnet build GameHook.MelonLoader -c Release -p:GameRefs=Stubs -p:GameDir=C:\nope` (stubs, proves no game file is used). Fix stubs until it compiles with no new warnings.
5. Write `tools/check-stubs.ps1` and run it against the game: no differences.
6. Write `.github/workflows/solver.yml`, `mod.yml`, `release.yml` (design "Workflows"). Lint with `actionlint` if installed.
7. Add Test Y as `user-verification/test-y.md` (requirement 9) and a row in `user-verification/README.md` (use the next free letter if Y is taken).
8. Docs: badges in `README.md`, a "CI build" section in `GameHook.MelonLoader/README.md`, `GameStubs/README.md`, `CLAUDE.md` "Status" lines for `GameStubs/`, `tools/` and `.github/`, the line in `spec/README.md`, and the status lines of the three spec files.

Don't run `git init`, commit, push or create the GitHub repo unless the user asks; the workflows are checked once the user pushes.

## Check

- `dotnet build` and `dotnet test` (solution): all pass, same count as before.
- The mod builds in both modes (step 4); the stub `Assembly-CSharp.dll` is not in `GameHook.MelonLoader/bin`.
- `tools/check-stubs.ps1` exits 0.
- After the user pushes: both workflows green; the mod artifact passes Test Y in the game.
