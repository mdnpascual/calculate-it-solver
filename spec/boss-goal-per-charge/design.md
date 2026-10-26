# Design: Boss rule 14, goal +1 per charge

**Status:** Implemented (2026-10-24). Tested in-game, Test L: L1–L3 confirmed; glass press untested.

## Overview

| Part | Change |
|------|--------|
| Mod (`GameHook.MelonLoader/CalcHookCore.cs`) | `bossRule` in `puzzle.json`; in a rule-14 round, one publish per round |
| `EngineeringCalculator.cs` (`Buttons`) | `:Ng` glass syntax in `Buttons.Parse`; `Buttons.Glass` |
| `GameLink.cs` | `GamePuzzle.BossRule` (int, 0 when absent); writes `:1g` for glass buttons |
| `GoalStepSearch.cs` (new, no UI) | the k = 1, 2, … loop over the three existing phases |
| `ResultLine.cs` | optional `Reaches` (the goal a line ends at), shown as `goal N` |
| `MainForm.cs` | Boss box, same-round rule-14 guard, runs `GoalStepSearch` for rule 14, status |

The three phase searches (`ArithmeticSearch`, `DivisionShortcut`, `ModuloSearch`) don't change. They are called with a different goal and cap and an `accept` filter, as `RequiredPlan` already does.

## Mod

`WritePuzzle`: add `"bossRule": <IsBossRound ? rule : 0>` next to `challenge` (same expression as `WriteJson` uses for `state.json`).

`CheckPuzzle`: the "new puzzle" test becomes

```csharp
bool goalMoves = _level.IsBossRound && _level.rule == GoalPerChargeRule;  // 14: each charged press raises the goal
if (!_hasPublished || round != _publishedRound || (!goalMoves && goal != _publishedGoal))
```

`OnRoundLoaded` (the `NextLvl` postfix) already sets `_pending = true`, so each new round still publishes once. `rule` is set in `NextLvl` before the first publish, which waits for the settle, so the first publish of the round uses the goal before any press. Add `const int GoalPerChargeRule = 14;` beside `MustUseRule`.

## Buttons: glass syntax

`Buttons.Parse`, the charge part (`token.Split(':')`): accept `^\d{1,9}g?$` instead of `^\d{1,9}$`. With `g`:

- the limit becomes `Math.Min(n, 1)` (glass breaks after one press),
- the button's charge key (the same key `Charges` uses, `Press.ChargeKey`: `X8`, `÷`, `10`, `c`, `q`, a transform token, …) goes into a new `HashSet<string> glass`, but only when the limit is above 0 (`:0g` leaves the button out like `:0`).

`=` takes no count, so `=:1g` is the same error as `=:1` today. Input is already lower-cased before the split, so `X8:1G` works too.

New property: `public IReadOnlySet<string> Glass { get; }` (empty when none). Nothing else reads it yet; `Fits`, `Drained` and `Spare` are unchanged (they already see a charge of 1).

## GameLink

- `GamePuzzle` gets `int BossRule = 0` (last parameter, so existing calls still compile). `Parse` reads `bossRule` when it is a number, else 0 (an older mod).
- Glass buttons are written with `g`: `X1:1g`, `÷:1g`, `c12:1g`. A glass button with 0 uses stays `X1:0` (no `g`; it is left out anyway). `GamePuzzle.Glass` stays as it is.
- Existing tests that change: `Parse_clamps_glass_to_one_charge` (`X1:1` → `X1:1g`), `Glass_division_fits_only_once` (`÷:1` → `÷:1g`), and `Glass_charge_key_has_one_press` in `MoreButtonsTests.cs` (`c12:1` → `c12:1g`). Check them with `dotnet test`.

## GoalStepSearch (new file `CalculatorPuzzle/GoalStepSearch.cs`)

