# Requirements: Shop overlay shows the charge total

**Status:** Complete (2026-10-24). Test J confirmed in-game.

## Goal

In the shop overlay (`shop-overlay/`), an offer for a button the player already owns has a small green `+1` in its top-left corner. Replace `+1` with the **charge count the button will have after buying it**: owned charges + 1. For example, `÷` owned with 4 charges shows `5`.

Only the green text changes. When it appears (offers for buttons the player already owns) stays as it is. The solver app doesn't change.

## Background (ilspycmd on `Assembly-CSharp.dll`, 2026-10-24)

| What | Where in the game |
|------|-------------------|
| Buy | `Randomisator.Buy` → `StartLvl.Buy(cost, offer)`. It first calls `ChargeOwnedOffer(offer, ResolveButtonTemplate(offer), cost)`. |
| Owned check | `StartLvl.FindOwnedButton(template)`: an active `CountUses` under `calc_transform` with a `Place` and the same `ButtonKey` as the offer's template. This is the same id the mod already compares (`ShopSlot.Owned`). |
| Owned: charge | `ChargeOwnedOffer`: `countUses.AddUse()` (`AddUses(1)`: `uses += 1`). **Exactly one charge**, whatever the offer is. |

## Rules

1. Mod only, read-only as before.
2. Where the overlay draws the green `+1` today, it draws `uses + 1` instead, where `uses` is the owned button's current `CountUses.uses` (the raw value the mod already reads into `ButtonInfo.Uses`). A drained button (0) shows `1`.
3. Same color, place and style as the `+1`. Nothing else in the overlay, `state.json`, `puzzle.json` or the log changes.

## Examples

| Owned | Offer | Green text |
|-------|-------|------------|
| `÷` with 4 charges | `÷` | `5` |
| `X8` with 0 charges | `X8` | `1` |
| nothing | `flip` | none (unchanged) |

## Out of scope

- Any other shop overlay change (prices, the box, marking offers the game would refuse).

## Open questions

None.
