# Design: Achievement modes (School mathematics, Just a one)

**Status:** Done (2026-10-25). Release times (S 41, G 48): test 3 (keys `0`–`9`, all `:9`) about 4 s, test 4 (`10`, `X8`) about 30 ms. Deviations: Just a one adds a plain `edits, mod, D, =` phase (`ModLines`), since `ModuloSearch` only prepends digits and can't give `X5 mod 10 =`; the Achievements header and panel share one row container; `TwoOperatorSearch.Operands` / `LeftSearch.Actions` became `internal`. Done: Test T confirmed except T3 (untested).

## Overview

| Part | Change |
|------|--------|
| `AchievementMode.cs` (new) | `enum AchievementMode { None, School, JustOne }`, `Credit` (which `operators_used` entry a Just-a-one line sets), `FourOps` (did a line press `+ - × ÷`, each with `=`) |
| `SchoolSearch.cs` (new) | n-operator search for School mathematics: best-first over (display, operators-used mask), up to 12 operator segments, closing segment by formula |
| `JustOneSearch.cs` (new) | Just a one: runs arithmetic, division and modulo for S + 1 with a one-`=`-at-the-end filter; credit per line; diversity ranking |
| `ResultLine.cs` | `School` line kind (`SchoolSearch.Line?`), `Credit` (string, shown as `[÷]`) |
| `MainForm.cs` | the Achievements section (header button, panel, two radio buttons), mode dispatch in `StartDivisionSearch`, status texts |

No mod change. No change to the existing phases (`ArithmeticSearch`, `DivisionShortcut`, `ModuloSearch`, `TwoOperatorSearch`): Just a one only calls them with a different goal and an `accept` filter they already take.

## AchievementMode.cs

```csharp
public enum AchievementMode { None, School, JustOne }

public static class Achievements
{
    /// <summary>Max operator segments in a School mathematics line.</summary>
    public const int MaxSchoolOperators = 12;

    /// <summary>The four operators School mathematics needs, as mask bits: + 1, × 2, - 4, ÷ 8 (OperationsCounter order).</summary>
    public static int OpBit(Key op) => op switch { Key.Add => 1, Key.Multiply => 2, Key.Subtract => 4, Key.Divide => 8, _ => 0 };
    public const int AllFour = 15;

    /// <summary>True when every one of + - × ÷ is applied by an = in the line.</summary>
    public static bool FourOps(IReadOnlyList<Press> presses);

    /// <summary>
    /// The operators_used entry a line's last = sets (game LastButId): the last charged press before that = whose game
    /// button has an Infos id. Null when there is none (no charged press at all).
    /// </summary>
    public static string? Credit(IReadOnlyList<Press> presses);
}
```

Use the real `Key` names from `EngineeringCalculator.cs` (`Key.Add`, `Key.Subtract`, `Key.Multiply`, the `÷` key, `Key.Mod`, `%`, `Key.Digit`, `Key.Ten`, `Key.Charge`, `Key.Quest`).

`FourOps`: walk the presses, remember the pending operator, OR its bit into a mask on each `=` that applies it (`op != null && operand typed`), return `mask == AllFour`. Remorph keys have no `=` and add nothing.

`Credit`: find the last `=`; walk back from it; the first press that is charged and has an id decides:

| Press | Credit |
|-------|--------|
| `Key.Digit` 1–9 | `digit` (id 0) |
| `Key.Digit` 0, `Key.Ten` | no id: keep walking back |
| `Key.Charge` | `c` (id 32) |
| `Key.Quest` | `q` (id 21) |
| `+` `-` `×` `÷` `mod` `%` | `+` `-` `×` `÷` `mod` `%` (ids 1, 2, 3, 4, 13, 34) |

Left-side presses come before the operator, so the walk always stops at the operator at the latest. Keep the ids in a comment table (from the scene scan) so a later mod spec can match them to `operators_used`.

## SchoolSearch.cs

```csharp
public static class SchoolSearch
{
    public sealed record Line(IReadOnlyList<Press> Presses, int Cost, int Operators);

    public static IReadOnlyList<Line> Find(long start, long goal, Buttons buttons, int maxCost,
        CancellationToken cancel, Action<Line>? onLine = null, Action? onRejected = null, int maxLines = 100);
}
```

