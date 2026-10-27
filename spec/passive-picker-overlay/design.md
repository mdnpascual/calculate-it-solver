# Design: Passive picker overlay (challenge 9)

**Status:** Done (2026-10-27). Built as designed; see "As built".

## Overview

Mod only. No change to the solver app.

1. New plain C# file `GameHook.MelonLoader/PassiveTexts.cs`: parses a passive text asset the way `TextsLoader.ReadTextAsset` does. Linked into the tests like `ButtonSymbols.cs`.
2. `CalcHookCore.cs`: `PassiveOffer` struct, `ReadPassivePicker()`, refresh in `UpdateOverlay`, `DrawPassives()` in `DrawOverlay`, `passivePicker` in `state.json` and the change line.
3. `GameStubs/Game.cs`: the new game members (see "Stubs").

## PassiveTexts.cs

```csharp
/// <summary>Passive name and description list from a game passive text asset ("1. Near1: You win ...;").
/// Mirrors TextsLoader.ReadTextAsset: line i is passive index i (empty lines dropped).</summary>
public static class PassiveTexts
{
    public static List<(string Name, string Description)> Parse(string text)
}
```

Per line (split on `\n`, `RemoveEmptyEntries`, `Trim`): `dot = IndexOf('.')`, `semi = IndexOf(';')`; when both exist, `parts = Substring(dot + 1, semi - dot - 1).Trim().Split(':')`, and with at least 2 parts the entry is `(parts[0].Trim(), parts[1].Trim())`. Otherwise the entry is `(null, null)`, so indexes stay aligned with the game. Strip a leading BOM (`﻿`) before splitting (the game's `IndexOf('.')` doesn't care, and neither does this).

## Reading the picker

```csharp
struct PassiveOffer
{
    public int Id;             // RandomisatorPassives.id (0-based passive index)
    public string Name, Description;   // null when the index has no text
    public float X;            // RectTransform.position.x, for left-to-right order
}

List<PassiveOffer> _passiveOffers = new List<PassiveOffer>();   // empty when the panel doesn't show
TextsLoader _texts; string _textsLang; List<(string Name, string Description)> _passiveTexts;
```

`ReadPassivePicker()` returns an empty list unless `Challenge() == Redacted`, `_level != null`, `_level.passiveshop != null` and `passiveshop.activeInHierarchy`. Then:

- `pool = passiveshop.GetComponent<PassivePool>()`; for each `p` in `pool.passives` with `p != null && p.gameObject.activeInHierarchy`: `Id = p.id`, `X = p.transform.position.x`, text from `PassiveText(p.id)`.
- Sort by `X`.

`PassiveText(id)`: `lang = GamePrefs.GetString("lang")`; when `_texts == null` (or destroyed) or `lang != _textsLang`: `_texts = GameObject.Find("TextsManager")?.GetComponent<TextsLoader>()`, asset = `lang == "rus" ? _texts.textAsset_p : _texts.textAsset_eng_p`, `_passiveTexts = PassiveTexts.Parse(asset.text)`. Index in range and name not empty → the entry; else `("passive " + (id + 1), null)`.

Refresh: in `UpdateOverlay`, next to the shop, `Safe("Passives", () => _passiveOffers = ReadPassivePicker())`. Because `UpdateOverlay` returns early when `_overlayOn` is false, F9 also hides the panel in challenge 9 (decided). Also clear `_passiveOffers` on the early return when `_level == null`.

`DrawPassives` starts with `if (Challenge() != Redacted || _passiveOffers.Count == 0) return;`, so the challenge 9 rule holds in the draw path too, even if `_passiveOffers` is stale (for example right after leaving a challenge 9 run, before the next 0.1 s refresh).

## Drawing

`DrawOverlay`: today it returns when there are no buttons and no shop. Change the early return to `if (_overlayButtons.Count == 0 && !shop && _passiveOffers.Count == 0) return;` and call `DrawPassives()` after the shop (it sets its own `GUI.matrix` from `s = Screen.height / 1080f`; it doesn't depend on the calculator's shrink scale).

`DrawPassives()` (1080p units, `W = Screen.width / s`):

```csharp
const float PassiveWidthShare = 0.75f;   // panel width / screen width; tune after playtesting
const float PassiveHeightShare = 0.25f;  // max panel height / screen height
const float PassiveTop = 16f;
const int PassiveTitleSize = 18, PassiveTextSize = 14;
const int PassiveTitleMin = 12, PassiveTextMin = 10;
```

1. `width = W * PassiveWidthShare`, `x0 = (W - width) / 2`, `y0 = PassiveTop`.
2. `n = _passiveOffers.Count` (3); `boxW = (width - 2 * PanelPad - (n - 1) * CellGap) / n`.
3. Heights: `titleH = _passiveTitleStyle.CalcHeight(title, boxW - 2 * inner)`, `descH = _passiveTextStyle.CalcHeight(desc, ...)`; `boxH = max over offers (inner + titleH + 4 + descH + inner)`.
4. Height cap: `maxH = (Screen.height / s) * PassiveHeightShare` (= 270 at any resolution, since GUI units are 1080p). While `2 * PanelPad + PanelHead + boxH > maxH` and a font can still shrink, lower both `fontSize` values by 1 (each stops at its minimum) and measure again. Then `boxH = min(boxH, maxH - 2 * PanelPad - PanelHead)`; the description is clipped to its box (`GUI.BeginGroup` per box, or `clipping = TextClipping.Clip` with a fixed rect). The sizes start from the base values on every repaint, so a shorter next picker gets the full size back.
5. Panel: `GUI.Box(new Rect(x0, y0, width, 2 * PanelPad + PanelHead + boxH), "", _boxStyle)`; header `Passives` with `_headerStyle`.
6. Each box: background cell like `DrawCell`'s (same tint), title (`_passiveTitleStyle`: bold, `PassiveTitleSize`, white, `wordWrap`, upper-left), description below (`_passiveTextStyle`: `PassiveTextSize`, light grey, `wordWrap`).

