# Design: Shop overlay shows the charge total

**Status:** Complete (2026-10-24). Test J confirmed in-game.

## Overview

`ReadShop` already knows which offers are owned (`ownedKeys`, a set of ids). Make it a map from id to the owned charge count, store that count on each `ShopSlot`, and draw it + 1 instead of `+1`. All in `GameHook.MelonLoader/CalcHookCore.cs`.

## `ShopSlot`

Add:

```csharp
public int OwnedUses;     // owned button's CountUses.uses; meaningful only when Owned
```

## `ReadShop` / `Offer`

- `HashSet<string> ownedKeys` becomes `Dictionary<string, int> ownedUses` (id → `ButtonInfo.Uses`; keep the first button per id, as `FindOwnedButton` returns the first match).
- In `Offer`:

```csharp
int uses;
s.Owned = s.Key != null && ownedUses.TryGetValue(s.Key, out uses);
if (s.Owned) s.OwnedUses = uses;
```

## `DrawShopCell`

```csharp
if (sl.Owned) GUI.Label(new Rect(rect.x + 3f, rect.y + 1f, rect.width - 3f, rect.height - 1f), (sl.OwnedUses + 1).ToString(), _ownedStyle);
```

Nothing else changes.

## Tests

No unit tests (the mod's shop code needs the game). `dotnet test` must still pass.

## Manual test plan (`user-verification/test-j.md`, new "Test J: Shop charge total")

1. Rebuild and reinstall the mod. Challenge 9, or F9 in any run.
2. **J1.** Open the shop with an offer for a button you own: its green corner text is the owned charge count + 1 (compare with the calculator overlay).
3. **J2.** Buy it: the calculator overlay's count goes up by 1 and matches what the shop showed.
4. **J3.** An offer for a button you don't own has no green text.

## Risks

- None for the game: reads only.

## As built

As designed. `ShopSlot.OwnedUses`; `ReadShop` builds a `Dictionary<string, int>` (first button per id); `Offer` sets `Owned` and `OwnedUses`; `DrawShopCell` draws `OwnedUses + 1`. Mod Release build: 0 warnings. `dotnet test`: 483 pass (no app change). Test J (`user-verification/test-j.md`): J1–J3 confirmed in-game.
