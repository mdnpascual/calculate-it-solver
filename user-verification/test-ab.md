# Test AB: Blind passive in boss rule 14

Checks the Blind checkbox and the G + k − 1 target (`spec/blind-passive-rule-14/`). Install the freshly built `CalcHook.MelonLoader.dll` first. You need the `passive_blind` passive (the first non-glass press of each round is free).

1. **AB1.** Own `passive_blind`. At the start of a round look at `%LOCALAPPDATA%\CalcHook\puzzle.json` and `state.json`, then make one press and look at `state.json` again.
   - Expected: `puzzle.json` has `"blind": true`; `state.json` has `true` before the first press and `false` after it. `puzzle.json` is not written again after the press (its `seq` stays). The log line says `blind` while the flag is up.
2. **AB2.** Pick `passive_blind` at a passive picker.
   - Expected: the puzzle is published again and the app ticks the Blind checkbox.
3. **AB3.** (Boss rule 14 with the passive; challenge 4 helps.) Play the top line from the app.
   - Expected: the round is won, and the goal shown after the first press is still G (it rises from the second charged press).
4. **AB4.** (Boss rule 14, if a glass button is first in the line.) Press the glass button, then a normal one.
   - Expected: the goal stays the same on the glass press and on the next press (the free press is still used by the normal one).
5. **AB5.** Untick Blind by hand in the app and press Enter.
   - Expected: the lines change to G + k targets (the status shows no new text; the `goal N` in each line moves by 1).
