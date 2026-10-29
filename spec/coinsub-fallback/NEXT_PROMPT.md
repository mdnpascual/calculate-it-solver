# Next prompt: coinsub button (fallback loop when no line is found)

Implement `spec/coinsub-fallback/`. `operator_coinsub` (token `coinsub`, label `$++`) pays 1 coin ($2 in boss rule 10) to add 1 to the display; with too few coins it does nothing but still uses its charge. When the normal search finds no line, the app loops: press coinsub once more, search again from the new state (display, money, rule-14 goal, blind flag), until a line is found, coinsub has no charges, or money is below $1 (below $2 in boss rule 10). The coinsub presses are outside the 12-press cap. Keep the line and status text short: `$++ ×n` in the line, `Searching… $++ ×n. Esc to stop.` while it runs, the normal end status after.

Do `spec/blind-passive-rule-14/` first (the loop carries `LineContext.Blind`).

## Read first

1. `spec/coinsub-fallback/requirements.md` and `design.md`.
2. `CalculatorPuzzle/README.md` ("Files", "Search order and truncation").
3. Code: `EngineeringCalculator.cs` (`Key`, `Press`, `Buttons.Parse`, `Run`'s charged-press block, `LineContext`, `MoneyRules`), `SwapSearch.cs` (`Legs`, `Wrap`: the pattern to copy), `ButtonLabels.cs`, `GameLink.MapButton`, `MainForm.cs` (`StartDivisionSearch`, `OnSearchTick`, `OnModuloDone`, `OnStepTick`, `NoLineStatus`, `DoneStatus`).
4. `spec/near-median-sup-money-equal-mw/requirements.md` ("Money moves during a line") for how the simulator tracks money.
5. Mod: `GameHook.MelonLoader/ButtonSymbols.cs` (mirror of `MapButton`).

## Steps

1. `Key.CoinSub`, `Buttons` parsing, the `Run` case and `LineContext.ChargedBefore`; tests 1–3.
2. `GameLink.MapButton`, `ButtonSymbols`, `ButtonLabels` (label `$++`, mirrored in `ButtonSymbols`, and the `$++ ×n` grouping in `Line`); tests 1 and 8, and update `ButtonSymbolsTests`.
3. `CoinSubSearch.cs` (`State`, `After`, `Wrap`, `Alone`); tests 4–7.
4. `MainForm`: `coinPresses`, the search from `CoinSubSearch.State`, `TryNextCoinPress()` at the no-line ends, the searching-status helper, Esc.
5. Build the mod: `dotnet build GameHook.MelonLoader -c Release`.
6. Docs: `CalculatorPuzzle/README.md` (new file, changed files), `CLAUDE.md` engineering table (a `coinsub` row: left side only, −1 coin +1 display, $2 in rule 10, nothing without the coins; and `coinsub` → `$++` in the Labels line), `docs/button-id.md` (`operator_coinsub` note), the `spec/README.md` line.
7. Add Test AA as `user-verification/test-aa.md` plus a row in `user-verification/README.md` (steps in the design; if AA is taken, the next free letter).

## Check

- `dotnet build` and `dotnet test`: every existing test passes, plus `CoinSubTests.cs`.
- App, by hand: S 39, G 48, Money 5, Buttons `÷:1, 10:1, X8:1, coinsub:5, =` → status shows `Searching… $++ ×1. Esc to stop.`, then the line `$++ ×1 | / | 10 | = | X8   (5 presses, …)` and a normal end status. Money 0 → `No line fits the available buttons.` Boss 10 with Money 1 → no line.
- Release timing for 41 → 483 with every button: unchanged (the loop doesn't run when lines are found).
