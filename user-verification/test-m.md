# Test M: Two-operator lines (M1 and M3 confirmed 2026-10-24; M2 open)

1. Rebuild the solver (`dotnet build`) and start it with Follow game on. Every two-operator line in the Lines box ends with `TEST M: report back` inside its brackets.
2. **M1.** Play until a line in the Lines box ends with `TEST M: report back`. Any round counts (normal, boss rule 11, boss rule 14). Write down S, G, the buttons, the boss rule if any, and the line. Press the line in the game.
   - Expected: each press gives the display the solver expects, and the round ends (for rule 11: with the required button pressed; for rule 14: at the raised goal).
3. **M2.** The status line ends with `TEST M2: boss required, report back` in every boss round with a required button, even when no line is found. If you get a boss rule 11 round where the required operator points the wrong way (as 59 → 40 with `+`), check the Lines box isn't empty.
   - Expected: two-operator lines with the marker, all pressing the required button (59 → 40: `- 2 0 = + 1 =`).
4. **M3.** A normal round where a 1–3 press arithmetic line exists.
   - Expected: the search time feels the same as before; two-operator lines, if any, rank below the shorter lines.

Result: M1 passed (`flip × 5 = / 3 =`, 73 → 61 round, worked in the game). M3: search time fine. M2 (boss rule 11 with a wrong-way operator) not seen yet. The line marker `TEST M: report back` was removed; a status-line marker stays for M2 until it is confirmed.

Report: for each marked line you tried, whether it worked, and if not, the display after each press. After M2 passes: remove `BossTestMarker` in `MainForm.cs` and mark this test done in its heading.
