# Next prompt: blind passive and the empty count (correctness)

Implement `spec/blind-empty-count/`. With `passive_blind` the round's first non-glass press is free, but `Buttons.Drained` still counts it, so a line like `p` on `p:1` shows `empty 1` and ranks below lines that really empty nothing.

## Read first

1. `spec/blind-empty-count/requirements.md` and `design.md`.
2. `spec/blind-passive-rule-14/requirements.md` (the free press, game facts).
3. Code: `Buttons.Drained` / `Spare` / `Fits` in `CalculatorPuzzle/EngineeringCalculator.cs`, `ResultLine.cs` (`Ranking`, `From`), `SwapSearch.Wrap` / `Flip`, `CoinSubSearch.Wrap`, and where `MainForm` calls them.
4. `CalculatorPuzzle.Tests/BlindTests.cs`, `CoinSubTests.cs`.
5. `CalculatorPuzzle/README.md` ("Files").

## Steps

1. Change `Drained` as in the design; tests 1–5.
2. Check the `buttons` that `SwapSearch.Wrap` and `CoinSubSearch.Wrap` pass to `Drained` (round-start, Blind on); fix if not; tests 6–7.
3. Test 8, then the docs, `user-verification/test-ac.md` and its index row.

## Check

- `dotnet build` and `dotnet test`: everything passes.
- App, by hand: Blind ticked, a Buttons box where a 1-charge button solves the puzzle: no `empty` on that line. Untick Blind: `empty 1`.