```csharp
/// <summary>
/// Boss rule 14: each charged press raises the goal by 1, so a line with k charged presses must end at goal + k.
/// Tries k = 1, 2, … and keeps only lines with exactly k charged presses.
/// </summary>
public static class GoalStepSearch
{
    public const int GoalPerChargeRule = 14;

    /// <summary>Lines found per kind, the last k searched, and the division input error if any.</summary>
    public sealed record Result(int Arithmetic, int Division, int Modulo, int LastStep, string? DivisionError);

    /// <summary>Presses that spend a charge: every press except = and presses of a glass button.</summary>
    public static int Charged(IReadOnlyList<Press> presses, Buttons buttons) =>
        presses.Count(p => p.Key != Key.Equals && !buttons.Glass.Contains(p.ChargeKey));

    public static Result Search(long start, long goal, Buttons buttons,
                                RequiredPlan plan, string? required, Action<ResultLine> onLine,
                                CancellationToken cancel, int wanted, int maxSteps,
                                Action? onRejected = null);
}
```

Loop, for k = 1..maxSteps:

1. `target = goal + k`. Skip when `target == start`.
2. `cap = k + buttons.Glass.Count` (each glass button can be pressed once). `cap = Math.Min(cap, maxSteps + glassPresses)`.
3. `accept = p => Charged(p, buttons) == k && (plan.Accept?.Invoke(p) ?? true)`.
4. Phases, in order, each only when its `plan` flag is set:
   - `ArithmeticSearch.Find(start, target, buttons, cap, cancel, accept: accept, onlyOps: plan.OnlyOps)`.
   - `DivisionShortcut.Search(start, target, buttons, cap, l => { if (accept(l.Presses)) onLine(...); }, cancel, onRejected: _ => onRejected?.Invoke())`. Its error (no `÷` or no operand keys) doesn't change with k: remember it, report it in `Result.DivisionError`, and skip division for the next k.
   - `ModuloSearch.Find(start, target, buttons, cap, cancel, accept: accept)`. Its error (no `mod`) also stops modulo for the next k.
5. Each kept line goes to `onLine` as `ResultLine.From(line, buttons, required) with { Reaches = target }`.
6. After the whole k: stop when the total kept so far is at least `wanted`, or the token is cancelled (`ArithmeticSearch` and `ModuloSearch` throw `OperationCanceledException`; let it out, like the normal search does).

There is no `CostSlack` here: only lines of exactly k charged presses count at target G + k, so every phase gets the same cap.

`onLine` and `onRejected` can be called from the division worker threads, under the division search's lock. The caller's queue is a `ConcurrentQueue`, so that's fine.

Cost: each k runs the three phases with a small cap, so the early k are fast. The total is at most about the sum of the normal search at caps 1..12. The real limit is `wanted`: most puzzles have 10 lines by k = 4–6.

## ResultLine

Add `long? Reaches = null` as the last record parameter. `ToString` adds `, goal {Reaches}` right after `(N presses` when it is set. `Ranking` is unchanged.

## MainForm

