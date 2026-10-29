# Test AC: Blind passive and the empty count (done, 2026-10-29; AC1-AC3 confirmed)

Checks that the first press of a round is not counted as a charge in `empty N` (`spec/blind-empty-count/`). You need the `passive_blind` passive (the first non-glass press of each round is free).

1. **AC1.** Own `passive_blind`. In the app tick Blind and type a Buttons box where a 1-charge button solves the round (for example `X++:1` for S 41, G 42). Press Enter.
   - Expected: the line `X++` shows no `empty`.
2. **AC2.** Untick Blind and press Enter.
   - Expected: the same line shows `empty 1`.
3. **AC3.** Tick Blind again and play the line in the game.
   - Expected: the round is won and the button keeps its charge after the press.
