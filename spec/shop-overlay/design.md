# Design: Shop overlay (challenge 9)

**Status:** Complete (2026-10-24). Tested in-game (Test G, all steps). G4 bug fixed and re-tested.

## Overview

All changes are in the mod (`GameHook.MelonLoader/CalcHookCore.cs`). No new file, no new Harmony patch, no change to `ButtonSymbols.cs`, `Mod.cs` (it already forwards `OnGUI`) or the solver app.

1. `ShopSlot` struct and `ReadShop()`: read the six offers and the box.
2. `UpdateOverlay` also refreshes the shop snapshot (same 0.1 s timer).
3. `DrawOverlay` returns the calculator panel's rect and scale; a new `DrawShop` draws the second panel to its right.
4. `Poll` / `WriteJson`: the shop goes into the logged change line and `state.json`.

## Types

```csharp
struct ShopSlot
{
    public string Slot;       // GameObject name: "item1", "item1 (3)", ..., "box"
    public string Key;        // button id (GetAlchemyButtonKey); null for the closed box or an empty slot
    public int Cost;
    public bool Glass, Legendary, Owned, Active;
    public bool Opened;       // box slot showing open_box's revealed offer
    public float X, Y;        // anchoredPosition under vitrina/items
}
```

## Reading the shop

```csharp
// Shop panel (StartLvl.shops[0] = vitrina) slides to local y 221 when shown and 1320 when hidden.
const float ShopShownBelowY = 770f;
const int BoxCost = 3;   // BuyBox.Buy: hard-coded 3 coins

bool ShopOpen()
{
    var shops = _level.shops;
    return shops != null && shops.Length > 0 && shops[0] != null
        && shops[0].transform.localPosition.y < ShopShownBelowY;
}

List<ShopSlot> ReadShop(List<ButtonInfo> owned)
{
    var list = new List<ShopSlot>();
    var ownedKeys = new HashSet<string>(owned.Select(b => b.Key));
    foreach (var go in _level.items ?? new GameObject[0])
    {
        if (go == null) continue;
        var r = go.GetComponent<Randomisator>();
        if (r == null) continue;                       // items[6] is "Next"
        list.Add(Offer(go, r, go.name, ownedKeys));
    }
    var box = _level.box;
    if (box != null)
    {
        var slot = new ShopSlot { Slot = box.name, Cost = BoxCost, Active = box.activeInHierarchy };
        Pos(box, ref slot);
        if (!slot.Active)
        {
            // Box bought: show the revealed offer while open_box is up. Never read it while closed (stale roll).
            var bb = box.GetComponent<BuyBox>();
            var r = bb != null && bb.toOpen != null && bb.toOpen.activeInHierarchy
                ? bb.toOpen.GetComponentInChildren<Randomisator>(false) : null;
            if (r != null)
            {
                var o = Offer(r.gameObject, r, box.name, ownedKeys);
                o.X = slot.X; o.Y = slot.Y; o.Opened = true;
                slot = o;
            }
        }
        list.Add(slot);
    }
    return list;
}
```

`Offer` fills `Key` only when the object is `activeInHierarchy` (an inactive offer is a bought one; its fields are stale): `Key = _level.GetAlchemyButtonKey(go)` (empty string → `null`), `Cost = r.cost`, `Glass = r.glassed`, `Legendary = r.type is 10 or 11 or 28 or 46 or 47`, `Owned = ownedKeys.Contains(Key)`. A throw from `GetAlchemyButtonKey` (stale `subtype` index) is caught per slot: `Report("Shop", e)` once, `Key = null`. `Pos` reads `((RectTransform)go.transform).anchoredPosition`.

`_level.items`, `_level.box`, `_level.shops`, `BuyBox.toOpen`, `Randomisator.cost/glassed/type` are all public, so no reflection. `GetAlchemyButtonKey` is the only call into game code; it reads `GetComponent<Randomisator>()`, an array element and `name`, nothing else.

## Overlay state and refresh

```csharp
List<ShopSlot> _shopSlots = new List<ShopSlot>();   // empty when the shop is closed
int _shopCoins;
```

