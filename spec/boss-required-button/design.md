# Design: Boss round required button (rule 11)

**Status:** Complete (2026-10-24). Tested in-game, Test K (challenge 4): K1–K5 confirmed.

## Overview

`RequiredKey` in the mod gets a second source: the boss rule's `must_butt`. It returns the id and the kind; `puzzle.json` / `state.json` add `requiredKind`. The app reads the kind; in boss mode it keeps only lines that press the button (filters passed into the searches, so their line limits count only kept lines) and, when the button is an operator, runs only the phase that uses that operator.

## Mod: `GameHook.MelonLoader/CalcHookCore.cs`

### Constants

```csharp
// Boss rule 11: "You must use button ..., otherwise the answer will not be counted." Public fields, never in challenge 8.
const int MustUseRule = 11;
```

### `RequiredKey`

Change the signature to return the kind too:

```csharp
/// <summary>
/// The id (as in buttons[].id) of the button the round requires, and why: "market" (Market Demand, challenge 8)
/// or "boss" (boss rule 11, StartLvl.must_butt). Null when neither applies or no active button has that name.
/// </summary>
string RequiredKey(List<ButtonInfo> buttons, out string kind)
{
    kind = null;
    string name = null, k = null;
    if (Challenge() == MarketDemand)
    {
        name = ReadRequired<string>(RequiredNameField);
        k = "market";
    }
    else if (_level.IsBossRound && _level.rule == MustUseRule && _level.must_butt != null)
    {
        name = _level.must_butt.name;
        k = "boss";
    }
    if (string.IsNullOrEmpty(name)) return null;
    foreach (var b in buttons)
        if (b.ObjectName == name) { kind = k; return b.Key; }
    return null;
}
```

- `_level.must_butt != null` uses Unity's null check (a destroyed object counts as null), same as the game.
- `IsBossRound` is a getter over `round` and `GamePrefs.GetInt("challenge")`: a read. It can throw in the menu like `Challenge()`; `CheckPuzzle` / `Poll` already run inside `Safe`.

### `CheckPuzzle`

```csharp
string kind;
string required = RequiredKey(buttons, out kind);
fp.Append("|req:").Append(required ?? "-").Append(':').Append(kind ?? "-");
...
WritePuzzle(round, left, goal, buttons, required, kind);
_info(... + (required != null ? ", required " + required + (kind == "boss" ? " (boss)" : "") : ""));
```

### `WritePuzzle`

After `required`:

```json
  "requiredKind": "boss",
```

(`Str(kind)`, so null writes `null`.)

### `Poll` / `WriteJson`

- Log: ` required=<id>` plus ` (boss)` for a boss pick.
- `state.json`: `requiredKind`, `bossRule` (`_level.IsBossRound ? _level.rule : 0`), `mustUsed` (`_level.must_butted`). Keep `requiredLabel` / `requiredUses` (Market Demand diagnostics; null outside challenge 8).

## App

### `CalculatorPuzzle/GameLink.cs`

- `GamePuzzle`: add `string? RequiredKind = null` at the end; update the doc comment ("Required is the Market Demand or boss rule-11 button …; RequiredKind is "market", "boss" or null").
- `Parse`: `requiredKind` when it is a non-empty string, else null. Only kept when `required` is set.

### `CalculatorPuzzle/ArithmeticSearch.cs` and `ModuloSearch.cs`

Add an optional filter to both `Find` methods, applied **before** a line is kept, so the 100-line limit counts only accepted lines:

```csharp
public static IReadOnlyList<Line> Find(long start, long goal, Buttons buttons, int maxCost,
                                       CancellationToken cancel = default, int maxLines = MaxLines,
                                       Func<IReadOnlyList<Press>, bool>? accept = null)
```

(`ModuloSearch.Find` likewise, after `maxA`.) Null keeps today's behavior.

