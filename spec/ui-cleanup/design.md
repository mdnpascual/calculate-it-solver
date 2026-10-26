# UI cleanup: design

**Status:** Done (2026-10-26).

## Where the code goes

| Item | File | Change |
|------|------|--------|
| 1, 2 | `CalculatorPuzzle/MainForm.cs` (`AddInlineRow(layout, "Starting number (S)", …)`, about line 129) | Captions `Start (S)` and `Goal (G)` |
| 3 | `MainForm.cs`, `divisionLinesBox` initializer | Anchor left and right, small width, both scrollbars |
| 4 | `CalculatorPuzzle/ButtonLabels.cs`, `Line` | `string.Join(" | ", …)` |
| 5, 6 | `CalculatorPuzzle/ResultLine.cs`, `ToString` | `press`, `empty`; drop the `Credit` tag |
| 7–9 | `MainForm.StartDivisionSearch` | Boss, Last, Money: empty or bad → 0, no message |
| 10 | `CalculatorPuzzle/EngineeringCalculator.cs`, `Buttons.Parse` (about lines 267–298) | Five error texts |
| 11–16 | `MainForm.cs`: `StartDivisionSearch`, `OnSearchTick`, `OnAchievementTick`, `OnStepTick`, `OnModuloDone`, `DoneStatus`, `NoOneSendLine`, `GoalText` | Notes, prefixes, no-line and error messages |

## Item 3: Lines box width

The form is `AutoSize` / `GrowAndShrink`, and `layout` is an auto-sized `TableLayoutPanel` with auto-sized columns, so column 2 is as wide as its widest control. A control anchored `Left | Right` stretches to its cell:

```csharp
readonly TextBox divisionLinesBox = new()
{
    ReadOnly = true,
    Multiline = true,
    ScrollBars = ScrollBars.Both,
    WordWrap = false,
    Anchor = AnchorStyles.Left | AnchorStyles.Right,
    Width = 200,   // below every other row, so the column width comes from them
    Height = 200,
};
```

`ScrollBars.Both`: with ` | ` a long line can pass the box width, and the bracket would be cut off with only a vertical bar.

## Items 4–6: line format

`ButtonLabels.Line` is used only by `ResultLine.ToString` (and tests). `.Replace('÷', '/')` stays.

```csharp
public override string ToString() =>
    $"{ButtonLabels.Line(...)}   ({Cost} press{goal}, spare {Spare}{(Drained > 0 ? $", empty {Drained}" : "")}{no})".Replace('÷', '/');
```

`ResultLine.Credit` stays (`JustOneSearch.Order` groups by it); only the ` [credit]` append goes. Update the record's comment.

## Items 7–9: boxes as 0

In `StartDivisionSearch`:

- Boss: `int.TryParse` fails or out of 0–99 → `bossRule = 0`, go on.
- Last: `long? last = TryParse ≥ 0 ? value : 0`. `Buttons.Parse` then never sets `LastIgnored`.
- Money: `long? money = TryParse ≥ 0 ? value : 0`. `MoneyIgnored` is never set.
- Delete the `requiredNote` lines for `LastIgnored`, `money $M`, `MoneyIgnored` and `mw empty`. `gameIgnored` stays if anything else reads it; otherwise remove the field and its assignment.

## Item 10: Buttons errors

Literal text changes in `Buttons.Parse`:

- `mw key \"{raw.Trim()}\" …` → `mem key \"{raw.Trim()}\" …`
- `Only one mw button (mD) is allowed` → `Only one mem button (memD) is allowed`
- `Charge key \"{raw.Trim()}\" needs …` → `Volt key \"{raw.Trim()}\" needs …`
- `Charge key c{value} can't …` → `Volt key volt{value} can't …`
- `Quest key needs its digit (write qD:n)` → `Unknown key needs its digit (write unkD:n)`

Check that `unkD:n` and `memD` are accepted by the Buttons box (`ButtonLabels.Token`, `spec/button-labels/`); they are, so the hints are right.

## Items 11–16: status

