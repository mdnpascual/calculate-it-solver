# Next prompt: Left-side transforms

**Status:** Complete (2026-10-23). Kept for reference; see "As built" in design.md.

Add Calculate It's left-side "value in, value out" buttons to the calculator puzzle solver: `X++`, `X--`, `X+k`, `X-k`, `X*2`, `X/2`, `<<`, `>>`, `sqr`, `sqrt`, the digit maps `a->b`, `to25` and `round`. Each button is one entry in a transform catalogue (buttons as data). The simulator, `Buttons.Parse` and the game feed learn them. A new `LeftSearch` (BFS over left-side displays with every left-side button, forward and backward, plus a meet-in-the-middle `Connect`) gives the arithmetic phase edits-only lines and extra candidates before and after its one operator. The Division and Modulo Shortcuts don't change. The repo is C#: .NET 9 WinForms + xUnit. Not a git repo; don't run `git init`.

## Read first

1. `CLAUDE.md`: project rules and the Engineering Calculator button table.
2. `spec/left-transforms/requirements.md` and `design.md`.
3. `docs/button-id.md`: the game ids and what each button does.
4. `CalculatorPuzzle/EngineeringCalculator.cs` (`Key`, `Press`, `Buttons`, `Parse`, `Run`), `GameLink.cs` (`MapButton`), `LeftEdits.cs` (the BFS style and tie order to follow), `ArithmeticSearch.cs` (`Find`: where the new candidates plug in), `DivisionShortcut.cs` (`TypeOperand`, `Pieces`, `Cores`).
5. `CalculatorPuzzle.Tests/` for the test style (`TestButtons.Parse` adds `:9` to tokens without a count; `ArithmeticTests.cs` for the arithmetic phase).

## Steps

1. Run `dotnet build` and `dotnet test` and note that everything passes.
2. Add `LeftTransforms.cs` (design §1) and `LeftTransformTests.cs`, including the preimage property test.
3. `Key.Transform`, `Press.Transform`, `Buttons.Transforms`, the `Parse` tokens and the simulator case (design §2–3). Add the simulator and parse tests.
4. `MapButton` (design §4) and its tests.
5. Add `LeftSearch.cs` (design §5) and `LeftSearchTests.cs`.
6. Wire it into `ArithmeticSearch.Find` only when the puzzle has transforms (design §6). All existing tests must pass unchanged; add the new arithmetic tests.
7. Time `ArithmeticSearch.Find` for 41 → 483 with `Buttons.All` plus every transform in Release. If it's over about 1 s, lower `MaxBfsX`, `MaxBfsY` or `MaxStates` and note the values.
8. Run the manual checks from the design's test plan with `dotnet run --project CalculatorPuzzle`.
9. Update the Status lines in all three spec files, add an "As built" section to the design (timing, any changed limits, anything that differs), and update `CLAUDE.md` (button table: the new left-side buttons; Status list: `LeftTransforms.cs`, `LeftSearch.cs`, this spec folder) and the descriptions in `docs/button-id.md` if behavior turned out different.

## Check the work

- `dotnet build` with no warnings.
- `dotnet test`: all tests pass, old and new.
- Manual: 41 → 84 with `X++:2, X*2:2` shows `X++ X*2` first; 12 → 97 with `×:2, 8:2, X++:2` shows `× 8 = X++`; division and modulo phases still run afterwards; Esc still stops the search.
