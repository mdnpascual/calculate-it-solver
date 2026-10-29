# Design: coinsub button (fallback loop when no line is found)

**Status:** Complete 2026-10-28.

Read `requirements.md` first. Implement after `spec/blind-passive-rule-14/` (the loop carries `LineContext.Blind` from one n to the next).

## Overview

| Part | Change |
|------|--------|
| `EngineeringCalculator.cs` | `Key.CoinSub`; `Buttons` parses `coinsub`; `Run` simulates it (money and display); `LineContext.ChargedBefore` |
| `GameLink.cs`, `GameHook.MelonLoader/ButtonSymbols.cs` | `operator_coinsub` → `coinsub` |
| `ButtonLabels.cs` (and its mirror in `ButtonSymbols`) | label `$++`; `Line` groups a run of coinsub presses as `$++ (n press)` |
| `CoinSubSearch.cs` (new, no UI) | the state after n presses, wrapping a rest line |
| `MainForm.cs` | after a search with no line, rerun it from the state after n + 1 presses; status text |

The phase searches don't change. Each loop reruns the normal search from a new start, goal and button context, like the swap leg runs from F.

## Simulator (`EngineeringCalculator.cs`)

- `Key.CoinSub`, added before `Equals`. `Press.ToString()` → `coinsub`. `ChargeKey` → `coinsub`.
- `Buttons`: token `coinsub` with a charge count like other left buttons (glass allowed). `Buttons.Has(Key.CoinSub)` when the charge is above 0.
- `Run`, a new case:

  ```csharp
  case Key.CoinSub when op == null && (buttons == null || buttons.Has(Key.CoinSub)):
      // MainCalc.UseCoins(-1): only with coins left after SubUse's drains on this press (rule 10 takes one first); PlusNumber(1) reads the number.
      if (money > 0)
      {
          money--;
          display = Math.Min(long.Parse(display) + 1, Max).ToString();
      }
      log.Add(display);
      break;
  ```

  The charged-press block above the switch already runs first (rule-10 coin, `DrainFromThird`, goal raise, glass, blind), as `SubUse` does in the game. So in rule 10 a press with $1 drains it and then does nothing, which gives the "$2 or nothing" rule without a special case.
- `LineContext` gets `int ChargedBefore = 0`: charged presses already made this round. `Run` starts `charged` at it, so `DrainFromThird` counts from the round start. Only the loop sets it.

## Labels and mapping

- `GameLink.MapButton`: `"operator_coinsub" => "coinsub"`. `ButtonSymbols` gets the same line (the linked mirror test checks it). Remove it from the unmapped list in `ButtonSymbolsTests`.
- `ButtonLabels`: `coinsub` ↔ `$++` (both directions, so the Buttons and Required boxes read either). Mirror it in `ButtonSymbols` for the overlays. `$++` must not clash with `money`'s `$`: the reverse lookup matches the whole text, so `$++:5` → `coinsub:5` and `$:2` → `money:2`. Add a test for both.
- `ButtonLabels.Line`: consecutive `coinsub` presses become one item `$++ (n press)` (n ≥ 1, so one press shows `$++ (1 press)`). Other presses are unchanged.

## `CoinSubSearch.cs`

```csharp
/// <summary>
/// The coinsub loop: when no line is found, press coinsub once more (−1 coin, +1 display; −2 coins in boss rule 10) and search again
/// from the new state. The presses are outside the 12-press cap.
/// </summary>
public static class CoinSubSearch
{
    public const string Token = "coinsub";

    /// <summary>The state after n coinsub presses from S: the display; the goal the game shows (G + the presses that raised it, rule 14);
    /// the buttons the rest runs with: no coinsub, context Goal raised, Money = coins left, ChargedBefore = n, Blind cleared if a press used it.</summary>
    public sealed record State(int N, long Start, long Goal, Buttons Rest);

    /// <summary>The state after n presses, or null when the n-th press can't be made or adds nothing:
    /// no charge left for it, the display didn't rise (no coin left after its drains, or 999 999), or the presses don't fit the charges.</summary>
    public static State? After(long start, long goal, int n, Buttons buttons, int bossRule);

    /// <summary>A rest line with the n presses in front: cost + n, drained / spare / Missing on the whole line, Reaches kept.
    /// Null when the whole line doesn't fit the charges or the simulator, run from S over the whole line, doesn't end at Reaches.</summary>
    public static ResultLine? Wrap(ResultLine inner, State state, long start, Buttons buttons, string? required, int bossRule);

    /// <summary>The line that is only the n presses, when State.Start is already the goal (not in rule 12: there the rest search adds the bare =).</summary>
    public static ResultLine? Alone(State state, long start, Buttons buttons, string? required);
}
```

```csharp
/// <summary>Coins the next coinsub press needs to add 1: $1, + 1 in boss rule 10, + 1 when the diff_type ≥ 7 drain applies
/// to it (chargedSoFar + 1 ≥ 3). Glass presses still drain (the drains run for glass too).</summary>
public static int Needed(MoneyRules? rules, int chargedSoFar);
```