`ArithmeticSearch.Find` also gets `IReadOnlyCollection<Key>? onlyOps = null`: when set, `ops` and `remorphs` are filtered to it and the edits-only lines are skipped. (Edits-only lines have no operator, so they can't press an operator button.)

`DivisionShortcut.Search` needs no change: `MainForm` filters in its `onLine` callback (it keeps no line limit of its own).

### `CalculatorPuzzle/MainForm.cs`

- New fields `string? gameRequiredKind` and `string? gameRequiredToken`, set in `ApplyGamePuzzle` from the puzzle (`RequiredKind`, `Required`).
- In `StartDivisionSearch`, after the Required box is read:

```csharp
bool boss = gameRequiredKind == "boss" && requiredBox.Text.Trim() == (gameRequiredToken ?? "");
```

  A hand edit makes the text differ, so boss mode turns off.
- **Boss with no usable token** (`searchRequired` null and `requiredNote` set: unmapped, or not in Buttons): boss mode is off for this search (`boss = false`), so it runs exactly as with an empty Required box. The status keeps `requiredNote`, as today.
- **Boss with a usable token** (`searchRequired` set):

```csharp
Func<IReadOnlyList<Press>, bool>? accept = boss ? p => Buttons.Uses(p, searchRequired) : null;
var opKey = boss ? OperatorKey(searchRequired) : null;   // Key for ÷ mod + - × % a->c b->c, else null
bool runArithmetic = opKey is null || opKey is Key.Add or Key.Subtract or Key.Multiply or Key.Percent or Key.RemorphA or Key.RemorphB;
bool runDivision   = opKey is null || opKey == Key.Divide;
bool runModulo     = opKey is null || opKey == Key.Mod;
```

  - Arithmetic: `ArithmeticSearch.Find(..., accept: accept, onlyOps: opKey is { } k ? [k] : null)` when `runArithmetic`.
  - Division: when `runDivision`, the `onLine` callback drops lines with `accept(l.Presses) == false` before `ResultLine.From`.
  - Modulo: when `runModulo`, `ModuloSearch.Find(..., accept: accept)`. With `opKey == Key.Mod` the arithmetic and division phases didn't run, so the cap is the no-`÷` cap (12), as today when the division phase can't run.
  - `OperatorKey(token)`: map through the same tokens `Buttons.Parse` uses (`÷`, `mod`, `+`, `-`, `×`, `%`, `a->c`, `b->c`); put it next to `Buttons.Token` in `EngineeringCalculator.cs` as `Buttons.OperatorKey(string token)`.
- Every phase skipped by the shortcut behaves like a phase that found nothing (the status counts stay 0).
- `DoneStatus`: the status is unchanged in boss mode (`use {token}`); a "(boss: other lines don't count)" note was removed after Test K. With every line accepted, `K` is the number of lines shown.
- Market Demand and no-required searches pass `accept: null`, `onlyOps: null` and run every phase: unchanged.

Put the phase decision in a small static helper (for example `RequiredPlan.For(string? token, bool boss)` returning `accept`, `onlyOps`, and the three run flags) so it can be unit tested without the form.

## Tests

### `CalculatorPuzzle.Tests/GameLinkTests.cs`

| Test | Input | Expect |
|------|-------|--------|
| `Parse_reads_boss_kind` | `"required": "operator_reverse", "requiredKind": "boss"` | `Required` `flip`, `RequiredKind` `boss` |
| `Parse_reads_market_kind` | `"required": "operator/", "requiredKind": "market"` | `÷`, `market` |
| `Parse_without_kind` | `"required": "operator/"` (current mod) | `÷`, null |
| `Parse_kind_without_required` | `"required": null, "requiredKind": "boss"` | both null |

### `RequiredPlanTests` (new)

| Test | Check |
|------|-------|
| `Market_runs_everything` | kind `market`, any token: no filter, all phases |
| `Boss_operator_runs_one_phase` | `÷` → division only; `mod` → modulo only; `+` → arithmetic only with `onlyOps = [Add]`; `%`, `a->c` likewise |
| `Boss_left_button_filters` | `flip` → all phases, `accept` true only for press lists containing `flip` |
| `Boss_unsupported_runs_everything` | boss with no usable token (null) → no filter, all phases |

### Search filters

- `ArithmeticSearch.Find` with `accept` keeps up to `maxLines` **accepted** lines: build a case with more than `maxLines` cheap lines without the button and one dearer line with it, and assert that line is returned. (Use a small `maxLines`, such as 3.)
- `ArithmeticSearch.Find` with `onlyOps = [Multiply]` returns only `×` lines and no edits-only line.
- `ModuloSearch.Find` with `accept` returns only lines that pass it.

### End to end (no UI)

41 → 48, Buttons `÷:3, 10:13, X8:2, 8X:2, flip:2`, boss `flip`: every line from the three phases (filtered) contains `flip`, and `8X ÷ 10 = flip` is among them.

The mod part has no unit tests (it needs the game); Test K covers it.

## Manual test plan (`user-verification/test-k.md`, new "Test K: Boss required button")

1. Rebuild and reinstall the mod; start the app with Follow game.
2. **K1.** Play a normal run (not challenge 8) to a boss round (round 5, 10, 15, 20). When the rule text says `You must use button "…"`, check `puzzle.json`: `required` is that button's id and `requiredKind` is `boss`. The Required box shows its token; every line shown presses it; only lines pressing it appear.
3. **K2.** Follow a line shown (then `=`): the round ends. Optional: reaching the goal without the button doesn't end it (confirms the penalty).
4. **K3.** A boss round with another rule: `required` is null, `bossRule` in `state.json` shows the rule number.
5. **K4.** Challenge 8 still gives `requiredKind` `market` (Test E unchanged).
6. **K5.** The round after the boss: `required` is null again.
7. Report any `error in` lines from `MelonLoader\Latest.log`.

## Risks

- The required button can be a glass button with one charge (`uses != 0` is the only check). The solver already clamps glass to 1 charge, so lines can still press it once.
- Many rule-11 picks are left-side buttons the solver can't build lines around yet (same as Market Demand). In a boss round the list is then empty (`0 use <token>`), where Market Demand would still show lines. The real fix is required-aware left search (out of scope, follow-up).
- With a left-side button, the searches still do all their work and only drop lines at the end, so a boss search takes as long as a normal one. Only operator buttons get the phase shortcut.

## As built (2026-10-24)

As designed, with these points:

- `RequiredPlan.For(token, boss)` (`RequiredPlan.cs`) returns `Accept`, `OnlyOps` and the three run flags. `Buttons.OperatorKey` is next to `Buttons.Token`.
- `MainForm` keeps `gameRequiredKind` / `gameRequiredToken` (set in `ApplyGamePuzzle`) and `searchBoss` / `searchPlan` (set in `StartDivisionSearch`). Boss mode needs a usable token, so an unmapped or missing button runs a normal search. When the division phase is skipped the search task returns null; when the modulo phase is skipped the tick finishes the search.
- The status has no boss note: it reads like Market Demand (`K use <token>`). In-game, `K` showed 100, the line limit, not the true number of lines.
- Mod: `RequiredKey(buttons, out kind)`; kind is in the fingerprint; `requiredKind` in both JSON files; `bossRule` and `mustUsed` only in `state.json`.
- Tests: `BossRequiredTests.cs` (17 cases; 500 in all).
- Test K (challenge 4 rolls rule 11 often): K1 `required` + `boss` and filtered lines; K2 following a line ended the round; K3 another rule gave `required` null and `bossRule` 6; K4 challenge 8 gave `market`; K5 the next round gave null.
- Seen in-game: a required button the solver doesn't know (`energy`) gives `no line can use energy` and a normal search (rule 15).
