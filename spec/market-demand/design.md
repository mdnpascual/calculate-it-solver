# Design: Market Demand (required button)

**Status:** Complete (2026-10-24). Tested in-game (Test E, E1–E5).

## Overview

The mod reads one private string (`StartLvl.challengeButtonName`) by reflection, turns it into the button id it already reports, and writes it as `required` in `puzzle.json`. The app reads it into a new `GamePuzzle.Required`, shows it in a new Required box, and uses it in two places: the ranking (lines that press it first) and the caps (only lines that press it tighten them). The searches themselves don't change.

## Mod: `GameHook.MelonLoader/CalcHookCore.cs`

### Reading the fields

```csharp
// Market Demand (challenge 8): StartLvl.challengeButtonName is private, so it is read by reflection.
const int MarketDemand = 8;
static readonly FieldInfo RequiredNameField = typeof(StartLvl).GetField("challengeButtonName", BindingFlags.Instance | BindingFlags.NonPublic);
static readonly FieldInfo RequiredLabelField = typeof(StartLvl).GetField("challengeButtonLabel", BindingFlags.Instance | BindingFlags.NonPublic);
static readonly FieldInfo RequiredUsesField = typeof(StartLvl).GetField("challengeButtonUses", BindingFlags.Instance | BindingFlags.NonPublic);

static int Challenge()
{
    try { return GamePrefs.GetInt("challenge"); }
    catch (NullReferenceException) { return 0; }
}
```

`GamePrefs.GetInt` only calls `Store.GetInt` (a read). If a `FieldInfo` is null (game update renamed it), `Required` returns null and `Report` logs it once (`Safe` / `Report` already dedupe).

### `ButtonInfo`

Add `public string ObjectName;` = the raw `cu.gameObject.name` (with `(Clone)` if any). `challengeButtonName` is the raw `GameObject.name`, so the match is on `ObjectName`, and the id written is that button's `Key`.

### `RequiredKey(List<ButtonInfo> buttons)`

```csharp
string RequiredKey(List<ButtonInfo> buttons)
{
    if (Challenge() != MarketDemand || RequiredNameField == null) return null;
    var name = RequiredNameField.GetValue(_level) as string;
    if (string.IsNullOrEmpty(name)) return null;
    foreach (var b in buttons)
        if (b.ObjectName == name) return b.Key;
    return null;
}
```

A button with uses 0 is still in `ReadButtons` (active), so the match works even after the last charge.

### `CheckPuzzle`

- `string required = RequiredKey(buttons);`
- Fingerprint: append `|req:` + `(required ?? "-")`.
- Pass `required` to `WritePuzzle`.

### `WritePuzzle`

After `scene`:

```json
  "challenge": 8,
  "required": "operator/",
```

`required` uses `Str(...)`, so null writes `null`.

### `Poll` / `WriteJson` (`state.json`)

- Log: ` required=<id>` after `op=` when `required` isn't null.
- `state.json`: `challenge`, `required`, `requiredLabel` (`RequiredLabelField`), `requiredUses` (`RequiredUsesField`, `null` when unreadable). The last two are diagnostics only.

Only fields are read. No game method that changes state is called, no new patch.

## App: `CalculatorPuzzle/GameLink.cs`

### `GamePuzzle`

Add `string? Required` at the end (solver token, or the unmapped id without `operator_`; null when none):

```csharp
public sealed record GamePuzzle(int Seq, string Time, int Round, long Start, long Goal,
                                string ButtonsText, IReadOnlyList<string> Ignored, IReadOnlyList<string> Glass,
                                string? Required = null);
```

The default keeps existing callers and tests compiling.

### `Parse`

```csharp
string? required = null;
if (root.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.String && r.GetString() is { Length: > 0 } id)
    required = MapButton(id) ?? (id.StartsWith("operator_") ? id["operator_".Length..] : id);
```

Missing, `null` or non-string → null. `challenge` isn't read (`required` is already null outside challenge 8).

## App: `CalculatorPuzzle/EngineeringCalculator.cs`

### `Buttons.Token(string text)`

```csharp
/// <summary>The canonical token for one button ("/" → "÷", "x1" → "X1"), or null when it isn't a button.</summary>
public static string? Token(string text)
```

Parse `text + ":1"` with `Parse` and return the single key of `Charges`. `=` and empty text → null. This keeps the spelling rules in one place.

### `Buttons.Uses(IReadOnlyList<Press> presses, string token)`

`presses.Any(p => p.Key != Key.Equals && p.ToString() == token)`. Same key as `Charges`.

## App: `CalculatorPuzzle/ResultLine.cs`