In `UpdateOverlay`, after `_overlayButtons = ReadButtons();`:

```csharp
_shopSlots = ShopOpen() ? ReadShop(_overlayButtons) : new List<ShopSlot>();
_shopCoins = _level.coins;
```

Wrapped so a shop failure leaves the calculator snapshot intact (`Safe("Shop", ...)` around those two lines).

## Drawing

`DrawOverlay` keeps drawing the calculator as today, and records the panel it drew: `Rect calcPanel` (in scaled GUI units) and the scale `s`. When no calculator panel was drawn, `calcPanel` is a zero-width rect at `x = 16 - 8`, bottom `Screen.height / s - 16`, and `s = Screen.height / 1080f`. Then, still inside the same `GUI.matrix` scope, `DrawShop(calcPanel)` runs if `_shopSlots` has at least one slot.

Rearrange the early return: today `DrawOverlay` returns when `_overlayButtons.Count == 0`; it should only skip the calculator part, so the shop can still draw.

`DrawShop(Rect calcPanel)`:

1. **Rows:** group slots by `Y` rounded to 0.1 (`Mathf.Round(y * 10)`), sort rows by `Y` descending (top first), slots in a row by `X` ascending.
2. **Width:** `nCols = max(4, longest row)`. Panel `width = pad*2 + nCols*cw + (nCols-1)*gap`, `height = pad*2 + head + nRows*ch + (nRows-1)*gap` (same constants as the calculator: cw 64, ch 48, gap 4, pad 8, head 24).
3. **Place:** `x0 = calcPanel.xMax + 8`, `y0 = calcPanel.yMax - height` (bottom-aligned).
4. **Header:** `Shop` left, `$` + coins right (`_headerStyle`, `_hintStyle`).
5. **Cells:** row `r` with `k` slots starts at `gx + (nCols - k) * (cw + gap) / 2` (centered), so 3 top slots sit half a cell in over 4 bottom slots.
6. **Each cell** (`DrawShopCell`):
   - empty (`Key == null` and not the closed box): `DrawCell(rect, "", null, false, true)` (dim).
   - closed box (`Slot` is the box, `Active`): symbol `?`, price `$3`.
   - opened box: `DrawCell(rect, SymbolOf(Key), null, Glass, false)` (no price; its `cost` is 0 because the box was paid for).
   - otherwise `DrawCell(rect, SymbolOf(Key), Cost == 0 ? "free" : "$" + Cost, Glass, false)`. `Cost` 0 is the Sale passive (`passive_free`, always offer id 1, bottom-left).
   - `Legendary`: after the cell, a 2 px gold frame (`GUI.Box` with four thin rects, color `(0.95, 0.75, 0.2)`).
   - `Opened`: the same frame in white, so the revealed box offer stands out.
   - `Owned`: `+1` in the top-left corner (a new `_ownedStyle`: `alignment = UpperLeft`, `fontSize = 11`, built in `BuildStyles`).

`DrawCell`'s `uses` argument is just the corner text, so the price goes through it unchanged.

No new textures; the frame reuses `_white` with `GUI.backgroundColor`.

## Data: `state.json` and the log line

`Poll` reads the shop too (`var shop = ShopOpen() ? ReadShop(buttons) : null;`) and appends ` shop=[item1:X4$4, item1 (1):flip$6g, ..., box:?]` (key or `?`/`-`, cost, `g` for glass, `L` for legendary, `+` for owned) to the change line, so a reroll, purchase or box opening is logged once and rewrites `state.json`. `WriteJson` writes:

```json
"shop": {
  "open": true,
  "coins": 14,
  "slots": [
    { "slot": "item1", "id": "operator_x4", "cost": 4, "glass": false, "legendary": false, "owned": false, "active": true, "opened": false, "x": 0.1, "y": -0.5 },
    { "slot": "box", "id": null, "cost": 3, "glass": false, "legendary": false, "owned": false, "active": true, "opened": false, "x": 2.6, "y": -2.0 }
  ]
}
```

or `"shop": null` when it is closed. Floats with `InvariantCulture`, one decimal. `puzzle.json` is unchanged, and `CheckPuzzle`'s fingerprint doesn't include the shop.

