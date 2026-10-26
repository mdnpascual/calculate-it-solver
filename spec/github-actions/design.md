# Design: GitHub Actions

**Status:** Not started (spec written 2026-10-26).

## Files

```
.github/workflows/solver.yml
.github/workflows/mod.yml
.github/workflows/release.yml
GameStubs/GameStubs.csproj          net472, AssemblyName Assembly-CSharp
GameStubs/Game.cs                   StartLvl, MainCalc, CountUses, Shop, ... (names only)
GameStubs/UI/GameStubs.UI.csproj    only if needed (requirement 5): AssemblyName UnityEngine.UI
GameStubs/UI/Text.cs
GameStubs/README.md                 what it is, how to add a member, the drift check
GameHook.MelonLoader/GameRefs.props two modes (below)
tools/check-stubs.ps1               drift guard (requirement 10)
```

`GameStubs/` stays out of `CalculatorPuzzle.sln`, like the mod.

## GameRefs.props

```xml
<Project>
  <PropertyGroup>
    <GameRefs Condition="'$(GameRefs)' == ''">Game</GameRefs>
    <GameDir Condition="'$(GameDir)' == ''">D:\SteamLibrary\steamapps\common\Calculate It</GameDir>
    <GameManaged>$(GameDir)\CalculateIt_Data\Managed</GameManaged>
  </PropertyGroup>

  <!-- Local: the installed game's DLLs, in place (unchanged). -->
  <ItemGroup Condition="'$(GameRefs)' == 'Game'"> ...current Reference items... </ItemGroup>

  <!-- CI: stub Assembly-CSharp + Unity reference assemblies. No game file. -->
  <ItemGroup Condition="'$(GameRefs)' == 'Stubs'">
    <ProjectReference Include="..\GameStubs\GameStubs.csproj" Private="false" />
    <PackageReference Include="UnityEngine.Modules" Version="2022.3.32" PrivateAssets="all" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

The `UnityEngine.Modules` package lives on `https://nuget.bepinex.dev/v3/index.json`. Add a `nuget.config` at the repo root with that source plus nuget.org, and package source mapping so only `UnityEngine.Modules` comes from the BepInEx feed. Check that the package's `lib` folder carries a reference for `net472` (or `netstandard2.0`); if it only ships `ref`/`lib` under another TFM, reference the DLLs by `HintPath` from the package folder instead.

`Private="false"` on the stub reference keeps the stub DLL out of the mod's `bin`, so nobody copies a fake `Assembly-CSharp.dll` into the game.

## The stubs

Build the member list from the mod source, not from the game: every `StartLvl.x`, `MainCalc.x`, ... in `CalcHookCore.cs` and `Mod.cs`. For each, look up the real declaration once (`ilspycmd -t StartLvl "<game>\...\Assembly-CSharp.dll"`) and copy only the signature: kind, type, static or not, declaring type, base class (`MonoBehaviour` for components, so `GetComponent<T>` / `FindObjectOfType<T>` compile). Bodies: `throw null;` for methods and properties, no initializers for fields. Private fields read by reflection (`challengeButtonName`, ...) don't need a stub; they're looked up by string.

Things that change the IL and must match the game, or the DLL breaks at run time:

- Field vs property (`ldfld` vs `call get_x`).
- The declaring type of an inherited member.
- Parameter and return types of called methods (`NextLvl`, `D20` for `nameof` and Harmony only need the name, but if the mod calls them, the signature too).
- Arrays vs lists (`StartLvl.items`, `shops`).
- `static`.

Game types referenced as field types (for example the type of `StartLvl.items[0]`) need their own stubs with the members the mod touches.

## Workflows

`solver.yml`:

```yaml
name: Solver
on:
  push: { branches: [main] }
  pull_request: { branches: [main] }
permissions: { contents: read }
jobs:
  build-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: 9.0.x }
      - run: dotnet restore CalculatorPuzzle.sln
      - run: dotnet build CalculatorPuzzle.sln -c Release --no-restore
      - run: dotnet test CalculatorPuzzle.sln -c Release --no-build --logger trx --results-directory TestResults
      - uses: actions/upload-artifact@v4
        if: always()
        with: { name: test-results, path: TestResults }
```

Add a `paths-ignore` for `**/*.md`, `user-verification/**`, `docs/**`, `spec/**` so doc-only commits don't run it.

`mod.yml`: same trigger with `paths: [GameHook.MelonLoader/**, GameStubs/**, nuget.config, .github/workflows/mod.yml]`; steps: checkout, setup-dotnet (the SDK builds `net472` through `Microsoft.NETFramework.ReferenceAssemblies`, already referenced), `dotnet build GameHook.MelonLoader -c Release -p:GameRefs=Stubs`, upload `GameHook.MelonLoader/bin/Release/net472/CalcHook.MelonLoader.dll` as artifact `CalcHook-mod`. `ubuntu-latest` would work for the mod alone; use `windows-latest` anyway so the build matches the player's.

`release.yml`: on `push: tags: ['v*']`. Jobs `solver` and `mod` (same steps, reusable through `workflow_call` from the two files above), then `release` (`needs` both, `permissions: contents: write`): `dotnet publish CalculatorPuzzle -c Release -r win-x64 --self-contained false -o publish`, `Compress-Archive publish CalculatorPuzzle-$tag-win-x64.zip`, download the mod artifact, `gh release create $tag --draft --generate-notes <zip> <dll>`. The notes include the line from requirement 7 until Test Y passes; then remove it.

Pin actions by major version (`@v4`); Dependabot for `github-actions` is optional.

## Drift guard

`tools/check-stubs.ps1 [-GameDir <path>]`: loads both assemblies with `System.Reflection.MetadataLoadContext` (or `Mono.Cecil` through a tiny console project under `tools/`), and for every public type and member in the stub checks the real one exists with the same kind, signature, `static` and declaring type. Prints missing or different members; exit code 1 if any. Run after a game update, and once when the stubs are first written.

## Tests

- No new xUnit tests (the existing ones run in CI).
- Verify on GitHub: a run of each workflow passes; a deliberate stub omission makes `mod.yml` fail; a doc-only commit runs neither.
- Local: `dotnet build GameHook.MelonLoader -c Release` still uses the game (unchanged output); `-p:GameRefs=Stubs` builds without the game (rename the game folder or pass a bad `GameDir` to prove it).
- `tools/check-stubs.ps1` passes against the installed game.
- In-game: Test Y (requirement 9).
