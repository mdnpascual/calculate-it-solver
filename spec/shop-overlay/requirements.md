# Requirements: Shop overlay (challenge 9)

**Status:** Complete (2026-10-24). Open questions 2 and 3 answered in the design's "As built".

## Goal

In challenge **9** (the game's *Alchemy*) the shop is redacted too: every offer shows a random symbol instead of its label, and its hover name is `$$$$$$$$$`. The mod can still read which button each offer is.

Extend the read-only mod: while the shop is open, draw a second overlay panel to the right of the calculator overlay. It shows the shop's offers in the same rows and order as the game's shop, each with its solver symbol and price. The mystery box shows `?` until it is opened.

The solver app doesn't change.

## Background (ilspycmd on `Assembly-CSharp.dll` and UnityPy on scene `level1`, 2026-10-24)

| What | Where in the game |
|------|-------------------|
| Shop panel | `StartLvl.shops[0]` is `Canvas/ShakerItems/vitrina`. It always stays active; DOTween slides it. Shown: `localPosition = (283, 221, …)` (`StartLvl` round end, `GoAwayRoutine`). Hidden: `(283, 1320, …)` (`NextLvl`). |
| Offers | `StartLvl.items[0..5]`: six `Randomisator` objects under `vitrina/items` (`item1`, `item1 (1)` … `item1 (5)`). `items[6]` is the `Next` button (no `Randomisator`). `RerollShop` and the round end turn each offer off and on; `Randomisator.OnEnable` then rolls a new offer. A bought offer is turned off (`SetActive(false)`). |
| Which button an offer is | `Randomisator.type`, `subtype`, `number` pick a template: `StartLvl.ResolveButtonTemplate` (private) maps type 6/8 → `appenders_tr[subtype]`, 20 → `transformers_tr[subtype]`, 35 → `adders_tr[subtype]`, 36 → `subers_tr[subtype]`, 7 → `all_numbers[number]`, else `buttons_tr[type]`. **`StartLvl.GetAlchemyButtonKey(GameObject)` is public and read-only**: it returns `ButtonKey(template)`, the same id the mod already reports (`CountUses.buttonKey`, else the object name without `(Clone)`). The alchemy challenge uses this key for its own symbol mapping. |
| Price, glass | `Randomisator.cost`, `Randomisator.glassed`. Legendary types: 10, 11, 28, 46, 47 (as in `Infos.id`). The price tag is written from `cost` (`cost_text.text = cost + "$"`), after the passives below, so `cost` is what the player pays. The box's offer (`id` 6) gets `cost = 0` after its tag is written (the 3 coins were paid on the box). |
| Shop layout (scene defaults, `anchoredPosition` under `items`; `Randomisator.id` parsed from the raw scene bytes) | Top row (y = -0.5): `item1` x 0.1 (id 0), `item1 (1)` x 1.2 (id 2), `item1 (2)` x 2.2 (id 4). Bottom row (y = -2.0): `item1 (3)` x -0.2 (**id 1**), `item1 (4)` x 0.7 (id 3), `item1 (5)` x 1.7 (id 5), `box` x 2.6. So the scene has **3 offers on top and 3 offers plus the mystery box on the bottom: 7 slots**. There is no 8th offer object in the scene. |
| Slot roles (`Randomisator.OnEnable`) | id 0 and id 1: a digit key (type 7, `all_numbers[number]`; calculator `Type` 5 rolls type 35–38 instead). id 2: a basic operator (type 0–2 for calculator `Type` 0/2, 6 for `Type` 1, 20 for `Type` 3, 35–38 for `Type` 5). id 3, 4, 5: any unlocked button (type 0–47, not 31). Offers 0–5 never repeat a button (`SelectUniqueShopOffer`). |
| "Sale" passive | In-game name **Sale**, "One of the non-main buttons will be sold for free" = object `passive_free`. Code: `if (id == 1 && passive_free) cost = 0`. **It is not random: it is always offer id 1, the bottom-left slot `item1 (3)`** (a digit-key offer). Nothing in the code hides or covers that slot. |
| "Discount" passive | In-game **Discount**, "All prices in the store are lower by 1" = object `passive_sale` (the names are swapped compared to the in-game names). Code: `cost--` when `cost > 0`. Applied before Sale. |
| Owned passives | `PassivePool.CreatePassive` places them under `pas_parent` at `(-118 + 92 * i, -24)`, up to 4. Not under `vitrina`; whether that strip overlaps the shop on screen isn't known from the code. |
| Mystery box | `StartLvl.box` is `vitrina/items/box` (a `BuyBox`, price 3 coins, not a `Randomisator`, not redacted: it is a box picture). `BuyBox.Buy` takes 3 coins, turns on `BuyBox.toOpen` = `Canvas/ShakerItems/open_box` (a separate panel) and turns the box off. The round end and `RerollShop` turn the box on again. |
| Box contents | `open_box` holds `item1 (6)`, a `Randomisator` with `id` 6. Its offer is **rolled when `open_box` turns on** (`Randomisator.Start` / `OnEnable`), from `System.Random(SeedMaker.Seed + 6)` advanced by `randomDraws`, then filtered by game state (`TrySetBoxChargeOffer` when the calculator is full, `TrySetUniqueShopOffer`, locked buttons, owned legendary buttons). Before that, its `type` / `subtype` / `number` still hold the previous box's roll. On `Buy`, `PrepareBoxReward` can roll again if the offer is no longer valid. **So the box is truly hidden until opened**; predicting it would mean emulating the game's random code and its state checks. Not done (rule 7). |
| Redaction in the shop | `AlchemyChallenge.LateUpdate` masks every active `Randomisator` that isn't `inform` (the six offers and `item1 (6)` in `open_box`). The perk picker every 5 rounds (`choose_passive`, `RandomisatorPassives`) is not masked and is out of scope. |

## Rules

### Mod stays read-only

1. Same as the calculator overlay: IMGUI boxes and labels only (no `GUI.Button`), no new game objects or components, no new Harmony patch. The only game method called is `StartLvl.GetAlchemyButtonKey`, which only reads (`GetComponent`, array lookups, `name`). Everything else is public field reads.

### When the shop overlay shows

2. The shop panel is drawn when **all** of these hold:
   - the calculator overlay is on (`_overlayOn`). That is only automatic in challenge 9 (redacted / *Alchemy*): it turns on when a challenge 9 run starts. In every other challenge it stays off unless the player presses F9. F9 toggles both panels together;
   - the shop is open: `StartLvl.shops[0].transform.localPosition.y < 770` (halfway between shown 221 and hidden 1320, so it appears while the shop slides in and goes away while it slides out);
   - at least one slot is showable (rule 4).
3. In other challenges, F9 shows it too (useful for testing outside challenge 9; labels there are readable on screen, so the overlay can be checked against them).

### Slots

4. Seven slots, read every 0.1 s like the calculator (same refresh):
   - **Six offers** `StartLvl.items[i]` that have a `Randomisator` (skip the rest; `items[6]` is `Next`). An offer that is not `activeInHierarchy` was bought (or removed by the game): its slot is drawn empty (dim, no symbol, no price).
   - **Mystery box**:
     - `StartLvl.box` active → `?` with price `3`.
     - else, `open_box` active (`box.GetComponent<BuyBox>().toOpen.activeInHierarchy`) and its `Randomisator` active → the revealed offer's symbol, price as the game shows it (`cost`), drawn in the box's slot with a highlight so it's clear this is the opened box.
     - else → empty (box bought and its item taken, or closed).
5. **Layout:** rows from each slot's `anchoredPosition.y` (larger y = higher row), order within a row by `anchoredPosition.x`. The box sits in the bottom row with the offers. Each row is centered in a 4-cell-wide panel, so the top row (3 slots) sits half a cell in, like the game's shelf. If the game ever shows a different count per row, the same rule still works (up to 4 per row; more widens the panel).
6. **Each slot shows:**
   - the symbol: `ButtonSymbols.Of(id)`, the same text as the calculator overlay (`÷`, `X4`, `flip`, `(energy)`, …);
   - the price small in the bottom-right corner (`$5`), or `free` when `cost` is 0 (the Sale passive's slot). The opened box's offer shows no price (already paid);
   - glass offers in the same light blue as glass buttons;
   - legendary offers (type 10, 11, 28, 46, 47) with a gold border or tint;
   - a small `+1` in the top-left corner when the player already owns that button (same id among the calculator buttons the mod reads): buying it adds a charge instead of a new button.
7. **Box contents are not predicted.** While the box is closed, the slot shows `?`. The mod doesn't read `item1 (6)`'s stale fields and doesn't emulate `NextRandom`.
8. **Header:** `Shop` on the left and coins on the right (`StartLvl.coins`, for example `$12`), same style as the calculator header.

### Placement and size

9. To the right of the calculator panel, 8 px gap (1080p units), bottom edges aligned. Same scale `s` as the calculator panel and the same cell size (64 × 48), so cells line up visually. If the calculator panel isn't drawn (no buttons read), the shop panel takes its place at the bottom-left corner. The 50% height cap stays the calculator's rule; the shop panel (header + 2 rows) is always shorter than the calculator panel (header + 4 or more rows).

### Data

10. `state.json` gets a `shop` object while the shop is open (`null` otherwise): `open`, `coins`, and `slots`, each with `slot` (`item1`, …, `box`), `id` (null for the closed box), `cost`, `glass`, `legendary`, `active`, `x`, `y` (raw `anchoredPosition`). The shop is added to the change line that `Poll` logs, so a reroll or purchase is logged once. `puzzle.json` doesn't change.
11. **Errors:** a failure reading a slot (for example `GetAlchemyButtonKey` throwing on a stale index) skips that slot and is logged once through `Report`; the rest of the overlay keeps working.

## Examples

Challenge 9, shop after round 3, coins 14. The box is closed:

The shop panel (drawn to the right of the calculator panel):

```
 Shop                         $14
    ┌────────┬────────┬────────┐
    │   X4   │  flip  │+1  ÷   │
    │     $4 │     $6 │     $5 │
┌───┴────┬───┴────┬───┴────┬───┴────┐
│   7    │   -    │  sqr   │   ?    │
│     $2 │     $3 │     $7 │     $3 │
└────────┴────────┴────────┴────────┘
```

`+1`: `÷` is already owned, so buying it adds a charge. The bottom-right `?` is the mystery box.

After paying 3 for the box, the `open_box` panel shows its button; the overlay's box slot shows that button's symbol (highlighted) until it is taken, then goes empty.

## Out of scope

- Predicting the box (rule 7).
- The perk picker (`choose_passive`): not redacted.
- Showing the solver's best line, or which offer the solver would want. Follow-ups.
- The solver app: no change.

## Open questions

1. (Answered, 2026-10-24.) How many slots? 7: 3 offers on top, 3 offers and the mystery box below (code and scene). The user first expected 8, with the bottom-left one hidden by the Sale passive, then agreed: Sale (`passive_free`) only marks the bottom-left offer (id 1) as free and hides nothing.
2. Do the offers' `anchoredPosition` values at runtime match the scene defaults (rows at y = -0.5 and -2.0)? `state.json` (`shop.slots[].x/y`) answers it.
3. (Answered by the code, 2026-10-24.) The price tag is `cost + "$"`, written after Discount and Sale, so `Randomisator.cost` is the price shown. Only the box's revealed offer differs (`cost` 0, tag left as rolled); the overlay shows no price there.