- Add `bool Missing` (true when a required button is set and the line doesn't press it). `From(..., Buttons buttons, string? required = null)` sets it with `Buttons.Uses`.
- `Ranking`: first key `x.Missing.CompareTo(y.Missing)` (false first), then the current order.
- `ToString`: `$", no {Required}"` inside the brackets when `Missing`. Store the token on the record (`string? Required`) so `ToString` can print it (`÷` shown as `/` like the presses).

## App: `CalculatorPuzzle/MainForm.cs`

### Layout

- New `requiredBox` (`TextBox`, same width as `startBox`).
- `AddRow(layout, "Required", requiredBox);` right before the `Buttons` row.
- `requiredBox.TextChanged += MarkDivisionStale`, and add it to the Enter-key loop.

### `ApplyGamePuzzle`

`requiredBox.Text = puzzle.Required ?? "";` before `StartDivisionSearch()`. Lock already stops `ApplyGamePuzzle` from running, so nothing new there.

### `StartDivisionSearch`

```csharp
searchRequired = null;
requiredNote = null;
var typed = requiredBox.Text.Trim();
if (typed.Length > 0)
{
    var token = Buttons.Token(typed);
    if (token == null)
        requiredNote = $"no line can use {typed}";
    else if (buttons.Charges.GetValueOrDefault(token) <= 0)
        requiredNote = $"{token} isn't in Buttons";
    else
        searchRequired = token;
}
```

`searchRequired` (field) goes into every `ResultLine.From(...)` call. `requiredNote` is appended to every status (searching and done).

Division cap inside the task:

```csharp
var r = ResultLine.From(line, buttons, required);
queue.Enqueue(r);
if (!r.Missing)
    divisionCap = Math.Min(divisionCap, line.Cost + CostSlack);
```

### Modulo cap (`OnSearchTick`)

```csharp
var counted = found.Where(l => !l.Missing).ToList();
int cap = counted.Count > 0 ? Math.Min(MaxPresses, counted.Min(l => l.Cost) + CostSlack) : MaxPresses;
```

With no required button, `Missing` is always false, so both caps are exactly as today.

### `DoneStatus`

When `searchRequired != null`, append `, {found.Count(l => !l.Missing)} use {searchRequired}`. When `requiredNote != null`, append `, {requiredNote}`.

## Tests

### `CalculatorPuzzle.Tests/GameLinkTests.cs`

| Test | Input | Expect |
|------|-------|--------|
| `Parse_maps_required` | `"required": "operator/"` | `Required` = `÷` |
| `Parse_maps_required_left_button` | `"required": "operator_reverse"` | `flip` |
| `Parse_null_required` | `"required": null` | null |
| `Parse_without_required_field` | old sample | null |
| `Parse_keeps_unmapped_required` | `"required": "operator_prime"` | `prime` |

### `ButtonsTests` (or the existing simulator test file)

- `Token` returns `÷` for `/` and `÷`, `X1` for `x1`, `flip` for `flip`, null for `=`, `foo` and empty.
- `Uses` is true for `[8X, ÷, 10, =, flip]` and `flip`, false for `X8`.

### `ResultLineTests` (new, or next to the existing ranking tests)

- Ranking: a 4-press line that uses the required button ranks before a 3-press line that doesn't.
- No required button: ranking unchanged (`Missing` false for all).
- `ToString` of a missing line ends with `, no flip)`; with `÷` it prints `no /`.

### End to end (no UI)

41 → 48, Buttons `÷:3, 10:13, X8:2, 8X:2, flip:2`, required `flip`: build `ResultLine`s from `ArithmeticSearch.Find` + `DivisionShortcut.Solve`, sort by `Ranking`, and assert the first line contains `flip` and `8X ÷ 10 = flip` is in the list.

## Manual test plan (`user-verification/test-e.md`, new "Test E: Market Demand")

1. Rebuild and reinstall the mod (`dotnet build GameHook.MelonLoader -c Release`), start the app with **Follow game**.
2. **E1.** Start a Market Demand run. Check `puzzle.json`: `"challenge"` is 8 (answers open question 1) and `"required"` is the id of the button named in the rule text. The app's Required box shows its token.
3. **E2.** Lines that press the required button come first; the others say `no <token>`.
4. **E3.** Note which buttons get picked over a few rounds. A left-side button (`X1`, `flip`, a transform) being picked answers open question 2.
5. **E4.** Save and quit mid-round, continue the run. `required` is the same after the restore.
6. **E5.** A normal run: `"challenge"` is not 8, `"required"` is null, the Required box is empty.
7. Report any `error in` lines from `MelonLoader\Latest.log`.

## Risks

- `ModuloSearch.Find` and `ArithmeticSearch.Find` keep up to 100 lines. When most of them miss the required button, the ones that use it could be cut. Unlikely at the usual sizes; if seen, the follow-up is to pass `required` into them so the limit counts only lines that use it.
- No required line until late in the division search means the cap stays at `MaxPresses` longer, so the search can run longer than today. Same as today's no-arithmetic case.
- The Update-path pick (first round, after a load) happens one frame after `NextLvl`. The fingerprint holds the publish until `required` is stable for 50 ms.

## As built (2026-10-24)

Built as designed. Differences and findings:

- `Buttons.Token` rejects text with `,` or `:` and empty text before calling `Parse(text + ":1")`.
- `ResultLine` gets `Missing` and `Required` as trailing record parameters (defaults `false` / `null`), so existing constructors and callers are unchanged. `ToString` replaces `÷` with `/` over the whole string.
- The arithmetic cap in `MainForm` (the division cap) counts only lines that press Required, as does the modulo cap.
- Mod: `ReadRequired<T>` reads each private field and logs a missing field once; `ButtonInfo.ObjectName` holds the raw `GameObject.name`.
- Tests: 23 new (`GameLinkTests`, `RequiredButtonTests`); 336 in all.
- In-game (Test E): `challenge` is 8 and `required` is the button id (`operator_mod`). The Required box and `use mod` status work. An unsupported required button (`money`, a left-side button) shows in the box with `no line can use money`, as designed. `required` is unchanged after Continue. E5: after switching challenge, `puzzle.json` showed `challenge` 9 (not 8), so `required` is off outside Market Demand.
- Open question 2 is answered: left-side buttons can be required.

