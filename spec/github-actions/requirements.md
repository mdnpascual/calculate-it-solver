# GitHub Actions: build the solver and the mod

**Status:** Implemented locally 2026-10-26: stubs, stub mode, drift check and workflows written; both mod builds pass, 790 tests pass, `tools/check-stubs.ps1` reports no differences. Open: first push (workflows green), Test Y in the game.

## Why

The repo is going on GitHub. Every push and pull request should show whether the solver builds and its tests pass, and whether the mod still compiles. Tagged versions should give downloadable builds of the app and the mod, so a player doesn't need the .NET SDK.

## The problem with the mod

The solver (`CalculatorPuzzle/`, `CalculatorPuzzle.Tests/`) uses only the .NET SDK and NuGet packages, so a GitHub runner can build and test it as-is.

The mod (`GameHook.MelonLoader/`) can't be built as-is. `GameRefs.props` points at the game's DLLs in `D:\SteamLibrary\...\CalculateIt_Data\Managed`. A runner has no game, and the DLLs can't go in the repo:

- `Assembly-CSharp.dll` is the game's own code (copyrighted). The mod uses its types directly: `StartLvl` (about 28 uses, plus `[HarmonyPatch]` on `NextLvl` and `D20`), `MainCalc`, `CountUses`, `Shop`, `CalcSize`, `GamePrefs`, `Randomisator`, `Blockirator`. Private fields are already read by reflection.
- The `UnityEngine*.dll` files are Unity's. The mod uses `GUI`, `GUIStyle`, `Rect`, `Color`, `GameObject`, `RectTransform`, `Screen`, `Time`, `Input`, `Texture2D`, `Application`, `Object`, and `UnityEngine.UI.Text`.
- MelonLoader and Harmony come from NuGet (`LavaGang.MelonLoader` 0.7.3), so they're fine.

So yes, the mod can be built on GitHub, but only against stand-in references. Options considered:

| Option | Verdict |
|--------|---------|
| A. Hand-written stub `Assembly-CSharp` (only the types and members the mod uses, empty bodies) + Unity reference assemblies from NuGet (`UnityEngine.Modules` 2022.3.32 on `nuget.bepinex.dev`, checked: the version exists) | **Chosen.** No game file is copied; the stubs are our own code. The DLL built this way should load in the real game, because a reference is resolved by assembly name, type name and member signature (Assembly-CSharp has no strong name). |
| B. Self-hosted runner on the player's PC, which has the game | Rejected for CI. GitHub advises against self-hosted runners on public repos: a fork's pull request could run code on that PC. |
| C. Encrypted game DLLs in the repo or in secrets | Rejected. Still redistributes game files; secrets are capped at 48 KB. |
| D. Rewrite the mod to use only reflection, with no compile-time game reference | Rejected. Large change; loses the compile-time checks that catch game updates. |
| E. Commit a stripped reference assembly made from the real `Assembly-CSharp.dll` (Refasmer / NStrip) | Rejected. Less work than A, but it is still a file derived from the game, with every public name. |

## Requirements

