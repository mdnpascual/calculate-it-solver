# Test Y: CI-built mod (stub build)

Checks that a `CalcHook.MelonLoader.dll` built on GitHub against name-only stubs (`spec/github-actions/`) loads and works in the real game. The release notes say the mod DLL is untested until this passes.

1. Download the `CalcHook-mod` artifact from a green **Mod** workflow run (Actions tab) and unzip it. Remove the locally built `CalcHook.MelonLoader.dll` from `<game>\Mods\` and put the downloaded one there.
2. **Y1.** Start the game and open `MelonLoader\Latest.log`.
   - Expected: CalcHook loads (its loaded line appears). No `MissingMethodException`, `MissingFieldException`, `TypeLoadException` or `error in` lines mentioning CalcHook.
3. **Y2.** Start a run and reach a round.
   - Expected: `%LOCALAPPDATA%\CalcHook\puzzle.json` is written with the right start, goal and buttons.
4. **Y3.** Press F9.
   - Expected: the overlay calculator draws with the buttons and charges; open the shop, its panel draws too.
5. **Y4.** Press the `warp` (d20) button.
   - Expected: the overlay re-scans the new buttons (the `StartLvl.D20` patch fires).
6. **Y5.** Finish the round.
   - Expected: the next round's puzzle is published (the `StartLvl.NextLvl` patch fires).
7. Copy any `error in` lines from `MelonLoader\Latest.log`. When Y1 to Y5 pass, remove the "not loaded in the game yet" line from the release notes in `.github/workflows/release.yml`.
