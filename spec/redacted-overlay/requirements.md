# Requirements: Redacted challenge overlay (challenge 9)

**Status:** Complete (2026-10-24). Tested in-game (Test F).

## Goal

In challenge **9** ("redacted"; the game code calls it *Alchemy*), the game hides every button's label behind a random symbol. The player can't tell `÷` from `X4`. The mod can still read each button's real id (it already does: `state.json` from a challenge 9 run lists `number1`, `operator+`, `operator/`, … with their real labels).

Update the read-only mod so it draws its own small calculator in the bottom-left corner of the game window. Each button sits in the same grid cell as in the game and shows its real symbol, so the player can match the overlay to the redacted keys by position.

The solver app doesn't change: `puzzle.json` keeps working in challenge 9, and its lines already use the real tokens.

## Background (ilspycmd on `Assembly-CSharp.dll`, 2026-10-24)

| What | Where in the game |
|------|-------------------|
| Challenge id | `GamePrefs.GetInt("challenge") == 9`. `StartLvl.Start` adds an `AlchemyChallenge` component in that case. |
| How labels are hidden | `AlchemyChallenge.LateUpdate` puts an `AlchemyButtonView` on every active child of `calc_transform` that has a `Place`, and on the shop offers (`Randomisator`). The view masks the button's `Text` / `icon` (`AlchemyGraphicMask`) and shows one of 128 sprites from `Resources/AlchemySymbols`. |
| Symbol mapping | `AlchemyChallenge.CreateMapping`: the button keys (`StartLvl.GetAlchemyButtonKey` = `ButtonKey` of the template) sorted ordinally, then a Fisher–Yates shuffle with `System.Random(StartLvl.AlchemySeed)`. One key always gets the same symbol in a run; a new seed gives new symbols. |
| What stays readable | `CountUses.buttonKey`, `GameObject.name` and the `Text.text` under the mask are untouched. So the ids the mod reports are correct in challenge 9. Item names in the shop become `$$$$$$$$$` (`Randomisator`), but the button keys don't. |
| Button position | Each button is a `RectTransform` under `calc_transform`. Bought buttons: `anchoredPosition = (-300 + (i % 8) * 91, -115 - (i / 8) * 80)` for slot `i` (`StartLvl`, `MiniCreate`). The starting calculator (`CreateCalc`) uses `(-300 + (i % 4 + 2) * 91, -115 - (i / 4) * 80)`: a 4-wide block in columns 2–5. The grid is 8 columns wide; `PlaceFind` uses slots up to 32 (4 rows). |
| Why the mod's order is wrong | `ReadButtons` lists buttons in hierarchy order (`GetComponentsInChildren`). `SubBuy` sets sibling index to `Place.overall + 3`, but `CreateCalc` writes `Place.x = i % 8` while drawing at column `i % 4 + 2`, and `Place.OnEnable` recomputes `x` with step 89, not 91. So neither the list order nor `Place` is a reliable position. `anchoredPosition` is what the game draws. |

## Rules

### Mod stays read-only

