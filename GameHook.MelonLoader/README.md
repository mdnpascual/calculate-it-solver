# GameHook.MelonLoader (CalcHook mod)

A read-only MelonLoader 0.7.3 mod for Calculate It (Unity 2022.3.32f1, Mono), net472, not in the solution. It reads the game state and writes the current puzzle to a JSON file that the solver in [`../CalculatorPuzzle`](../CalculatorPuzzle/README.md) follows. It references the game's DLLs by path through `GameRefs.props` (never copied). BepInEx was dropped.

## What it writes

- `%LOCALAPPDATA%\CalcHook\puzzle.json` (version 1, with `seq`): the puzzle, written once per new puzzle (round and goal) after the game has settled. Checked every frame; written once `RunSave.Restoring` and `StartLvl.IsTransitioning` are false, left ≠ goal, and the round, goal, left side, and button ids and charges are unchanged for 50 ms. It holds the round, start, goal and every active button with its game id and charges. The solver reads this file.
- `%LOCALAPPDATA%\CalcHook\state.json`: a snapshot of the live state, refreshed about once a second, for debugging.

Extra fields:

- `challenge`, `required` (the required button's id: Market Demand from private `StartLvl.challengeButtonName`, or boss rule 11 from the public `must_butt`) and `requiredKind` (`market` / `boss`), `bossRule` (0 outside boss rounds). In a rule-14 round the mod publishes once per round, not once per goal. `state.json` also has `requiredLabel`, `requiredUses`, `mustUsed`. Private `StartLvl` fields are read through reflection.
- Per button: raw `uses`, `glass` (`CountUses.glassed`), `blocked` (`Blockirator.inf_blocked`: boss rules 2 and 9, difficulty 8 or 0 charges; the solver writes such a button `:0`), a diagnostic `legendary` flag (`Infos.id` 10, 11, 28, 46, 47; untested in-game, unused by the solver), and `col` / `row` (grid cell) in both files.
- `last` (`StartLvl.last`) and the quest key's `digit` in both files; `shop` and `passivePicker` (`null`, or `open` plus `offers` with `id`, `name`, `description`, `x`) in `state.json`; the log line gets `passives=[...]` while the picker is open in challenge 9.
- `near1`: true when the player owns `passive_near1` (`GameObject.Find("passive_near1") != null`, the game's own win-check test; also in `state.json`). A change of it in the same round makes the puzzle pending again, so it is published once more (the perk picker can run before or after the publish); the log line says `near1`.
- `blind`: true when `GamePrefs` `blind` is 1, i.e. the passive_blind free press is still waiting (both files). The flag drops to 0 on the round's first non-glass press, so the mod republishes on owning the passive (`GameObject.Find("passive_blind") != null`, fingerprint `|blind:`), never on the flag; the log line says `blind`.
- `coins` (`StartLvl.coins`), `diffType` (`GamePrefs.GetInt("diff_type")`) and `coinPassives` (the names among `passive777`, `passive_lucky`, `passive_perfect`, `passive_odd` for which `GameObject.Find(name) != null`) at the top level of both files; the solver's money handling needs them. A change of `coins` alone doesn't republish (it changes with every press in some modes).
- `stored` on the `number_mw` button: `NumberReader.number` once `NumberReader.readed` is true (no field while the button is empty). An absorb changes the button set, so it republishes (fingerprint `|mw:<stored or empty>`); the log line says `coins=…`.
- `warp`: how many times the warp button (`operator_warp`, `StartLvl.D20`) re-rolled the buttons in this round (0 when none; reset at `NextLvl`). Each warp makes the puzzle pending again, so the new button set is published once it settles (also in a rule-14 round); the log line says `puzzle ready: …, warp n`.

It also logs start, goal, left side, operand and buttons with charges to `<game>\MelonLoader\Latest.log` when they change. Tested in-game: values match the screen.

## Overlays

- **Calculator overlay** (spec `redacted-overlay/`): in challenge 9 (*Alchemy*, labels hidden) an IMGUI calculator in the bottom-left corner shows each button's in-game label in its real grid cell (`ButtonSymbols.cs` mirrors `GameLink.MapButton` and `ButtonLabels.Label`). F9 toggles it; on by default in challenge 9. The charge, quest and mw keys show `volt<n>`, `unk<d>` and `mem<stored>` live (`mem` while empty); other buttons show their label (`$`, `reverse`, `X²`, `equalize`, `+-10`, …; see `spec/button-labels/`), `AX` and `swap` their tokens.
- **Passive panel** (spec `passive-picker-overlay/`): in challenge 9 the game shows Latin button texts in the passive picker (`TextsLoader` loads the button file into the passive arrays). While `StartLvl.passiveshop` is active, a top-center panel (75% of the screen width, at most 25% of its height) shows the 3 offered passives left to right with their real title and description, parsed (`PassiveTexts.cs`) from the game's own `TextsLoader.textAsset_eng_p`. Challenge 9 only (the draw path checks it); F9 hides it with the other panels. Russian (`textAsset_p`) is wired but untested and not supported.
- **Shop panel** (spec `shop-overlay/`): while the shop is open, a second panel right of the calculator shows the 7 offers with token, price, glass, legendary frame and the charge total after buying when owned.

## Based on REA research

The field names the mod reads come from research with the **REA** reverse-engineering skill (`reverse-engineer-anything`), which statically analysed the game's `Assembly-CSharp.dll`:

- goal `StartLvl.number`, round `StartLvl.round`
- left side `MainCalc.total` (display `MainCalc.output`), pending `MainCalc.operand` (999999999 = none), operator `MainCalc.currentoperand` (4 = ÷)
- buttons: active `CountUses` under `StartLvl.calc_transform`, charges in `CountUses.uses`, id from `buttonKey` or the object name (for example `operator_x1`, `number10`, `operator/`, `operator_reverse`, `operator_mod`, `operator_to0`)
- settle flags `RunSave.Restoring` (static) and `StartLvl.IsTransitioning`

The values were then checked in-game against the screen. See [`../docs/button-id.md`](../docs/button-id.md) for the button ids.

## Read-only

The mod never presses buttons, changes charges or writes game or save files. Its only Harmony patches are two postfixes that mark the puzzle as pending: on `StartLvl.NextLvl` (new round) and on `StartLvl.D20` (warp button; also counts the warp). Both only set mod fields.

The game unlocks Steam achievements (`Achiever` calls `SteamUserStats.SetAchievement`), so it stays read-only to avoid triggering them by accident. The game DLL contains Steamworks.NET's leaderboard types, but the game code never calls them: there are no leaderboards.

## Build and install

```
dotnet build GameHook.MelonLoader -c Release
```

Copy `bin\Release\net472\CalcHook.MelonLoader.dll` directly into `<game>\Mods\` (not a subfolder). See [`../user-verification/install-and-remove.md`](../user-verification/install-and-remove.md) for installing MelonLoader, testing and removal.

## CI build

GitHub can't have the game's DLLs, so `.github/workflows/mod.yml` builds the mod in stub mode:

```
dotnet build GameHook.MelonLoader -c Release -p:GameRefs=Stubs
```

`GameRefs.props` then references `GameStubs/` (name-only `Assembly-CSharp` and `UnityEngine.UI`) and the `UnityEngine.Modules` 2022.3.32 NuGet package (BepInEx feed, set in `nuget.config`) instead of the game's `Managed` folder. No game file is used, and the stub DLLs are not copied to `bin/`. The default (`GameRefs=Game`) is unchanged.

When the mod uses a new game member, the stub build fails with `CS1061` or `CS0103`. Add the member to `GameStubs/Game.cs` with the same kind, type, `static` and declaring type as the game, then run `tools/check-stubs.ps1` (needs the game). After a game update, run that script too. Details: [`../GameStubs/README.md`](../GameStubs/README.md).

A CI-built DLL is loaded in the game once with [Test Y](../user-verification/test-y.md) before a release trusts it.