- `DoneStatus`: remove the three prefix lines (`Near 1`, `Rule 12`, `Rule 13`); drop the `modulo` argument's error text: add the part only when it is a count (`N modulo line(s)` with N > 0). Callers pass the count, not an error. Keep `BossTestMarker`.
- One helper for the no-line text:

  ```csharp
  string NoLineStatus() => "No line fits the available buttons" + (rejectedLines > 0 ? " within the charges." : ".");
  ```

  Remove `NoOneSendLine` and `GoalText` (only used for these messages).
- `OnSearchTick`: on `error != null` (Division Shortcut input error) don't show it; finish like a search with no division lines (the modulo phase still runs where it did). Where the code now does `SetStatus(error)`, use `found.Count == 0 && !searchSameNumber ? NoLineStatus() : DoneStatus(...)`. The rule 6 / 12 / 13 branches become `NoLineStatus()`. Keep the existing boss rule 11 behaviour (with no lines, the done status, which is the marker).
- `OnModuloDone`: the rule 13 branch goes; `found.Count == 0 && !searchSameNumber && !searchBoss` → `NoLineStatus()`, else `DoneStatus`.
- `OnStepTick`: `found.Count == 0` → `NoLineStatus()`, else `DoneStatus` with the modulo count (no `Rule 14: …` prefix). `r.DivisionError` isn't shown.
- `OnAchievementTick`: `r.Error` isn't shown. `found.Count == 0` → `NoLineStatus()`; else `$"{found.Count} line(s)"` plus `rejected` and the notes, joined by `, `.
- Exceptions (`task.Exception!.GetBaseException().Message`) stay everywhere.
- The search classes keep returning their error strings (tests check them); only `MainForm` stops showing them.

## Docs to update

- `CalculatorPuzzle/README.md`, "Files": the notes for `ResultLine.cs` (bracket words, no credit tag), `ButtonLabels.cs` (`Line` joins with ` | `), `MainForm.cs` (captions, Lines box width, Boss/Last/Money as 0, removed notes), and the section text that quotes removed status prefixes (`Rule 12: every = adds 1:`, `Rule 13: one = per line:`, `Near 1: goal ± 1:`).
- `spec/README.md`: this spec's line.
- Earlier specs keep their old examples; they describe what was true then.

## Test plan

Update:

- `ButtonLabelsTests.Line_shows_labels_and_slash`: `"volt12 | volt11 | / | ="`, `"8X | / | 10 | = | reverse"`.
- `ButtonLabelsTests.ResultLine_ToString_shows_labels`: `StartsWith("reverse | /   (2 press")`.
- `AchievementTests.ResultLine_shows_the_credit` → `ResultLine_hides_the_credit`: `Credit: "÷"`, `Assert.DoesNotContain("[", line.ToString())`.
- Any test that checks the five Buttons error texts (`grep -rn "mw key\|mw button\|Charge key\|Quest key" CalculatorPuzzle.Tests`), and any other check on `presses` / `empties` (`grep -rn "presses\b\|empties" CalculatorPuzzle.Tests`, ignoring variable names).

Add (in `ButtonLabelsTests.cs`):

- `ResultLine_ToString_short_words`: `Drained = 1`, `Reaches = 49`, `Missing = true`, `Required = "X8"` prints `(… press, goal 49, spare …, empty 1, no X8)`; with `Drained = 0` there is no `empty`.
- `Parse_errors_use_labels`: `Buttons.Parse("q:1")` → `Unknown key needs its digit (write unkD:n)`; `"c0:1"` → starts with `Volt key`; `"c2:3"` → `Volt key volt2 can't have more than 2 presses`; two mem keys with charges → `Only one mem button (memD) is allowed`.

`MainForm` has no unit tests; check items 1–3, 7–9 and 11–15 by running the app.

Manual: `dotnet run --project CalculatorPuzzle`.

- S 41, G 48, default buttons, Enter: captions `Start (S)` / `Goal (G)`; lines like `8X | / | 10 | = | reverse   (4 press, spare …)`; the Lines box lines up with the widest row and the window is no wider than before.
- Boss `abc`, Last and Money empty, Enter: a normal search, no input message.
- Boss 13, a goal no line can reach (e.g. remove all operators): `No line fits the available buttons.`
- Buttons `q:1`: `Unknown key needs its digit (write unkD:n)`.
- Achievements → Just a one: lines have no `[…]` tag; done status is `N line(s)…`.
