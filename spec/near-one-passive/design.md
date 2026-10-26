# Near-one passive: design

Read `requirements.md` first.

## Mod (`GameHook.MelonLoader/CalcHookCore.cs`)

Read-only, as always: one `GameObject.Find`, no writes to the game.

- `bool Near1() => GameObject.Find("passive_near1") != null;` (the game's own test).
- `CheckPuzzle`: read `bool near1 = Near1();` each frame. If `_hasPublished && near1 != _publishedNear1`, set `_pending = true` (the warp pattern, `_warps` / `_pending`). Add `|near1:` + `0`/`1` to the fingerprint. On publish, store `_publishedNear1 = near1`.
- `WritePuzzle` and the `state.json` writer: add `"near1": true|false` next to `bossRule` / `warp`. Add `near1` to the log line when true.
- `GameHook.MelonLoader/README.md`: document the field.

## App

### `GameLink.cs`

- `GamePuzzle` gets `bool Near1 = false` (last parameter, so existing calls still compile).
- `Parse`: `root.TryGetProperty("near1", out var n) && n.ValueKind == JsonValueKind.True`.

### `NearGoals` (new static helper, in `EngineeringCalculator.cs` next to `Sent`, or its own `NearGoals.cs`)

```csharp
public const string Near1Passive = "passive_near1";
/// <summary>The goals a line may end at: G alone, or G − 1, G, G + 1 with the near-one passive. Below 0 (below 1 with plusOne) dropped.</summary>
public static IReadOnlyList<long> Targets(long goal, bool near1, bool plusOne)
```

The order is G, G − 1, G + 1. The order doesn't change the ranking. It only decides which lines stream in first.

### `MainForm.cs`

- A `Near 1` checkbox on the Required / Last / Boss row (`AddInlineRow`). `ApplyGamePuzzle` sets it from `puzzle.Near1`; changing it calls `MarkDivisionStale()`, like the Boss box.
- `searchNear1` field. `searchSameNumber` stays `start == goal && !plusOne`: S == G is already solved, and the mod doesn't publish it.
- **Bare `=` line (rule 5).** When Near 1 is on, not `plusOne`, and |S − G| == 1, enqueue the 0-press arithmetic line `=` with `Reaches = S`. Easiest: `ArithmeticSearch.Find` gets an optional `bareSend` flag, and when `!plusOne && start == goal && bareSend` it keeps `[=]` the way the `plusOne && start == t` branch does. Then calling `Find(start, target = S, …, bareSend: true)` gives it, and target S needs no other phase.
- **Phase loop.** In the `searchTask` lambda, loop the targets inside each phase (phase-major), so the arithmetic lines of every target set `divisionCap` before the two-operator phase runs:

```
targets = NearGoals.Targets(goal, near1, plusOne)
foreach t in targets: arithmetic(t) → enqueue (Reaches = t != goal ? t : null), update divisionCap
foreach t in targets: two-operator(t, divisionCap) → same
foreach t in targets: division(t, divisionCap)        // returns the first error; the error doesn't depend on t
```

  `OnSearchTick` → the modulo task does the same loop over the targets with the shared cap. `OnModuloDone` merges the lines. Skip a target equal to S in every phase (the phases return nothing for it anyway, except the bare `=` above).
- `ResultLine.From(...) with { Reaches = t }` only when t ≠ G, so exact lines look as they do today.
- `DoneStatus`: prefix `Near 1: goal ± 1: ` when `searchNear1`. The "no line" texts say `G ± 1` when Near 1 is on.
- `OnGameFileChanged`: the rule-14 same-round guard also lets the file through when `puzzle.Near1 != appliedStepNear1` (store it next to `appliedStepWarp`).

### `GoalStepSearch.cs`

`Search(..., bool near1 = false)`. For each k, loop `NearGoals.Targets(goal + k, near1, plusOne: false)` (rule 14 never runs with rule 12), skip t == start, run the four phases for each t with the same `accept` (exactly k charged presses) and `Reaches = t`. The `wanted` check stays after each k, over all targets.

### `ResultLine.cs`

No change to `Ranking`. `Reaches` already prints `goal N`. Update the comment on `Reaches`: "the goal the line ends at when it differs from G (boss rule 14, or the near-one passive)".

### Not changed

`ArithmeticSearch` (apart from `bareSend`), `TwoOperatorSearch`, `DivisionShortcut`, `ModuloSearch`, `EngineeringCalculator.Run`: each already takes one goal; the solver just calls it up to three times.

## Cost

Up to 3× the search time with Near 1 on. Division is the heavy phase (41 → 483 with every button about 1 s Release). The shared cap limits the extra targets. If it gets too slow, a follow-up can run the division targets in one `WorkerPool` pass.

## Tests (`CalculatorPuzzle.Tests/NearOneTests.cs`, new)

| Test | Checks |
|------|--------|
| `Targets_basic` | `Targets(48, true, false)` = {48, 47, 49}; `Targets(48, false, false)` = {48} |
| `Targets_zero` | `Targets(0, true, false)` = {0, 1}; `Targets(1, true, true)` = {1, 2} |
| `BareSend_when_start_is_one_off` | `ArithmeticSearch.Find(49, 49, …, bareSend: true)` gives `[=]` at cost 0 |
| `Near_line_is_found` | S 41, G 48, `X7:1, ÷:1, 10:1` (no `X8`): with the near-one targets, `÷ 10 = X7` reaches 47 |
| `Ranking_unchanged` | a 2-press `goal 51` line ranks above a 3-press exact line, and equal-cost lines keep the drained / spare order |
| `GoalStep_near1` | rule 14 with near1: a line with k charged presses ending at G + k − 1 is kept; one ending at G + k − 2 isn't |
| `PlusOne_near1` | rule 12 with near1: a line whose `Sent` is G + 1 is kept |
| `GameLink_near1` | `"near1": true` parses to `Near1 = true`; missing gives false |

The phase loop in `MainForm` has no unit test (the UI code has none today); Test U covers it.

## Docs to update when implemented

- `CalculatorPuzzle/README.md`: "Search order" (targets), `GameLink.cs`, `MainForm.cs`, `GoalStepSearch.cs`, `ResultLine.cs` notes.
- `docs/boss-rules.md` row 13: "`passive_near1` is published; the one-`=` filter is unchanged".
- `spec/README.md`: status line.

## As built (2026-10-25)

- Implemented as designed. `NearGoals` is its own file. The Near 1 checkbox sits in the Game row (the Required / Last / Boss row helper only takes text boxes).
- `ArithmeticSearch.Find(..., bareSend)` builds the bare `=` line directly, because the simulator rejects a bare `=` without rule 12. It applies `accept`, `onlyOps` and `Fits`.
- In rule 14 the bare line can't occur (k ≥ 1 charged presses), so `GoalStepSearch` skips a target equal to S.
- With S == G (`searchSameNumber`) no search runs, near-one targets included.
- `moduloTask` now returns the finished `ResultLine`s with `Reaches` set.
- Tests: `NearOneTests.cs`, 8 cases (636 in the suite).
- Release timing, `41 → 483` with the default Buttons box (every button, 9 charges): Near 1 off 380 ms, Near 1 on 652 ms (all four phases, from a test harness that mirrors the `MainForm` loop).