1. **Solver workflow** (`.github/workflows/solver.yml`): on push and pull request to `main`, on `windows-latest` (the app and tests target `net9.0-windows`): restore, `dotnet build CalculatorPuzzle.sln -c Release`, `dotnet test` (no rebuild). A failing test fails the run. Test results are uploaded as an artifact.
2. **Mod workflow** (`.github/workflows/mod.yml`): on push and pull request to `main` when `GameHook.MelonLoader/**`, the stub project or the workflow changes: build the mod in stub mode (requirement 4) and upload `CalcHook.MelonLoader.dll` as an artifact.
3. **Stub project** `GameStubs/` (new, not in the solution): a `net472` class library with assembly name `Assembly-CSharp` that declares, with empty or `default` bodies, exactly the game types and public members the mod uses: same namespace (the global one), same names, same kind (field vs property vs method), same signatures, same declaring type (a member the real game declares on a base class must be declared on that base class in the stub, or the mod's reference points at the wrong type and fails at run time). Header comment: this is not game code; it lists names only so the mod compiles on CI.
4. **Stub mode** in the mod build: a property (`-p:GameRefs=Stubs`) switches `GameRefs.props` from the game's `Managed` folder to the stub project's output plus the `UnityEngine.Modules` package. The local default stays the real game folder, so `dotnet build GameHook.MelonLoader -c Release` on the player's PC is unchanged.
5. **UI types:** `UnityEngine.UI` is a Unity package (uGUI), not a module, so it's probably not in `UnityEngine.Modules`. If no matching NuGet reference exists, the stub project gets a second stub assembly `UnityEngine.UI` with `Text` (and whatever else the mod uses from that namespace), following rule 3.
6. **Release workflow** (`.github/workflows/release.yml`): on a pushed tag `v*`: run requirements 1 and 2, then `dotnet publish CalculatorPuzzle -c Release -r win-x64 --self-contained false` (framework-dependent; players install the .NET 9 Desktop Runtime) zipped as `CalculatorPuzzle-<tag>-win-x64.zip`, plus `CalcHook.MelonLoader.dll`, attached to a GitHub Release (draft, so the player checks it before publishing).
7. **Mod release check:** a mod DLL built against stubs is attached to a release only after it has been loaded in the game once (Test Y, requirement 9). Until then, the release notes say the mod DLL is a CI build and point to building it locally.
8. **No secrets and no game files** in any workflow, artifact or log. Workflows use `permissions: contents: read`, except the release job (`contents: write`).
9. **Manual test** (Test Y, `user-verification/test-y.md`): download the CI mod artifact, put it in `<game>\Mods\` in place of the local build, start a run: the MelonLoader log shows CalcHook loaded with no `MissingMethodException` / `MissingFieldException` / `TypeLoadException`; `puzzle.json` is written; F9 overlay draws; a `warp` (d20) press and a new round still trigger (the two Harmony patches).
10. **Drift guard (local):** a check the player can run after a game update that compares the stub against the real `Assembly-CSharp.dll` (every stub type and member exists in the real one with the same signature and declaring type). Runs only where the game is installed, so not in CI.
11. **Docs:** `README.md` gets build badges; `GameHook.MelonLoader/README.md` gets a "CI build" section (stub mode, how to update the stubs when the mod uses a new game member); `CLAUDE.md` "Status" names `GameStubs/` and the workflows.

## Examples

- Push that edits `CalculatorPuzzle/LeftSearch.cs`: solver workflow runs (build + 790-ish tests); mod workflow doesn't.
- Push that adds `StartLvl.someNewField` to the mod without updating the stub: mod workflow fails with `CS1061`, telling the author to add the member to `GameStubs/`.
- Tag `v0.1.0`: draft release with the app zip and the mod DLL.

## Open questions

- Does `UnityEngine.Modules` 2022.3.32 contain everything the mod references (`UnityEngine.dll`, `CoreModule`, `IMGUIModule`, `InputLegacyModule`, `TextRenderingModule`, `UIModule`)? Check in step 1 of the next prompt.
- Is `windows-latest` fast enough for the full test run? The parallel-search tests pin threads to the last N processors; a 4-core runner should be fine, but timing-sensitive tests may need a longer timeout.
- Some specs and `docs/` quote short pieces of decompiled game code. Not a CI question, but worth a look before the repo goes public.

## Out of scope

- Self-contained or single-file app builds, installers, code signing.
- Publishing to Thunderstore or Nexus.
- Building or testing the mod against the real game on CI.

## Findings (2026-10-26)

- `UnityEngine.Modules` 2022.3.32 on the BepInEx feed has `lib/net35`, `net45` and `netstandard2.0`; the `net45` set works for `net472`. It has all the modules the mod uses (`CoreModule`, `IMGUIModule`, `InputLegacyModule`, `TextRenderingModule`, `UIModule`, `UnityEngine.dll`) but **not `UnityEngine.UI`**, so requirement 5 applies: `GameStubs/UI/` stubs `Text`.
- The mod also uses `RunSave.Restoring` (found by compiling against the stubs), which this spec's list didn't name.
- Not checked until the first push: `windows-latest` timing of the full test run, and the workflows themselves (`actionlint` isn't installed here; the YAML parses).