The money stop is explicit: before press n + 1, `TryNextCoinPress` checks `state.Rest.Context.Money >= Needed(...)` (money < $1 outside rule 10, < $2 in rule 10) and stops the loop when it fails, without starting a search. `After` checks the same before each press it simulates.

`After` runs `n` coinsub presses through `EngineeringCalculator.Run` with the full buttons and context and reads the end display, money, goal raises and blind flag. It returns null if the display after press n isn't 1 higher than after press n − 1 (the press did nothing). For n = 0 it returns the start state unchanged (`Rest` = buttons without coinsub).

`Wrap` follows `SwapSearch.Wrap`, and also re-runs the whole line from S in the simulator as a guard against a wrong context in the rest search.

In rule 14 the rest search is `GoalStepSearch.Search(state.Start, state.Goal, state.Rest, …)`: its k loop starts from the goal the game shows after the n presses. The phases get the usual `MaxPresses` cap; the n presses are not part of it.

## MainForm

- A field `int coinPresses` (0 = today's search). Enter, Follow game and the checkboxes start at 0.
- `StartDivisionSearch` gets the state from `CoinSubSearch.After(start, goal, coinPresses, buttons, bossRule)` and searches from `state.Start`, `state.Goal`, `state.Rest` in place of S, G and buttons. For `coinPresses` ≥ 1: the direct leg only (no swap leg, no `SwapAlone`), every line goes through `CoinSubSearch.Wrap` before the queue (null counts as rejected), and `CoinSubSearch.Alone` is added when `state.Start` is the target. For 0 it behaves as today (wrapping is the identity).
- **Next loop.** The places that end with no line (`OnSearchTick`, `OnModuloDone`, `OnStepTick`; also the boss branch that shows `DoneStatus` when `found.Count == 0`) first call `bool TryNextCoinPress()`. It returns true and restarts the search with `coinPresses + 1` when: `found.Count == 0`, the search wasn't cancelled or faulted, no achievement mode, not `searchSameNumber`, the buttons have `coinsub`, the money left is at least `CoinSubSearch.Needed(…)` ($1, or $2 in boss rule 10), and `CoinSubSearch.After(…, coinPresses + 1, …)` isn't null. `rejectedLines` is reset per loop like any search.
- **Status.** One helper for the searching text: `coinPresses > 0 ? $"Searching… $++ ({coinPresses} press). Esc to stop." : $"Searching… {found.Count} line(s) so far. Esc to stop."`, used by every tick that prints the searching text. The end status is the normal `DoneStatus` / `NoLineStatus`. No new `DoneStatus` part.
- Esc cancels the current loop and doesn't start the next one.

Speed: each loop is a full search (41 → 483 with every button is about 1 s Release), at most one per coinsub charge (and coin). It runs only when n = 0 found nothing.

## Tests (`CalculatorPuzzle.Tests/CoinSubTests.cs`)

1. Parse: `coinsub:5`, `$++:5`, `coinsub:1g`; `$:2` still parses as `money`; `GameLink` maps `operator_coinsub`; label round trip.
2. `Run`: `coinsub` with money 2 → display + 1, money 1; money 0 → display unchanged (the press is valid, the charge used); on the right side → null; at 999 999 stays 999 999; `04` → `5`.
3. `Run`, rule 10 (`MoneyRules(Rule10: true)`): money 5 → 3 per press; money 1 → 0 and the display doesn't change.
4. `After`: n = 3 from 41, money 5 → Start 44, Rest money 2, no coinsub in Rest, ChargedBefore 3; money 2, n = 3 → null; charges 2, n = 3 → null; rule 10, money 5, n = 3 → null, money 6 → Start 44.
4b. `Needed`: $1 normally; $2 in rule 10; $2 for the 3rd charged press at `diff_type` ≥ 7; $3 for both. The loop stops at money $0 (normal) and $1 (rule 10) without starting a search.
5. `After` in rule 14: n = 2 from 41, G 45 → Start 43, Goal 47; with Blind → Goal 46 and Rest without Blind; with glass coinsub (n = 1) → Goal 45.
6. Requirements examples: 39 → 48 with `÷:1, 10:1, X8:1, coinsub:5`: the line `$++ (1 press) | / | 10 | = | X8`, 5 presses; 41 → 44 with `coinsub:5`: `$++ (3 press)`.
7. Outside the cap: a rest line of 12 presses after n = 2 is kept (14 presses in all).
8. `ButtonLabels.Line` groups `coinsub coinsub ÷` as `$++ (2 press) | /`.

## Manual test: Test AA

`user-verification/test-aa.md` (take the next free letter if AA is used):

- AA1: own `operator_coinsub`; the overlay and the app show `$++` with its charges. Note what the in-game button face shows.
- AA2: a round where the app finds no line but coinsub has charges: the app shows `Searching… $++ (n press)`, then a `$++ (n press) | …` line. Play it; the round is won; coins drop by n.
- AA3: press coinsub at $0: the display doesn't change and the charge drops.
- AA4 (if met, boss rule 10): one press costs $2; with $1 it takes the coin and the display doesn't change.
- AA5 (if met, boss rule 14): a `$++ (n press)` line wins; the goal rises on each coinsub press.
