# Next prompt: implement the leading-zeros fix for `flip` and `dX`

**Status:** Done (2026-10-24). Kept for reference.

Implement the bugfix in `spec/leading-zeros-flip-and-prepend/`. In the game, `flip` and `dX` read the left side as a number, so leading zeros drop (`120` → `flip` → `21`; `04` → `1X` → `14`). The solver treats them as string edits that keep the zeros.

## Read first

1. `spec/leading-zeros-flip-and-prepend/requirements.md`: the rule, the evidence, the examples table (the "After" column is the target) and the 8 tests that change.
2. `spec/leading-zeros-flip-and-prepend/design.md`: the exact code for each file, why the backward search lists plain numbers only, and the test plan.
3. The code: `CalculatorPuzzle/EngineeringCalculator.cs` (`Run`), `CalculatorPuzzle/LeftSearch.cs` (`Actions`, `Join`), `CalculatorPuzzle/LeftEdits.cs` (`Plan`), `CalculatorPuzzle/ModuloShortcut.cs` (class comment only), `CalculatorPuzzle/ModuloSearch.cs` (no change; read `Presses`).
4. CLAUDE.md: the Engineering Calculator table and the `ModuloShortcut.cs` bullet, both of which get corrected.

## Steps

1. Add `EngineeringCalculator.Prepend` and `EngineeringCalculator.Flip`, and use them in `Run`.
2. `LeftSearch`: forward `dX` and `flip` use the helpers; backward `dX` needs a plain rest; backward `flip` uses `FlipPreimages`; `Join` adds the by-value lookup. Update the class summary.
3. `LeftEdits.Plan`: no `dX` on a piece with a leading zero; `flip` trims zeros at both ends of the piece.
4. Fix the `ModuloShortcut` class comment.
5. Update the 8 tests and add the new ones from the design's test plan.
6. Update CLAUDE.md (the table, the leading-zeros sentence, the `ModuloShortcut.cs` and `LeftSearch.cs` bullets, and this spec's entry in the `spec/` list as complete), and set the status line in requirements.md and design.md to Done.

## Check

- `dotnet build` with no new warnings.
- `dotnet test`: everything passes (308 before, plus the new tests).
- Release timing, as in requirements.md "Checked": the arithmetic phase for 41 → 483 with every button stays around 0.6–0.7 s, and the Old Calculator benchmark (`X++, X--, X+10, X-10, flip, 1->0, 2->5, 7->3`, start 1..99 step 7, goal 1..124 step 9, cap 12, `ArithmeticSearch.Find` with max 1 line) stays around 0.35–0.4 s with mean cost 4.58. If it is much slower, compare the backward preimages against the design; listing trailing-zero or leading-zero preimages is what made it slow.
- Don't change the game mod. Don't run `git init`.
