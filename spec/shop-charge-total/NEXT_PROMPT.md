# Next prompt: Shop overlay shows the charge total

**Status:** Complete (2026-10-24). Test J confirmed in-game.

In the read-only MelonLoader mod for Calculate It, the shop overlay marks offers for buttons the player already owns with a green `+1`. Change only that text: show the charge count after buying (owned `uses` + 1; the game's `ChargeOwnedOffer` adds exactly one charge). Nothing else changes. Mod only; net472. The solver app doesn't change.

This is small and touches the same file as `spec/prime-button/`; it can be done in the same session and tested in the same game session.

## Read first

1. `CLAUDE.md`: the mod must stay read-only (Steam achievements).
2. `spec/shop-charge-total/requirements.md` and `design.md`.
3. `GameHook.MelonLoader/CalcHookCore.cs`: `ShopSlot`, `ReadShop`, `Offer`, `DrawShopCell`.

## Steps

1. `dotnet build GameHook.MelonLoader -c Release` and `dotnet test`; note they pass.
2. `ShopSlot.OwnedUses`, the owned map in `ReadShop`, `Offer`, `DrawShopCell`.
3. Build the mod with no warnings.
4. Add "Test J: Shop charge total" as `user-verification/test-x.md` from the design, then stop and ask the user to run it.
5. After the report: Status lines to complete, "As built" in the design, `CLAUDE.md` (the `shop-overlay/` bullet and the `shop-charge-total/` entry).

## Check the work

- `dotnet build GameHook.MelonLoader -c Release`: no warnings.
- `dotnet test`: all pass (no app change expected).
