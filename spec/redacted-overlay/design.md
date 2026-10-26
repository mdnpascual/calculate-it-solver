# Design: Redacted challenge overlay (challenge 9)

**Status:** Complete (2026-10-24). Tested in-game (Test F).

## Overview

Three pieces, all in the mod plus one shared file:

1. `ButtonSymbols.cs` (new, mod folder, no Unity references): id → overlay text. Linked into the test project so it is checked against `GameLink.MapButton`.
2. `CalcHookCore.cs`: `ButtonInfo` gets `Col` / `Row` from `anchoredPosition`; both JSON files write them; a cached overlay snapshot; `OnGUI` draws it.
3. `Mod.cs`: `OnGUI` override that calls `Core.OnGUI()`.

The solver app (`CalculatorPuzzle/`) doesn't change.

## `GameHook.MelonLoader/ButtonSymbols.cs`

Plain C# (net472 and net9 both compile it), namespace `CalcHook`:

```csharp
public static class ButtonSymbols
{
    /// <summary>Overlay text for a game button id: the solver token, or "(name)" for an id the solver doesn't map.</summary>
    public static string Of(string id) => Token(id) ?? "(" + Bare(id) + ")";

    // Mirrors GameLink.MapButton (CalculatorPuzzle/GameLink.cs). ButtonSymbolsTests keeps them equal.
    public static string Token(string id) { /* same regexes and switch as MapButton */ }

    // "operator_energy" -> "energy", "operator%" -> "%", "number11" -> "11", "number_charge" -> "charge".
    static string Bare(string id)
    {
        string s = id.StartsWith("operator") ? id.Substring(8) : id.StartsWith("number") ? id.Substring(6) : id;
        return s.StartsWith("_") ? s.Substring(1) : s;
    }
}
```

The mod can't reference the .NET 9 app, so the mapping is duplicated on purpose; the test catches drift.

`GameHook.MelonLoader.csproj` compiles it already (SDK globbing). `CalculatorPuzzle.Tests.csproj` adds:

```xml
<Compile Include="..\GameHook.MelonLoader\ButtonSymbols.cs" Link="Linked\ButtonSymbols.cs" />
```

## `CalcHookCore.cs`

### Grid cell

```csharp
// Grid from the game's layout code: x = -300 + col * 91, y = -115 - row * 80 (StartLvl.MiniCreate, CreateCalc).
// Place.x / overall aren't used: CreateCalc writes Place.x = i % 8 while drawing at column i % 4 + 2.
const float GridLeft = -300f, GridTop = -115f, CellW = 91f, CellH = 80f;

static void CellOf(CountUses cu, out int col, out int row)
{
    var p = ((RectTransform)cu.transform).anchoredPosition;
    col = Mathf.RoundToInt((p.x - GridLeft) / CellW);
    row = Mathf.RoundToInt(-(p.y - GridTop) / CellH);
}
```

`ButtonInfo` gets `public int Col, Row;`, set in `ReadButtons`. If the transform isn't a `RectTransform` (shouldn't happen), `Col = Row = -1` and the overlay skips the button.

### JSON

`WritePuzzle` and `WriteJson` append `, "col": <Col>, "row": <Row>` after `legendary` on each button. Order of the list unchanged. The fingerprint in `CheckPuzzle` doesn't include the cell (a button can't move without a charge or id change around it, and moving doesn't change the puzzle).

### Overlay state

```csharp
const int Redacted = 9;
const float OverlayRefresh = 0.1f;
bool _overlayOn;              // F9 toggles; set true when the challenge becomes 9
int _lastChallenge = -1;
float _nextOverlayRead;
List<ButtonInfo> _overlayButtons = new List<ButtonInfo>();
string _overlayHeader = "";
```

In `Tick` (every frame), inside a new `Safe("Overlay", UpdateOverlay)`:

```csharp
void UpdateOverlay()
{
    if (Input.GetKeyDown(KeyCode.F9)) _overlayOn = !_overlayOn;
    int challenge = Challenge();
    if (challenge != _lastChallenge) { _lastChallenge = challenge; if (challenge == Redacted) _overlayOn = true; }
    if (!_overlayOn || Time.unscaledTime < _nextOverlayRead) return;
    _nextOverlayRead = Time.unscaledTime + OverlayRefresh;
    if (_level == null || _calc == null) { _overlayButtons.Clear(); return; }
    _overlayButtons = ReadButtons();
    _overlayHeader = _calc.total + " → " + _level.number;
    _overlaySize = CalcWidth();
}

// CalcSize.size_x on the "Calculator" object: 1 = 4 wide, 2 = 6 wide, 3+ = 8 wide. 0 when unreadable.
int CalcWidth()
{
    if (_calcSize == null) { var go = GameObject.Find("Calculator"); _calcSize = go != null ? go.GetComponent<CalcSize>() : null; }
    return _calcSize != null ? _calcSize.size_x : 0;
}
```

`_calcSize` (`CalcSize`) is cached like `_level` and cleared when `_level` changes (new scene). Columns: `size_x` 1 → 2..5, 2 → 1..6, 3 or more → 0..7; 0 → min..max used column. Rows: 0..max(3, largest used row). `GameObject.Find` runs only until the component is found.

Leaving challenge 9 doesn't force it off; a new run in another challenge keeps the F9 state. (The menu has no `StartLvl`, so nothing is drawn there.) `Input` needs `UnityEngine.InputLegacyModule.dll` in `GameRefs.props`; `GUI` needs `UnityEngine.IMGUIModule.dll`.

