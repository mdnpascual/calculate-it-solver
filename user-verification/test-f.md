# Test F: Redacted overlay (done, 2026-10-24)

The mod now draws a small calculator in the bottom-left corner (IMGUI, at most half the screen height) with each button's solver symbol in its real grid cell, and writes `col` and `row` for every button to `puzzle.json` and `state.json`. Challenge 9 (the game's *Alchemy*) hides every label behind a random symbol. Rebuild and reinstall the mod first (as in Test C: `dotnet build GameHook.MelonLoader -c Release`, copy the DLL to `<game>\Mods\`).

1. **F1.** Start a challenge 9 run.
   - Expected: the overlay appears bottom-left, 4 × 4, with `=` in the 4th row, 3rd column. Each cell's symbol matches the real button in the same position (press a digit and watch the display). Note any cell that doesn't match the screen (open question 2).
2. **F2.** Buy buttons until the calculator widens (to 6, then 8 columns).
   - Expected: the overlay widens by one column on each side the same way within 0.1 s, and `=` stays on the same key.
3. **F3.** Press a button.
   - Expected: its charge in the overlay drops. A glass button is blue; a 0-charge button greys out.
4. **F4.** Press F9.
   - Expected: it hides and shows the overlay. In a normal run (not challenge 9) it is hidden at start; F9 shows the same layout.
5. **F5.** Resize the window or change resolution.
   - Expected: the overlay stays in the bottom-left corner, keeps its proportions and is never taller than half the screen.
6. **F6.** Open `%LOCALAPPDATA%\CalcHook\puzzle.json` and `state.json`.
   - Expected: every button has `col` and `row`. The app still follows the game as before.
7. **F7.** Click the game's buttons under the overlay's area.
   - Expected: they still work (the overlay takes no clicks).
8. Copy any `error in` lines from `MelonLoader\Latest.log`, including `error in Overlay` (two buttons in one cell) and `error in OnGUI`.