## Read-only check

- New reads: `StartLvl.shops`, `items`, `box`, `coins`; `BuyBox.toOpen`; `Randomisator.cost`, `glassed`, `type`; `RectTransform.anchoredPosition`; `Transform.localPosition`; `activeInHierarchy`.
- New call: `StartLvl.GetAlchemyButtonKey` (reads only).
- Not used: `Randomisator.Buy`, `OnEnable`, `NextRandom`, `RestoreRandom` (they advance the shop's random state or buy), `BuyBox.Buy`, `RerollShop`, anything with `SetActive`.

## Test plan

### Unit tests

None new: the code is Unity-bound (`ButtonSymbols` already has its tests). Optional: move the row grouping (`(x, y)` list → rows, centered offsets) into a static helper in `ButtonSymbols.cs`'s style (plain C#, linked into the tests) and test it with the scene positions (3 top, 4 bottom → offsets 0.5, 0) and with 4 + 4.

### Build

- `dotnet build GameHook.MelonLoader -c Release` with no warnings.
- `dotnet build`, `dotnet test`: all tests pass (352 before this change).

### In-game ("Test G: Shop overlay" in `user-verification/`)

- G1: Challenge 9 run, win round 1. When the shop slides in, a `Shop` panel appears to the right of the calculator overlay with 3 slots on top and 4 below; the bottom-right one is `?` with `$3`. Each symbol matches the offer: hover each offer and compare with the overlay's slot (or buy one and see which button lands on the calculator).- G2: Prices match the price tags. Coins in the header match the game. With the **Sale** passive: the bottom-left slot shows `free`, like the game's `0$` tag. With **Discount**: every price is 1 lower.
- G3: Reroll: the symbols change within 0.1 s. Buy an offer: its slot goes empty. Buy an offer for a button you already own: before buying, the slot showed `+1`.
- G4: Buy the box (3 coins): the box slot shows the revealed button (white frame) while the box panel is open; after taking it, the slot is empty. Note whether the revealed symbol matches what you get.
- G5: Click `Next`: the shop panel disappears as the shop slides out; the calculator overlay stays.
- G6: F9 hides both panels. In a normal run (not challenge 9), F9 shows them; the shop panel's symbols match the readable labels on screen.
- G7: `state.json` has a `shop` object with 7 slots while the shop is open (note `x` / `y`, open question 2) and `null` after `Next`.

## As built (2026-10-24)

Built as designed, all in `CalcHookCore.cs` (`ShopSlot`, `ShopOpen`, `ShopNow`, `ReadShop`, `Offer`, `Pos`; `DrawOverlay` split into `DrawCalculator` + `DrawShop` / `DrawShopCell` / `DrawFrame`; `_ownedStyle`). Tested in-game (Test G, challenge 9): G1–G3, G5–G7 passed on the first run; G4 found one bug, fixed.

- **Runtime positions (open question 2):** rows match the scene (y -0.5 top, -2.0 bottom). Slightly different x: top `item1` 0.1, `item1 (1)` 1.2, `item1 (2)` **2.3** (scene 2.2); bottom `item1 (3)` -0.2, `item1 (4)` 0.7, `item1 (5)` 1.7, `box` 2.6. 7 slots confirmed. Rows are grouped by y, so the small x shift doesn't matter.
- **Prices:** `Randomisator.cost` matches the price tags (e.g. 4, 6, 3, 5). Sale (`free`) and Discount behave as in the code.
- **Legendary:** `operator_near` showed `legendary: true` with the gold frame.
- **Bug found in G4 (fixed):** the opened box's offer is a live object, so its `Active` is true and the `?` check (`IsBox && Active`) hid it behind `?` with the white frame. The check is now `IsBox && Active && !Opened`, in both the drawing and the log line. Re-tested: the revealed button shows correctly.
- The log line marks an opened box offer with `o` and an owned offer with `+`; `-` is an empty slot, `?` the closed box.
- `state.json` slots come in `StartLvl.items` order (not screen order); the overlay sorts by `x`/`y`.
