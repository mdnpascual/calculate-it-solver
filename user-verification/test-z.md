# Test Z: Passive picker overlay (challenge 9) (done, 2026-10-27; Z6 not run)

Checks the top-center passive panel (`spec/passive-picker-overlay/`). Install the freshly built `CalcHook.MelonLoader.dll` first. In challenge 9 the game shows Latin button texts for passives; the panel shows the real names and descriptions.

1. **Z1.** Start a challenge 9 run.
   - Expected: before round 1 the passive picker opens, and a top-center panel ("Passives") shows 3 boxes with real passive names (Near1, Odd, …) and descriptions, in the same left-to-right order as the icons. Hovering an icon in the game still shows a Latin button text. If no panel appears, tell me whether the picker was open and copy any `error in` lines from `MelonLoader\Latest.log`.
2. **Z2.** Pick a passive.
   - Expected: the panel goes away with the picker.
3. **Z3.** Beat the round 5 boss.
   - Expected: the panel shows again with the new 3 offers.
4. **Z4.** Press F9 while the picker is open (challenge 9).
   - Expected: the passive panel hides with the other panels; F9 again shows it.
5. **Z5.** Start another challenge (for example 0 or 8), press F9 so the other overlays are on, and reach a picker (before round 1, and after the round 5 boss). Then go straight from a challenge 9 run to another challenge's run.
   - Expected: the calculator overlay shows, the passive panel never does.
6. **Z6.** Set the language to Russian, challenge 9 picker.
   - Expected: Russian titles and descriptions.
7. **Z7.** Playtest the size (75% of the screen width, at most 25% of its height).
   - Expected: the text is readable, and every passive icon (and its hover box) is still visible and clickable. Note what to change: width, height cap, fonts, top offset.
8. **Z8.** Open `%LOCALAPPDATA%\CalcHook\state.json` while the picker is open.
   - Expected: `passivePicker.offers` has the 3 ids with name and description; it is `null` when the picker is closed. `MelonLoader\Latest.log` has a change line with `passives=[…]`.