- New `bossBox` (`TextBox`, width 120), row **Boss** between Last and Buttons. `TextChanged` → `MarkDivisionStale`; Enter → `StartDivisionSearch`, like the others.
- `ApplyGamePuzzle`: `bossBox.Text = puzzle.BossRule == 0 ? "" : puzzle.BossRule.ToString()`. Glass comes in through the Buttons text (`X8:1g`), so the form keeps no glass state.
- **Same-round guard** in `OnGameFileChanged`, after the seq/time check (so `lastSeq` / `lastTime` are still updated and the file isn't re-read in a loop):

  ```csharp
  // Rule 14 raises the goal on every press; the round's first puzzle is the one to solve.
  if (puzzle.BossRule == GoalStepSearch.GoalPerChargeRule && appliedStepRound == puzzle.Round)
      return;
  appliedStepRound = puzzle.BossRule == GoalStepSearch.GoalPerChargeRule ? puzzle.Round : null;
  ApplyGamePuzzle(puzzle);
  ```

  `int? appliedStepRound` is a new field. Set it to null where `lastSeq = -1` is set today (Lock unchecked, `SetFollowing(true)`), so those still re-read and apply the file.
- `StartDivisionSearch`:
  - Boss box: empty → 0; otherwise a whole number 0–99, else status `Enter a boss rule number, or leave Boss empty.` and return.
  - Rules 2, 6, 9, 12: add `boss rule N not supported` to `requiredNote` (shown in the status).
  - Rule 14: `searchStepped = true`; start `stepTask = Task.Run(() => GoalStepSearch.Search(start, goal, buttons, plan, required, queue.Enqueue, token, MaxShownLines, MaxPresses, () => Interlocked.Increment(ref rejectedLines)))`, the queue and timer as now, and skip the normal `searchTask`.
  - Every other value: the normal search, unchanged.
- `OnSearchTick`: when `stepTask != null`, drain; when it is done, set the counts from its result and show `Rule 14: goal {G} + k, k = 1..{LastStep}: ` + `DoneStatus(...)`. A faulted task shows its message (cancel isn't a fault here: Esc stops the timer first). With no lines: `No line fits the available buttons for goal {G} + k, k = 1..{LastStep}.`
- Esc and `StopDivisionSearch` also check and clear `stepTask`.
- Search counts keep the same names (`arithmeticCount`, `divisionCount`); add `moduloCount` as a field for the stepped status.

## Tests (`CalculatorPuzzle.Tests/GoalStepTests.cs`)

Buttons `÷:9, 10:9, X8:9, =` unless said otherwise.

Glass syntax (`ChargesTests.cs` or a new `GlassSyntaxTests.cs`):

- `X8:1g` → `Glass` = {`X8`}, charge 1. `X8:4g` → charge 1. `X8:0g` → left out, not in `Glass`. `X8:1G` works.
- `X8:g`, `X8:1x`, `X8:1gg` → `Bad charge count`. `=:1g` → same error as `=:1`.
- `÷:1g`, `c12:1g`, `sqr:1g` → `Glass` holds `÷`, `c`, `sqr` (the charge keys).
- `GameLink.Parse` of a glass button gives `X1:1g`; with 0 uses `X1:0`.

GoalStepSearch:

1. `Charged`: `÷ 10 = X8` is 3; with `X8:1g` it's 2; `=` never counts.
2. S 41, G 45: first kept line is `÷ 10 = X8` with `Reaches` 48; no line at k = 1 or 2; `LastStep` ≥ 3.
3. S 41, G 46, Buttons `÷:9, 10:9, X8:1g, =`: `÷ 10 = X8` kept at k = 2 with `Reaches` 48.
4. S 41, G 46, no glass: `÷ 10 = X8` is never reported with `Reaches` 48.
5. Every reported line: simulating its presses (`EngineeringCalculator.Run`) ends at `Reaches`, and `Reaches == G + Charged`.
6. Stops at `wanted`: with a rich button set, `wanted = 3` returns once ≥ 3 lines are kept and `LastStep` is the k where that happened.
7. Cancelled token: throws `OperationCanceledException` (or returns) without reporting more lines.
8. `target == start` is skipped: S 47, G 45, k = 2 adds no line and doesn't throw.
9. `GameLink.Parse`: `bossRule` 14 → `BossRule` 14; missing → 0.
10. `ResultLine.ToString` with `Reaches` shows `goal 48`.

The same-round guard lives in `MainForm` (no UI tests in this repo); it is checked by Test L3.

## Manual test

Test L (`user-verification/test-l.md`), challenge 4 (every round is a boss), until the rule-14 text shows:

- L1: `puzzle.json` has `"bossRule": 14`, the app's Boss box shows 14.
- L2: lines show `goal N`; pressing the top line raises the in-game goal by 1 per charged press and ends on it (answers the open question).
- L3: `seq` in `puzzle.json` doesn't change while the line is pressed (mod guard), and the Lines box stays. Then the next round is solved as usual (the app guard lets a new round through).
- L4 (if a glass button shows up): the Buttons box shows it as `:1g`, and a glass press doesn't raise the goal.