### Preconditions

Return no lines and set an error the form shows (`School mathematics needs + - × ÷ (missing: …)`) when any of the four operators has 0 charges, or when there are no operand keys. (Use a `Result` record like `ModuloSearch.Result` if that reads better: `Lines`, `Error`.)

### Search

A best-first search (Dijkstra on press cost) over states:

```
State = (display string, mask of + × - ÷ used, segments used, pending: none)
```

The display is a string (like `LeftSearch`), so leading zeros from `Xd` / digit maps stay. Edges:

1. **Left press**: every left-side button the puzzle has (`dX`, `Xd`, `flip`, `reset`, the transforms, `last`, `100-X`, `25|69`), through the same move function `LeftSearch` uses (`Xd` / `dX` only below 6 characters). Cost 1.
2. **Segment** `op D =` for `op` in `+ - × ÷ mod %`, with `D` from the small operand list: every operand of up to 2 operand keys (reuse `TwoOperatorSearch.Operands` with a max of 2, or move it to a shared place). Cost 1 + keys. The new display is `EngineeringCalculator.Apply` (clamps included); skip `÷ 0` / `mod 0`. Mask gets `OpBit(op)`. Segments + 1 must be ≤ `MaxSchoolOperators`.
3. **Closing segment by formula** (so the last operand isn't limited to 2 keys): from state `(x, mask)` with `mask | OpBit(op) == AllFour` for some `op` in `+ - × ÷`, and each `(y, post)` from `ArithmeticSearch.BuildYs` for G: the operand is fixed: `y − x` for `+`, `x − y` for `-`, `y / x` when exact for `×`, and for `÷` the range `[floor(x / (y+1)) + 1, floor(x / y)]` (`y ≥ 1`) through `DivisionShortcut.CheapestDivisor`. Type it with `DivisionShortcut.TypeOperand` (null → skip). This gives a full line: path + `op D =` + `post`. It goes to the candidate list, not back into the queue.
4. **Goal**: a popped state with `mask == AllFour` and display == G's text (after left presses) is a full line too.

Bounds:

- Display value ≤ 999 999 (the game clamps; `Apply` does it).
- State cap: 300 000 settled states (like `LeftSearch.DefaultLimits`); stop expanding past it.
- Cost cap: `maxCost` (form passes 30: 12 segments at ≥ 2 presses is 24, plus edits). Once the first line is found at cost `c`, stop popping states above `c + 2` (the form's `CostSlack`).
- `cancel` checked every 1 000 pops.

### Charges

Dijkstra ignores charges in the state (a charge vector in the key would blow up). To keep good lines when charges are tight:

- **Labels**: each state keeps up to 4 labels (cost + uses per button, as a small `int[]` indexed by button), not one. A new label is dropped when an existing one has cost ≤ and every use ≤ (dominated). A label whose uses go over a button's charges is dropped right away (so `× 1 = ÷ 1 =` stops once `1` runs out).
- Every full line is replayed with `EngineeringCalculator.Run` (must end at G), checked with `Buttons.Fits` and `Achievements.FourOps`; a failing line calls `onRejected`. The `q` key: at most one press per line (as everywhere).

### Output

Lines sorted by cost, then by the existing `ResultLine.Ranking` (drained, spare); dedupe by press text; up to `maxLines`. `onLine` streams each line as it is accepted (the form's queue).

### Why the filler pairs come for free

`× 1 =`, `÷ 1 =`, `+ 0 =`, `- 0 =`, and the pairs `× 10 = ÷ 10 =`, `+ 10 = - 10 =` are ordinary segments that return to the same display with more mask bits. Dijkstra finds them without special code. The state includes the mask, so the same display with a bigger mask is a different state.

## JustOneSearch.cs

```csharp
public static class JustOneSearch
{
    public static bool OneEqualsAtEnd(IReadOnlyList<Press> presses) =>
        presses.Count > 0 && presses[^1].Key == Key.Equals && EngineeringCalculator.OneSend(presses);

    /// <summary>Lines from S whose only = (the last press) gives S + 1. Streams through onLine.</summary>
    public static Result Search(long start, Buttons buttons, int maxCost, Action<ResultLine> onLine,
        CancellationToken cancel, Action onRejected);

    /// <summary>Cheapest line of each credit first (by cost), then the rest by ResultLine.Ranking.</summary>
    public static IComparer<ResultLine> Ranking { get; }
}
```

Steps (all with `goal = start + 1`, `accept: OneEqualsAtEnd`):

1. `start >= EngineeringCalculator.Max` → error `Just a one can't go above 999 999.`
2. `ArithmeticSearch.Find(start, start + 1, buttons, maxCost, cancel, accept: OneEqualsAtEnd)`. Edits-only and remorph lines fail the filter (no `=`). Lines that end with edits after `=` fail it too (last press isn't `=`).
3. `DivisionShortcut.Search(start, start + 1, …)` with the same filter in the callback (as `MainForm` does with `plan.Accept`). Cap: the cheapest arithmetic line + `CostSlack`, or `maxCost`.
4. `ModuloSearch.Find(start, start + 1, …, accept: OneEqualsAtEnd)` (modulo lines already end with `=`).
5. Each line → `ResultLine.From(...)` with `Credit = Achievements.Credit(line.Presses)`. A line with no credit (can't happen with an operator, but guard) is dropped.

`Result`: counts per phase and the list of credits found (cheapest first) for the status.

`Ranking`: split the list into "first line of each credit" (the cheapest per credit by `ResultLine.Ranking`) and the rest. Show the first group sorted by `ResultLine.Ranking`, then the rest sorted by `ResultLine.Ranking`. `MainForm.Redraw` uses this comparer in Just-a-one mode. (A comparer alone can't express "first per group": implement it as a `Order(List<ResultLine>)` function and call it from `Redraw` instead of `found.Sort`.)

Phases run in one background task (like the rule-14 `stepTask`), so `MainForm` handles it with one task and the usual queue and timer.

## ResultLine.cs

- New optional fields: `SchoolSearch.Line? School = null`, `string? Credit = null`.
- `From(SchoolSearch.Line, Buttons)`.
- `ToString`: when `Credit != null`, append ` [credit]` after the count (`÷` shows as `÷`, like the other display rules; `/` is only for the presses).
- `Ranking`: a School line ranks with cost, drained, spare like the others; it never meets the other kinds (one mode at a time).

## MainForm.cs

### UI

```csharp
readonly Button achievementsHeader = new() { Text = "▸ Achievements", AutoSize = true, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft };
readonly FlowLayoutPanel achievementsPanel = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, Visible = false, Margin = Padding.Empty };
readonly RadioButton schoolRadio = new() { Text = "(+, -, /, *) solve (School mathematics)", AutoSize = true, AutoCheck = false };
readonly RadioButton justOneRadio = new() { Text = "+1 with 4 ops (Just a one)", AutoSize = true, AutoCheck = false };
AchievementMode achievementMode;
```

- `achievementsHeader.FlatAppearance.BorderSize = 0`.
- Layout: after the Buttons row, `AddRow(layout, "", achievementsHeader)`, then `AddRow(layout, "", achievementsPanel)`. The panel holds the two radio buttons. The form already uses `AutoSize` + `GrowAndShrink`, so it resizes when the panel shows or hides.
- Header click: toggle `achievementsPanel.Visible`, then `UpdateAchievementsHeader()`.
- `UpdateAchievementsHeader()`: `(open ? "▾" : "▸") + " Achievements" + (mode != None && !open ? ": " + name : "")` with names `School mathematics` / `Just a one`.
- `AutoCheck = false` so a click on the checked radio can turn it off. Click handler: `achievementMode = clicked mode == achievementMode ? None : clicked mode`; set both `Checked` from it; update the header; `StartDivisionSearch()`.

### Search dispatch

At the top of the search part of `StartDivisionSearch` (after Buttons are parsed, rule 6 `TakeTwo` applied, and the blocked note built), before the rule-14 branch:

- `achievementMode != None`: build the note (`achievement mode ignores boss rule N` for 11–14, `ignores Required` when Required is set), clear `searchBoss` / `searchPlusOne` / `searchOneSend` / `searchRequired`, then:
  - **School**: `schoolTask = Task.Run(() => SchoolSearch.Find(start, goal, buttons, MaxSchoolPresses, token, queue.Enqueue-through-ResultLine.From, onRejected))`. `const int MaxSchoolPresses = 30;`
  - **JustOne**: `justOneTask = Task.Run(() => JustOneSearch.Search(start, buttons, MaxPresses, queue.Enqueue, token, onRejected))`.
  - Start the timer and return.
- The S == G "no lines" case doesn't apply in either mode (School needs four operators even then; Just a one ignores G).

One field `Task<…>? achievementTask` (with a small result record holding counts, credits and an error) is enough; `OnSearchTick` checks it first, like `stepTask`, drains the queue, and on completion sets the status:

- School done: `School mathematics: {n} line(s)` + rejected + notes. No line: `No line reaches {G} with + - × ÷ in 12 operators or fewer.` Error from preconditions: shown as is.
- Just a one done: `Just a one: {S} → {S+1}, credits: ÷, +, digit` + rejected + notes. No line: `No line makes {S+1} with one = this round.`

`Esc`, `StopDivisionSearch` and `OnFormClosing` must also cancel / clear the new task (add it next to `stepTask`).

`Redraw`: in Just-a-one mode, order with `JustOneSearch.Order(found)` instead of `found.Sort(ResultLine.Ranking)`.

Follow game: `ApplyGamePuzzle` doesn't touch the mode; the search it starts uses it.

## Tests (`CalculatorPuzzle.Tests/AchievementTests.cs`)

`Achievements` helpers:

1. `FourOps`: `+ 7 = × 1 = ÷ 1 = - 0 =` → true; without the `- 0 =` → false; `mod 3 =` adds nothing.
2. `Credit`: `+ 1 =` → `digit`; `5X ÷ 10 =` → `÷`; `X5 mod 10 =` → `mod`; `X5 - 4 0 =` → `digit` (`0` skipped); `+ 10 10 =` → `+`; `+ c =` → `c`; `% 10 =` → `%`.

`SchoolSearch`:

3. S 41, G 48, `+ - × ÷` and keys `0`–`9` (all `:9`): first line costs 8; every line passes `Run` → 48 and `FourOps`.
4. S 41, G 48, `+:9, -:9, ×:9, ÷:9, 10:9, X8:9`: a line is found; first cost ≤ 11; all lines pass `Run` and `FourOps`.
5. No `÷` in Buttons → no lines, error names `÷`.
6. Charges: keys `0`–`9` with `1:1, 0:1` and the four operators `:1` → every line presses `1` at most once (tight charges still give a line, e.g. `× 1 =` plus `+ 7 =`, `- 0 =`, and a `÷` segment with another key).
7. Operator cap: no line has more than 12 operator segments.
8. Cancel: a cancelled token throws `OperationCanceledException` (or returns, matching the other phases).

`JustOneSearch`:

9. S 4, buttons `+:9, 1:9`: line `+ 1 =`, credit `digit`.
10. S 4, buttons `5X:9, ÷:9, 10:9`: line `5X ÷ 10 =`, credit `÷`.
11. S 4, buttons `X5:9, mod:9, 10:9`: line `X5 mod 10 =`, credit `mod`.
12. S 41, buttons `÷:9, 10:9, X8:9`: no line ends with edits after `=` (`÷ 10 = X…` never shows), and none has two `=`.
13. Order: with lines of credits `digit` (cost 2, cost 3) and `÷` (cost 4), the order is digit 2, ÷ 4, digit 3.
14. S 999 999 → error, no lines.

The UI (header, radio toggle, status) is checked by hand and in Test T.

## Manual test

Test T (`user-verification/test-t.md`):

- T1 (School mathematics): any round, School mode on, press the top line. Expected: round won, Steam shows *School mathematics* (if not unlocked yet; otherwise check that the line ends on G with all four operators used).
- T2 (Just a one, `score_is` = S): at a round start, Just a one on, press a top line. Expected: the display shows S + 1. Write down the credit tag.
- T3 (Just a one, `10` / `0` don't take the credit): over the run, press lines with 4 different credits, at least one `÷` / `-` / `+` / `mod` line typed with `10` only. Expected: *Just a one* unlocks on the fourth different credit, not before.
- T4: after a Just a one line, type the new S (S + 1) and solve the round normally. Expected: the round goes on as usual.
- T5 (UI): open and close the section; click a radio twice. Expected: second click turns the mode off; the collapsed header names the active mode.
