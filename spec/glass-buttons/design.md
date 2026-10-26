# Design: Glass buttons

**Status:** Complete (2026-10-23). Tested in-game; legendary not confirmed (the solver doesn't use it).

## Overview

Two small changes. The mod reads `CountUses.glassed` (and the legendary slot) and writes them next to `uses`. `GameLink.Parse` clamps a glass button's charge to 1 before it builds `ButtonsText`. Everything after the Buttons box already works on charges, so the searches don't change.

## Mod: `GameHook.MelonLoader/CalcHookCore.cs`

### `ButtonInfo`

Add two fields:

```csharp
public bool Glass;
public bool Legendary;
```

### `ReadButtons`

```csharp
Glass = cu.glassed,
Legendary = IsLegendary(cu),
```

```csharp
// Legendary slots from Descriptionframe.UpdateItemHints: near, cut, equal, complement, mediana.
static bool IsLegendary(CountUses cu)
{
    var info = cu.GetComponent<Infos>();
    if (info == null || info.passive) return false;
    int id = info.id;
    return id == 10 || id == 11 || id == 28 || id == 46 || id == 47;
}
```

Only fields are read (`glassed`, `Infos.id`, `Infos.passive`). No game methods are called.

### Fingerprint (`CheckPuzzle`)

Append the glass flag per button: `b.Key:b.Uses` becomes `b.Key:b.Uses:g` (`g` only when glass), so a glass change counts as a change.

### `WritePuzzle`

Each button line becomes:

```json
{ "id": "operator_x1", "label": "X1|4", "uses": 4, "glass": true, "legendary": false }
```

`version` stays 1.

### `WriteJson` (`state.json`) and the log line (`Poll`)

- `state.json`: the same two fields after `uses`.
- Log: `buttons=[operator_x1:4g, number10:13]`: a `g` suffix for glass, `L` for legendary.

## App: `CalculatorPuzzle/GameLink.cs`

### `GamePuzzle`

Add `IReadOnlyList<string> Glass` (solver tokens of the glass buttons that map), after `Ignored`:

```csharp
public sealed record GamePuzzle(int Seq, string Time, int Round, long Start, long Goal,
                                string ButtonsText, IReadOnlyList<string> Ignored, IReadOnlyList<string> Glass);
```

### `Parse`

```csharp
int uses = button.GetProperty("uses").GetInt32();
bool glass = button.TryGetProperty("glass", out var g) && g.ValueKind == JsonValueKind.True;
if (MapButton(id) is { } token)
{
    // Glass breaks after one press, whatever the charge count says.
    tokens.Add($"{token}:{(glass ? Math.Min(uses, 1) : uses)}");
    if (glass) glassTokens.Add(token);
}
```

A missing `glass` (old mod) means `false`. A `glass` that isn't a bool also means `false` (no new null case). `legendary` is not read.

## App: `CalculatorPuzzle/MainForm.cs`

Where the game status is built (`Game: round R, S → G` plus ignored buttons), add `Glass: X1, ÷.` when `puzzle.Glass` isn't empty. Follow the existing wording for the ignored list.

## Tests (`CalculatorPuzzle.Tests/GameLinkTests.cs`)

| Test | Input | Expect |
|------|-------|--------|
| `Parse_clamps_glass_to_one_charge` | `operator_x1`, uses 4, glass true | `ButtonsText` = `X1:1`, `Glass` = [`X1`] |
| `Parse_keeps_normal_charges` | uses 4, glass false | `X1:4`, `Glass` empty |
| `Parse_without_glass_field_is_not_glass` | uses 4, no `glass` | `X1:4` |
| `Parse_keeps_zero_for_glass` | uses 0, glass true | `X1:0` |
| `Parse_ignores_unmapped_glass` | `operator_prime`, glass true | in `Ignored`, not in `Glass` |
| `Parse_reads_the_sample` (update) | add `glass`/`legendary` to the sample | still parses; existing asserts unchanged |

Plus one end-to-end check: parse a puzzle with `÷` glass at uses 3, feed `ButtonsText` to `Buttons.Parse`, and assert `Fits` rejects a press list with two `÷`.

Fix the existing callers of the `GamePuzzle` constructor (if any test builds one by hand).

## Manual test plan (`user-verification/test-d.md`, new "Test D: glass buttons")

1. Rebuild and reinstall the mod, start the app with **Follow game**.
2. **D1.** Get a glass button (buy one when the shop offers glass). Check `puzzle.json`: that button has `"glass": true` and its raw `uses`. The app's Buttons box shows it at `:1`, and the status lists it under `Glass:`.
3. **D2.** Press the glass button once in-game. It breaks. No second publish happens (same round and goal).
4. **D3.** (Answered by the user: a glass operator applies its operation, then breaks. No test needed.)
5. **D4.** If a legendary button shows up, check `"legendary": true` in `puzzle.json`.
6. Report any `error in` lines from `MelonLoader\Latest.log`.

## Risks

- `Infos` may be missing on number buttons (`CanBecomeGlass` handles `Infos == null` for `number_tag`). `IsLegendary` returns false then; `Glass` doesn't need `Infos`.
- A glass button breaking hides it after 0.5 s (`activeInHierarchy` false), so `ReadButtons` drops it. That's already the right outcome and needs nothing new.

## As built

- Built as designed. `GamePuzzle.Glass` and the clamp are in `GameLink.Parse`; the mod writes `glass` and `legendary` in `puzzle.json` and `state.json`, marks `g` / `L` in the log, and has the glass flag in the fingerprint.
- The `Glass: …` status text was built, then removed on request (it widened the window). `GamePuzzle.Glass` stays; `MainForm` doesn't show it.
- Tests: 6 new in `GameLinkTests` (308 in all), including the `Fits` check.
- In-game (2026-10-23): `operator_sqr` with `uses` 2 and `glass: true` parsed as `sqr:1`; every other button kept its raw charge. Legendary was not seen, so `"legendary": true` is untested. Nothing depends on it.
