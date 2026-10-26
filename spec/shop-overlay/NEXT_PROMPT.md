# Next prompt: Shop overlay (challenge 9)

**Status:** Done (2026-10-24). Implemented and tested; see the design's "As built".

Extend the read-only MelonLoader mod for Calculate It. In challenge 9 (the game's *Alchemy*) the shop's offers are redacted like the calculator buttons. The calculator overlay (`spec/redacted-overlay/`, complete) already draws the real button symbols bottom-left. Add a second IMGUI panel to its right that appears while the shop is open and shows the shop's 7 slots (3 offers on top; 3 offers and the mystery box below) in the game's order, each with its solver symbol and price. The mystery box shows `?` until it is opened (its button is rolled only when opened, so it can't be read earlier). Also write the shop to `state.json`. The solver app doesn't change.

## Read first

1. `CLAUDE.md`: project rules. The mod must stay read-only (Steam achievements): only read fields, no presses, no purchases, no rerolls, no `SetActive`, no new patches, no game objects.
2. `spec/shop-overlay/requirements.md` and `design.md`.
3. `spec/redacted-overlay/design.md` (the "As built" section) for the existing overlay.
4. `GameHook.MelonLoader/CalcHookCore.cs`: `ButtonInfo`, `ReadButtons`, `Poll`, `WriteJson`, the overlay section (`UpdateOverlay`, `BuildStyles`, `DrawOverlay`, `DrawCell`), `Safe` / `Report`.
5. Optional, to check a game detail: `ilspycmd -t Randomisator "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"` (also `BuyBox`, `StartLvl` for `GetAlchemyButtonKey`, `ResolveButtonTemplate`, `RerollShop`, `NextLvl`).
6. `user-verification/` ("Test F") for the manual test format.

## Steps

1. Run `dotnet build`, `dotnet test` and `dotnet build GameHook.MelonLoader -c Release` first and note that everything passes.
2. `CalcHookCore`: `ShopSlot`, `ShopOpen`, `ReadShop`, `Offer` (per-slot try/catch through `Report`).
3. `UpdateOverlay`: refresh `_shopSlots` and `_shopCoins` (inside `Safe("Shop", ...)`).
4. `DrawOverlay`: draw the calculator only when it has buttons (don't return early for the shop), keep its panel rect and scale, then `DrawShop`. Add `_ownedStyle` to `BuildStyles`.
5. `Poll` / `WriteJson`: the `shop=[...]` part of the change line and the `shop` object (or `null`) in `state.json`.
6. Add "Test G: Shop overlay" as `user-verification/test-x.md` from the design's test plan. Then stop and ask the user to run it. You can't run the game. The user will answer the open questions (slot count, runtime positions, price vs `cost`) with you.
7. After the user reports back, record the answers, update the Status lines in all three spec files, add an "As built" section to the design, and update `CLAUDE.md` (the mod bullet and the `shop-overlay/` entry under specs).

## Check the work

- `dotnet build GameHook.MelonLoader -c Release` and `dotnet build`, both with no warnings.
- `dotnet test`: all tests pass.
- Read the diff once more for anything that writes to the game or advances its random state: no `SetActive`, no `AddComponent`, no `GUI.Button`, no call to `Randomisator.Buy` / `OnEnable` / `RestoreRandom`, `BuyBox.Buy`, `RerollShop`, no assignment to a game field.
