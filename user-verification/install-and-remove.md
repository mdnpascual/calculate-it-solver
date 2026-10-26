# CalcHook: install and remove (MelonLoader)

The read-only CalcHook mod for Calculate It. We use MelonLoader only; BepInEx was dropped without being tested.

- Game folder: `D:\SteamLibrary\steamapps\common\Calculate It` (called `<game>` below)
- Game: Unity 2022.3.32f1, Mono runtime, 64-bit `CalculateIt.exe`, game version "0.1 rel"
- Mod build: `dotnet build GameHook.MelonLoader -c Release` → `GameHook.MelonLoader\bin\Release\net472\CalcHook.MelonLoader.dll`

The mod only reads fields. It doesn't press buttons, change charges or touch scores. The game unlocks Steam achievements (it has no leaderboards), so Steam **Offline mode** while testing is the safe choice.

## Install (MelonLoader 0.7.3)

1. Before the first install, the top level of `<game>` holds exactly:
   `CalculateIt_Data\`, `MonoBleedingEdge\`, `CalculateIt.exe`, `steam_appid.txt`, `UnityCrashHandler64.exe`, `UnityPlayer.dll`.
2. Download **MelonLoader.x64.zip** from https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3
   (SHA-256 of the copy I checked: `5b2b2f3d1cd42b59ec886c5bdc2663edae87a0097a4f4a8f58c0965a99dda416`).
   Extract it into `<game>` so `version.dll` sits next to `CalculateIt.exe`. Or use `MelonLoader.Installer.exe` with version 0.7.3.
3. Start the game once, then quit. This creates `Mods\`, `Plugins\`, `UserData\`, `UserLibs\` and `MelonLoader\Latest.log`.
4. Copy `CalcHook.MelonLoader.dll` directly into `<game>\Mods\`, **not into a subfolder**. MelonLoader doesn't search subfolders of `Mods\`, so `Mods\CalcHook\CalcHook.dll` gives "0 Mods loaded" with no error. You can rename the file.
5. To update the mod, close the game and overwrite the DLL.

Log: `<game>\MelonLoader\Latest.log` (also shown in the MelonLoader console). State files: `%LOCALAPPDATA%\CalcHook\`.

## Remove MelonLoader completely

Close the game, then delete these from `<game>`:

- `version.dll`
- `MelonLoader\` (copy `Latest.log` out first if you need it)
- `Mods\`
- `Plugins\`
- `UserLibs\`
- `UserData\`
- `dobby.dll` and `NOTICE.txt`, if present

Check the top level against install step 1, then run Steam's **Verify integrity of game files**. Verify restores game files but doesn't delete extra ones like `version.dll`, so delete the list above first.
