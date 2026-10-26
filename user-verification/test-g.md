# Test G: Shop overlay (done, 2026-10-24)

While the shop is open the mod draws a second panel to the right of the calculator overlay: 3 offers on top, 3 offers and the mystery box below, each with its solver symbol and price. It also writes a `shop` object to `state.json`. Rebuild and reinstall the mod first (as in Test C).

1. **G1.** Start a challenge 9 run and win round 1.
   - Expected: when the shop slides in, a `Shop` panel appears right of the calculator overlay, 3 slots on top and 4 below. The bottom-right one is `?` with `$3`. Hover each offer, or buy one and see which button lands on the calculator, and compare with the panel's symbol.
2. **G2.** Compare prices and coins with the game's tags.
   - Expected: prices match the price tags; the header coins match the game. With the **Sale** passive the bottom-left slot shows `free` (the game's `0$`). With **Discount** every price is 1 lower.
3. **G3.** Reroll, then buy an offer.
   - Expected: after a reroll the symbols change within 0.1 s. A bought offer's slot goes empty. Before buying a button you already own, its slot showed `+1`.
4. **G4.** Buy the mystery box (3 coins).
   - Expected: the box slot shows the revealed button with a white frame while the box panel is open, then goes empty once you take it. Note whether the symbol matches what you get.
5. **G5.** Click `Next`.
   - Expected: the shop panel disappears as the shop slides out; the calculator overlay stays.
6. **G6.** Press F9.
   - Expected: both panels hide and show. In a normal run (not challenge 9) F9 shows both; the shop symbols match the readable labels on screen. Legendary offers have a gold frame; glass offers are light blue.
7. **G7.** Open `%LOCALAPPDATA%\CalcHook\state.json` with the shop open, then after `Next`.
   - Expected: `shop` has 7 `slots` while open and is `null` after `Next`. Report the `x` / `y` of every slot (open question 2: scene defaults are rows at y -0.5 and -2.0) and each `cost` against its price tag.
8. Copy any `error in` lines from `MelonLoader\Latest.log`, including `error in Shop` and `error in OnGUI`, and one `shop=[...]` change line.