1. The overlay is drawn with Unity IMGUI (`OnGUI`) from the mod. It creates no game objects, adds no components, and draws only boxes and labels (no `GUI.Button`, so it can't take a click). No new Harmony patch. No game method that changes state is called. Reading `Input.GetKeyDown` is a read.

### Grid position

2. For each button the mod already reads, it computes a grid cell from the button's `RectTransform.anchoredPosition`:
   - `col = round((x + 300) / 91)`
   - `row = round(-(y + 115) / 80)`
3. `puzzle.json` and `state.json` get `"col"` and `"row"` on every button. The button order in the files stays as today (the app doesn't depend on it, and changing it isn't needed once each button carries its cell). `version` stays 1; the app ignores the new fields.

### Overlay

4. **Where:** anchored to the bottom-left corner of the game window, with a 16 px margin (at 1080p; see rule 9). It stays anchored when the window is resized.
5. **When:** shown by default while the challenge is 9 and the scene has a `StartLvl` (a run is on). Hidden in the menu and in other challenges. **F9** toggles it at any time, in any challenge (useful to test the layout outside challenge 9). The game doesn't use F9.
6. **Layout:** the calculator starts 4 wide and 4 high (columns 2–5, rows 0–3). It widens by one column on each side, twice: 6 wide (columns 1–6), then 8 wide (columns 0–7). The overlay shows the same columns as the game, read from `CalcSize.size_x` on the `Calculator` object (1 → columns 2–5, 2 → 1–6, 3 or more → 0–7). Rows run from 0 to the largest used row (at least 3). If `CalcSize` can't be read, the range is the smallest to the largest used column. Empty cells are drawn as blank gaps.
   - **`=` key:** it isn't in the mod's button list, but it never moves: 4th row, 3rd column of the starting calculator, which is grid cell column 4, row 3. The overlay always draws `=` there (no charges).
7. **Each button shows:**
   - its symbol, large and centered (rule 8);
   - its charges (`uses`) small in the bottom-right corner of the cell;
   - glass buttons with a light blue tint; buttons at 0 charges greyed out.
   A header line above the grid shows `left → goal` (for example `2 → 64`) and the hint `F9 hide`.
8. **Symbol:** the solver token for the id, the same text the app's Lines box uses (`GameLink.MapButton`): `÷`, `×`, `+`, `-`, `mod`, `X4`, `4X`, `flip`, `reset`, `X++`, `3->0`, `0`–`9`, `10`, …
   - **Unsupported id** (`MapButton` gives nothing): `(` + the id without its `operator` or `number` prefix and a leading `_` + `)`. Examples: `operator_energy` → `(energy)`, `operator_prime` → `(prime)`, `operator%` → `(%)`, `operator_equal` → `(equal)`, `number11` → `(11)`, `number_charge` → `(charge)`.
9. **Size:** cells about 64 × 48 px at 1080p, scaled with the window height (`Screen.height / 1080`), so the overlay covers the same share of the screen at any resolution. The whole panel (header included) is at most 50% of the screen height; when it would be taller, everything is scaled down to fit. Long symbols (`(complement)`) shrink their font to fit the cell.
10. **Refresh:** the overlay's data is re-read at most 10 times a second (not every `OnGUI` call), so a press, a purchase or a charge change shows within 0.1 s.
11. **Errors:** any exception in the overlay is logged once (the existing `Report`) and the overlay skips that frame; the rest of the mod keeps working.

## Examples

Challenge 9, round 1, start 2, goal 64. The game's starting calculator (buttons from a real `state.json`, `=` at its fixed cell; other cells assumed from the `CreateCalc` formula, to be confirmed in-game):

```
 2 → 64                         F9 hide
┌────────┬────────┬────────┬────────┐
│   1    │   2    │   3    │   +    │
├────────┼────────┼────────┼────────┤
│   4    │   5    │   6    │   -    │
├────────┼────────┼────────┼────────┤
│   7    │   8    │   9    │   ×    │
├────────┼────────┼────────┼────────┤
│(energy)│   0    │   =    │   ÷    │
└────────┴────────┴────────┴────────┘
```

(Charges are drawn small in each cell's corner; left out here.)

| id | overlay text |
|----|--------------|
| `operator/` | `÷` |
| `operator*` | `×` |
| `operator_x4` | `X4` |
| `operator_3to0` | `3->0` |
| `operator_reverse` | `flip` |
| `number10` | `10` |
| `operator_energy` | `(energy)` |
| `number_mw` | `(mw)` |

## Out of scope

- The shop: offers (`Randomisator`) are redacted too. Showing their real ids is a follow-up: `spec/shop-overlay/`.
- The solver app: no change. A "redacted" note in its status is not needed.
- Showing the solver's best line in the overlay, or highlighting the Market Demand required button. Follow-ups.
- Mapping the 128 alchemy sprites back to ids (not needed: position is enough).

## Open questions

1. (Answered by the user, 2026-10-24.) Where is `=`? The starting calculator is 4 × 4 and `=` is in its 4th row, 3rd column (grid column 4, row 3). The calculator widens one column left and right, twice. See rule 6.
2. (Answered, 2026-10-24.) Yes: the user ran Test F in-game and the computed cells match the screen (start calculator and after widening). No mismatches were reported.