The two styles are built in `BuildStyles`. `CalcHeight` is called during repaint only (as everything in `DrawOverlay`), fine for 3 boxes.

## Data

`Poll`: `var passives = ReadPassivePicker();` (cheap: three objects) and append ` passives=[Near1, Odd, Lucky]` to the change line when not empty. `WriteJson`: `"passivePicker": null` or `{ "open": true, "offers": [ { "id": 0, "name": "Near1", "description": "...", "x": 412.0 } ] }` (strings escaped with the existing JSON helper, floats `InvariantCulture`, one decimal). `CheckPuzzle`'s fingerprint doesn't include it.

## Read-only check

- New reads: `StartLvl.passiveshop`, `PassivePool.passives`, `RandomisatorPassives.id`, `TextsLoader.textAsset_eng_p` / `textAsset_p`, `TextAsset.text`, `GamePrefs.GetString("lang")`, `GameObject.Find("TextsManager")`, `Transform.position`, `activeInHierarchy`.
- Not used: `PassivePool.CreatePassive`, `OnEnable`, `GenerateUniqueNumbers` (advances nothing, but don't call it), `StartLvl.PassiveChoosed`, any `Infos` or `TextsLoader` assignment, `SetActive`.

## Stubs

`GameStubs/Game.cs` needs, with the game's signatures: `StartLvl.passiveshop` (`public GameObject`), class `PassivePool : MonoBehaviour` with `public RandomisatorPassives[] passives`, class `RandomisatorPassives : MonoBehaviour` with `public int id`, class `TextsLoader : MonoBehaviour` with `public TextAsset textAsset_eng_p, textAsset_p`, and `GamePrefs.GetString(string)` if the stub lacks it. Run `tools/check-stubs.ps1` after.

## Test plan

### Unit tests (`CalculatorPuzzle.Tests/PassiveTextsTests.cs`, `PassiveTexts.cs` linked)

- The first lines of the real asset: `1. Near1: You win the round, even if you are 1 short of the required amount;` → index 0 `("Near1", "You win the round, even if you are 1 short of the required amount")`.
- No space after the colon: `2. Jackpot:Every time you get the number 777, you will get 10 coins;` → `("Jackpot", "Every time …")`.
- No space after the dot (Russian file): `3.Долой босса!:Позволяет …;` → `("Долой босса!", "Позволяет …")`.
- `\r\n` line ends and a leading BOM; empty lines don't shift indexes (dropped, like the game).
- A line without `;` or `:` gives `(null, null)` and keeps the next line's index.
- A description with quotes (`Professor: Adds 3 coins if … "+","-","*","/";`) is kept whole.

### Build

`dotnet build GameHook.MelonLoader -c Release`, `dotnet build GameHook.MelonLoader -c Release -p:GameRefs=Stubs`, `dotnet build`, `dotnet test`.

### Manual: Test Z (`user-verification/test-z.md`)

- **Z1** Start a challenge 9 run. Expected: before round 1 the picker opens and the top-center panel shows 3 boxes whose titles are real passive names (Near1, Odd, …) in the same order as the icons; hovering an icon in the game shows a Latin button text instead.
- **Z2** Pick one. Expected: the panel goes away with the picker.
- **Z3** Beat the round 5 boss. Expected: the panel shows again with the new 3 offers.
- **Z4** F9 while the picker is open (challenge 9). Expected: the passive panel hides with the other panels; F9 again shows it.
- **Z5** Another challenge (for example 0 or 8), F9 on, reach a picker (before round 1 and after the round 5 boss). Expected: calculator overlay shows, passive panel never does. Also go straight from a challenge 9 run to another challenge's run: no passive panel there.
- **Z6** Language Russian, challenge 9 picker. Expected: Russian titles and descriptions.
- **Z7** Playtest the size at 75% width, at most 25% height: is the text readable, and is every passive icon (and its hover box) still visible and clickable? Note what to change (width, height cap, fonts, top offset).
- **Z8** `state.json` while the picker is open: `passivePicker.offers` has the 3 ids and texts.

## As built

Implemented 2026-10-27 as designed; no size changes after Z7.

- `PassiveTexts.cs` (linked into the tests, 7 cases in `PassiveTextsTests.cs`); stubs for `StartLvl.passiveshop`, `GamePrefs.GetString`, `PassivePool`, `RandomisatorPassives`, `TextsLoader` (`tools/check-stubs.ps1`: no differences).
- `CalcHookCore`: `ReadPassivePicker` / `PassiveText` / `DrawPassives`, styles in `BuildStyles`, refresh in `UpdateOverlay`. The offers are also cleared when the overlay is off (F9) or the level is gone. `Poll` adds `passives=[...]` to the change line; `WriteJson` takes the list and writes `passivePicker`.
- Constants kept: width share 0.75, height share 0.25, top 16, fonts 18/14 (min 12/10).
- Test Z: Z1, Z2, Z3, Z4, Z5, Z7 (size good) and Z8 confirmed. Z6 (Russian) not run: the code path exists (`lang == "rus"` picks `textAsset_p`), but Russian is not officially supported and Cyrillic rendering is unverified.
