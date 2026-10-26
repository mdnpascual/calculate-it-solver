# Button labels: design

## Approach

A label layer at the edges. Tokens stay the internal names; `Label(token)` turns a token into what a person reads, and `Token(text)` turns a label (or a token) back into a token before parsing. Same pattern as `GameLink.MapButton` / `ButtonSymbols.Token`: one copy in the solver, a mirror in the mod, a linked test keeps them equal.

## Types

### `CalculatorPuzzle/ButtonLabels.cs` (new)

```csharp
public static class ButtonLabels
{
    // token -> label for fixed tokens
    static readonly Dictionary<string, string> Fixed = new()
    {
        ["money"] = "$", ["flip"] = "reverse", ["round"] = "->)", ["sup"] = "combine",
        ["sqr"] = "X²", ["median"] = "_2_", ["25|69"] = "mirror", ["=="] = "equalize",
        ["<<"] = "X<<", [">>"] = "X>>", ["near"] = "+-10", ["reset"] = "x->0", ["to25"] = "x->25",
    };

    public static string Label(string token);   // fixed table, then c<n> -> volt<n>, q<d> -> unk<d>, m<n> -> mem<n>, mw -> mem; else token
    public static string Token(string text);    // reverse of Label, case-insensitive; a token or unknown text comes back unchanged
    public static string Line(IEnumerable<string> presses); // presses joined by ' ', each through Label, '÷' shown as '/'
}
```

- Prefix forms: `^c(\d*)$` → `volt$1`, `^q(\d?)$` → `unk$1`, `^m(\d+)$` → `mem$1`, `mw` → `mem`. Reverse: `^volt(\d*)$` → `c$1`, `^unk(\d?)$` → `q$1`, `^mem(\d+)$` → `m$1`.
- `Token` lowercases its input (Parse already lowercases), so the reverse table keys are lowercase (`x²`, `x<<`, `x->25`, `x->0`).
- If the `²` check fails (Test X1/X2), change one entry (`["sqr"] = "sqr"`), nothing else.

### `GameHook.MelonLoader/ButtonSymbols.cs`

- Add `Label(string token)`, a copy of `ButtonLabels.Label` (plain C#, net472; `Regex` already used).
- `Of(id)` returns `Label(Token(id))` for a mapped id, `(name)` otherwise.
- The live overloads return `volt<uses>`, `unk<digit>`, `mem<stored>` / `mem` instead of `c…`, `q…`, `m…` / `mw`.
- `CalcHookCore.SymbolOf(id)` writes `"mw"` for a shop mw offer: use `Label("mw")`.
- `SymbolFontSize`: `equalize` (8) gets 11 pt, `volt12` (6) 14 pt. Check in Test X that they fit a cell; no change planned.

## Call sites (solver)

| Where | Change |
|-------|--------|
| `Buttons.Parse` (`EngineeringCalculator.cs`) | after `token = parts[0]` (and for entries without `:`), `token = ButtonLabels.Token(token)` before the existing matching |
| Required box parse in `MainForm` | `ButtonLabels.Token` on the typed text |
| `ResultLine.ToString` | presses through `ButtonLabels.Line`; `no {Required}` through `Label` |
| `GameLink.Parse` → `ButtonsText` | write `Label(token)` (and the live `volt12:12`, `unk8:1`, `mem5:1`); `GamePuzzle.Required`, `Glass`, `Blocked` keep tokens |
| `MainForm` Required box fill, status `use …`, `no line can use …`, `blocked: …` | `Label` when the text is built |

`ButtonsText` with labels parses back to the same `Buttons` (round-trip test). Internal lists (`Glass`, `Blocked`, `Required`) stay tokens so the comparisons in `MainForm` / `RequiredPlan` don't change.

## Tests (`CalculatorPuzzle.Tests/ButtonLabelsTests.cs`, new)

- `Label` for every row of the requirements table, including `c`, `c12`, `q`, `q8`, `m5`, `mw`.
- `Token(Label(t)) == t` for every token `MapButton` can return and the live forms.
- `Token` is case-insensitive (`VOLT12`, `x²`, `X²`) and leaves tokens and unknown text unchanged.
- No label equals another button's token (collect all `MapButton` outputs, compare).
- `Buttons.Parse("volt12:12, x²:3, $:1, reverse:2, mirror:1, equalize:1, x<<:1, +-10:1, x->25:1, x->0:1, ->):1, combine:1, _2_:1, unk8:1, mem5:1", …)` equals the parse of the token form.
- `ResultLine.ToString` shows labels (`volt12 volt11 / =`).
- `GameLink.Parse` writes labels in `ButtonsText`; parsing that text gives the same buttons as before.
- `ButtonSymbolsTests`: for every id in `Ids`, `ButtonSymbols.Of(id) == ButtonLabels.Label(MapButton(id))` for mapped ids; live overloads give `volt12`, `unk8`, `mem5`, `mem`.

Existing tests that assert `ToString`, `ButtonsText` or overlay text with old tokens need their expected strings updated; tests that only feed token input stay as they are.

## Docs to update when implemented

- `CLAUDE.md` engineering table: add the label next to each changed token (or a short "labels" line pointing here).
- `CalculatorPuzzle/README.md` ("Files": new `ButtonLabels.cs`; `ResultLine`, `GameLink`, `EngineeringCalculator` notes).
- `GameHook.MelonLoader/README.md` (overlay shows labels).
- `docs/button-id.md`: add a label column for the mapped ids (the `ButtonSymbolsTests` regex reads only the first column).
- `spec/README.md` line.

## As built

- `ButtonLabels.Token("mem")` returns `m` (the key `MapButton` returns for `number_mw`), not `mw`; `Label` maps both `m` and `mw` to `mem`.
- `Buttons.Token` (Required box) runs the typed text through `ButtonLabels.Token` and parses the token; `Buttons.Parse` does the same per entry after the `:` split.
- `MainForm` fills the Required box with the label and compares `ButtonLabels.Token(typed)` with the game's token for boss mode.
- Mod: `ButtonSymbols.Label` is a copy; `ButtonLabelsTests`/`ButtonSymbolsTests.Label_copy_equals_ButtonLabels_Label` keep them equal. `sqr` label is written `X²` in the mod source. `SymbolFontSize` is unchanged until Test X.
- Tests: 756 before, 788 after.