### Drawing: `public void OnGUI()`

Wrapped in `Safe("OnGUI", ...)`. Nothing is drawn when `!_overlayOn` or the list is empty.

1. Bounds: columns from `_overlaySize` (see above), rows `0..max(3, maxRow)`.
2. Cell 64 × 48, gap 4, padding 8, header 24 (1080p units). Panel width `pad*2 + n*64 + (n-1)*4`; height similarly plus the header.
3. Scale: `float s = Screen.height / 1080f`, then `s = Mathf.Min(s, 0.5f * Screen.height / height)` so the panel is at most half the screen height. `GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1))`; the virtual screen is `Screen.width / s` × `Screen.height / s`. Panel rect: `x = 16`, `y = Screen.height / s - 16 - height` (bottom-left).
4. `GUI.Box(panel, "")` for the background (dark, set once in a cached `GUIStyle` with a 1×1 `Texture2D`; created lazily inside `OnGUI`, since `GUI.skin` is only valid there).
5. Header: `GUI.Label` with `_overlayHeader` left and `F9 hide` right.
6. For each button: cell rect at `((Col - minCol), (Row - minRow))`. `GUI.Box` with the cell style (tint: normal grey, light blue if `Glass`, darker and dim text if `Uses == 0`). Symbol `ButtonSymbols.Of(Key)`, centered, bold, `fontSize = 22`, reduced to 14 for 5–7 characters and 11 above that. Charges: small label (`fontSize 11`) bottom-right.
7. `=` at column 4, row 3 (fixed; the `=` key isn't a `CountUses`), drawn like a button without the charges label. If a listed button reports that cell, the button wins and `Report` logs it once.
8. Two buttons in the same cell: draw the later one; `Report` it once ("Overlay: two buttons at col,row").
9. Reset `GUI.matrix` to identity at the end.

The textures and styles are built once and kept; no per-frame allocation besides the label strings (the symbol strings can be cached per id in a `Dictionary<string,string>`).

## `Mod.cs`

```csharp
public override void OnGUI() => Core?.OnGUI();
```

## Read-only check

- IMGUI draws from MelonLoader's `OnGUI` callback; no `GameObject`, component, or `Canvas` is created. The two `Texture2D`s are mod-owned, not part of the scene.
- No `GUI.Button` / `GUI.Toggle`: IMGUI labels and boxes don't consume clicks, so the game's uGUI buttons under the overlay still work. (The overlay can cover game UI in the corner; F9 hides it.)
- No new Harmony patch. `Input.GetKeyDown(F9)` only reads.

## Test plan

### Unit tests (`CalculatorPuzzle.Tests/ButtonSymbolsTests.cs`)

- For every id in `docs/button-id.md` (a hard-coded list in the test, the table's ids): if `GameLink.MapButton(id)` is not null, `ButtonSymbols.Of(id) == MapButton(id)`; otherwise `Of(id)` is `(` + bare name + `)`.
- Spot checks: `operator/` → `÷`, `operator*` → `×`, `operator_x4` → `X4`, `operator_4x` → `4X`, `operator_3to0` → `3->0`, `operator_+16` → `X+16`, `number10` → `10`, `number11` → `(11)`, `operator_energy` → `(energy)`, `operator%` → `(%)`, `number_charge` → `(charge)`, `operator_equal` → `(equal)`.

### Build

- `dotnet build GameHook.MelonLoader -c Release` with no warnings.
- `dotnet build`, `dotnet test`: all old tests plus the new ones pass.

### In-game ("Test F: Redacted overlay" in `user-verification/`)

- F1: Start a challenge 9 run. The overlay appears bottom-left, 4 × 4, with `=` in the 4th row, 3rd column; each cell's symbol matches the real button in the same position (check by pressing a digit and watching the display). Note the cells vs the screen (open question 2).
- F2: Buy buttons until the calculator widens (to 6, then 8 columns). The overlay widens one column on each side the same way within 0.1 s, and `=` stays on the same key.
- F3: Press a button: its charge in the overlay drops. A glass button is blue; a 0-charge button greys out.
- F4: F9 hides and shows it. In a normal run (not challenge 9) it is hidden at start; F9 shows the same layout.
- F5: Resize the window / change resolution: the overlay stays in the bottom-left corner, keeps its proportions and is never taller than half the screen.
- F6: `puzzle.json` and `state.json` have `col` and `row` on every button; the app still follows the game as before.

## As built (2026-10-24)

Implemented as designed, tested in-game (Test F done; open question 2 answered: the cells match the screen). 352 tests, both builds with no warnings.

- `ButtonSymbols.cs` has `#nullable disable` so the linked copy in the test project builds without warnings. `ButtonSymbolsTests` also reads `docs/button-id.md` and fails if its id list drifts from the test's list.
- `CalcWidth` uses `FindObjectOfType<CalcSize>()` (cached per `StartLvl`) instead of `GameObject.Find("Calculator")`.
- `OnGUI` draws only on `EventType.Repaint` (nothing takes input). Style and texture are built lazily; the `Texture2D` has `HideAndDontSave`.
- Symbol font size: 22 up to 4 characters, 14 up to 7, 11 up to 9, else 9.
- `col` / `row` are -1 when the transform isn't a `RectTransform`; those buttons are skipped. Two buttons in one cell: the later one is drawn and `Overlay` is reported once.
