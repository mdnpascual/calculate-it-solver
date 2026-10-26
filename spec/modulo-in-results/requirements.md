# Requirements: Modulo lines in the results list

**Status:** Complete (2026-10-22).

## Goal

Today the Lines box only shows Division Shortcut lines. The Modulo Shortcut only shows two formulas (Solution 1 and Solution 2) for the A, k and factor pair picked by hand, with no presses or cost.

After this spec, once the Division Shortcut search finishes, the app also searches Modulo Shortcut lines over every factor pair, A and k, turns each one into presses with a cost, and merges up to 100 of them into the Lines box. Lines are ranked by cost, then by how many buttons they would empty, then by how many charges their buttons have left, so cheap lines that don't drain a nearly empty button come first. Finally, the manual Modulo Shortcut rows (A, k, dropdown, Solution 1 and 2) are removed from the UI.

## Background

A Modulo Shortcut line for S, G, A, k and a factor pair M × N of d = L·10^k + S − G (L = A when S < G, A − 1 when S > G) is:

- **Solution 1** (`First`): prepend M + L to S (padded to k places), mod (10^k + N).
- **Solution 2** (`Second`): the same with M and N swapped.

Each is valid only when the result equals G (`Formula.Valid`). See `ModuloShortcut.cs` and `CLAUDE.md`.

## Rules

### 1. Charges are required

1. Every button in the Buttons box must have a charge count: `X1:2`, `10:13`, `3:1`, `mod:4`. A token without `:n` is an error: `Missing charge count in "X1" (write X1:n)`. `=` is still free and needs no count.
2. `:0` still leaves the button out.
3. The game feed already writes a count for every button, so it is unaffected. The default text in the Buttons box gets counts.

### 2. Cost per modulo line

For a valid formula with value X = P·10^k + S (P = the prepended number) and modulus m = 10^k + N:

1. **Left side:** the fewest left-side presses (`dX`, `Xd`, `flip`) that turn S into the digit string `P ‖ S padded with 0s to k digits`. This is the same left-edit search the Division Shortcut uses, so `0X` pads, `dX` prepends, and `Xd` or `flip` are used when cheaper.
2. `mod` (1 press).
3. **Operand:** the fewest presses of `10` and the digit keys that type m.
4. `=` (free).

Cost = presses 1 to 3. A formula is dropped when any step is impossible with the puzzle's buttons, when the presses don't give G in the simulator, or when they need more presses of a button than its charges.

Examples (buttons with enough charges):

| S → G | A, k | Line | Presses | Cost |
|-------|------|------|---------|------|
| 31 → 11 | 1, 2 | `431 mod 105` | `4X mod 10 5 =` | 4 |
| 41 → 48 | 1, 2 | `3241 mod 103` | `2X 3X mod 10 3 =` | 5 |
| 41 → 48 | 1, 2 | `441 mod 131` | `4X mod 1 3 1 =` | 5 |
| 5 → 48 | 1, 2 | `2005 mod 103` | `0X 0X 2X mod 10 3 =` | 6 |

### 3. When the modulo search runs

1. The Division Shortcut search runs first, as now, and its lines stream into the box as now.
2. Only when it **finishes on its own** does the modulo search start. Esc during the division search stops everything; no modulo search follows.
3. **Cost cap:** C = the lowest cost among the accepted division lines (lines rejected for charges don't count). The modulo search keeps lines with cost **at most C + 1**. With no division line (or no `÷`), the cap is the app's press limit (12).
4. The modulo search needs `mod` and at least one of `10` or a digit key. Without them it is skipped and the status says so. If the Division Shortcut can't run (no `÷`, or no `10`/digit key), the modulo search still runs with the cap set to the press limit.
5. S = G: no modulo search.

### 4. Which modulo lines are kept

1. Walk A = 1, 2, 3, … up to **99**. For each A, walk k from the digit count of S upward while a line with that k could still fit the cap. For each k, take every factor pair of d (smallest first, as in the dropdown) and both formulas (Solution 1, then Solution 2).
2. Keep each valid formula whose presses fit the buttons, the charges and the cost cap (rule 2). Two formulas with the same presses count once.
3. Stop at the **100th** kept line, or after A = 99, whichever comes first.
4. Esc during the modulo search stops it. The lines it found so far are not added (the search isn't finished).

### 5. Spare

The spare of a line (division or modulo) = the sum of the charges of the **distinct** buttons it presses, using the charges at puzzle start. A button pressed twice counts once. `=` doesn't count.

Example: `4X mod 10 5 =` with `4X:2, mod:3, 10:5, 5:1` has spare 2 + 3 + 5 + 1 = 11.

A higher spare means the line leans on buttons with plenty of charges left, so it is less likely to use up a scarce button.

### 5b. Drained

The drained count of a line = how many distinct buttons it presses as many times as their charges, so they would be left at 0. `=` doesn't count.

Example: `4X mod 10 5 =` with `4X:2, mod:3, 10:5, 5:1` drains 1 button (`5`). `5X mod 10 4 =` with `5X:2, mod:3, 10:5, 4:3` drains 0, so it ranks first at the same cost.

This is a separate sort key, not a penalty subtracted from the spare, so no weight needs tuning: at the same cost, a line that empties a button always ranks below one that doesn't.

### 6. Ranking and display

1. One list: division and modulo lines together, sorted by
   1. cost, lowest first,
   2. drained, fewest first,
   3. spare, highest first,
   4. division lines before modulo lines,
   5. division lines by the existing `DivisionShortcut.Ranking` tie-breaks; modulo lines in the order they were found (A, k, pair, Solution 1 before 2).
2. This order applies during the division search too (division lines get a spare and drained count as they stream in).
3. The Lines box still shows the top 10 rows. Each row: `presses   (n presses, spare s)`, plus `, empties b` when drained b > 0. A modulo line costing C (a tie) or less competes directly with the division lines: it ranks above a division line of the same cost when it drains fewer buttons or, with equal drained, has a higher spare. Modulo lines at C + 1 are alternatives; it's fine if they fall below the top 10.
4. The status after both searches: `N division line(s), M modulo line(s), R rejected`. While the modulo search runs: `Modulo Shortcut… Esc to stop.`

### 7. Remove the manual Modulo Shortcut rows (last step)

Once the modulo lines are in the Lines box, the manual rows are redundant. After everything above works, remove from the UI:

- Iterator (A) with its − / + buttons,
- Places (k) with its − / + buttons,
- the `d = …, n factor pair(s)` detail label,
- Factors (M × N) dropdown,
- Solution 1 and Solution 2.

The game feed no longer resets A (there is no A box). `ModuloShortcut.cs` and its tests stay: `ModuloSearch` uses `ModuloShortcut.Solve`.

### 8. Unchanged

- The Division Shortcut search itself (paths, parallel workers, charge rejection).

## Decisions taken

- The merged list is ranked **cost first**, then drained, then spare (the first request's "spare, then cost" was a typo).
- The cost cap is **at most** C + 1, not exactly C + 1.
- Every button needs a charge count, so no button is unlimited and the spare is always defined.
- Modulo lines that don't reach the top 10 are fine; they are alternatives.
- Emptying a button is ranked by a separate drained count, not a numeric penalty.

## Open questions

None.

## Out of scope

- Chained operations (`X mod m ÷ n`).
- Speeding up the modulo search with worker threads (only if it turns out slow).
