# Test J: Shop charge total (done, 2026-10-24; J1–J3 confirmed)

1. Rebuild and reinstall the mod. Use challenge 9, or press F9 in any run.
2. **J1.** Open the shop with an offer for a button you own.
   - Expected: its green corner text is the owned charge count + 1 (compare with the calculator overlay).
3. **J2.** Buy it.
   - Expected: the calculator overlay's count goes up by 1 and matches what the shop showed.
4. **J3.** Look at an offer for a button you don't own.
   - Expected: no green text.
5. Copy any `error in` lines from `MelonLoader\Latest.log`.
