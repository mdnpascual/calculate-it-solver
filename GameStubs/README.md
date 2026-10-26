# GameStubs

Name-only stand-ins for the game's `Assembly-CSharp.dll` and `UnityEngine.UI.dll`, so the mod (`GameHook.MelonLoader/`) compiles on GitHub, where the game's DLLs can't be. **This is not game code**: it lists type and member names and signatures, with empty bodies (`throw null;`). Not in `CalculatorPuzzle.sln`.

| File | What |
|------|------|
| `GameStubs.csproj`, `Game.cs` | `net472`, assembly name `Assembly-CSharp`. `StartLvl`, `MainCalc`, `CountUses`, `Blockirator`, `QuestionNumber`, `NumberReader`, `Infos`, `Randomisator`, `BuyBox`, `CalcSize`, `GamePrefs`, `RunSave`: only the members the mod uses. |
| `UI/GameStubs.UI.csproj`, `UI/Text.cs` | `net472`, assembly name `UnityEngine.UI` (uGUI is a Unity package, not in `UnityEngine.Modules`). `Text.text`, with the real base chain. |

The mod's build picks these up with `-p:GameRefs=Stubs` (see `GameHook.MelonLoader/GameRefs.props`). The compiled mod refers to the game by assembly name, type name and member signature, so a DLL built against the stubs resolves against the real game at run time, if the stubs match it.

## What must match the game

Field vs property, the declaring type of an inherited member, parameter and return types, arrays vs lists, `static`, default parameter values (`GamePrefs.GetInt(string, int = 0)`), the base class (`MonoBehaviour` for components). Private fields the mod reads by reflection need no stub.

## Add a member

1. Look up the real declaration: `dotnet tools/StubCheck/bin/Release/net9.0/StubCheck.dll --game "<game>\CalculateIt_Data\Managed" --dump StartLvl` (build `tools/StubCheck` first), or `ilspycmd -t StartLvl <Assembly-CSharp.dll>`.
2. Copy the signature into `Game.cs`, never the body.
3. `dotnet build GameHook.MelonLoader -c Release -p:GameRefs=Stubs`, then `tools/check-stubs.ps1`.

## Drift check

`tools/check-stubs.ps1 [-GameDir <game folder>]` builds the stubs and `tools/StubCheck`, then checks that every public stub type and member exists in the installed game with the same kind, signature, `static`, base type and declaring type. Exit code 0 means no differences. It needs the game, so CI doesn't run it. Run it after a game update and after editing the stubs.
