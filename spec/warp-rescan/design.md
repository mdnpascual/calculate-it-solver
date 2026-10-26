# Design: Warp re-scan

**Status:** Implemented 2026-10-25; in-game complete; Test S done, S4 and S5 (boss rules 14 and 2) skipped. The `Game: round` status prefix no longer exists in `MainForm`, so the warp shows as a `warp n` note in the done status instead.

Small change in two places: a second Harmony postfix in the mod and one field through to the app.

## 1. Mod: `GameHook.MelonLoader/`

### `Mod.cs`

Add a postfix next to `NextLvlPatch`:

```csharp
// Postfix only marks the puzzle pending after the warp button has rebuilt the calculator.
[HarmonyPatch(typeof(StartLvl), nameof(StartLvl.D20))]
static class WarpPatch
{
    static void Postfix(StartLvl __instance)
    {
        try { Mod.Core?.OnWarp(__instance); }
        catch { }
    }
}
```

MelonLoader applies every `[HarmonyPatch]` class in the assembly, so no other wiring.

### `CalcHookCore.cs`

- New field `int _warps;` (warps in this round).
- `OnRoundLoaded`: also set `_warps = 0`.
- New method:

```csharp
/// <summary>Called from the Harmony postfix on StartLvl.D20 (the warp button replaced every button).</summary>
public void OnWarp(StartLvl level)
{
    Safe("OnWarp", () =>
    {
        if (level != null) _level = level;
        _warps++;
        _pending = true;
        _fingerprint = null;
        _info(Tag + " warp " + _warps + ": buttons re-rolled");
    });
}
```

- `CheckPuzzle`: no change to the settle logic. `_pending = true` alone is enough, because the "is it new" test only ever *sets* `_pending`; the early `return` only happens when it is false. That also covers boss rule 14 (`goalMoves`), where the goal test is skipped: the warp sets `_pending` directly.
- `WritePuzzle` and the `state.json` writer: add `"warp": _warps` after `"last"`.
- The `puzzle ready` log line: append `, warp N` when `_warps > 0`.

Reading stays read-only: the postfix touches only mod fields.

## 2. App: `CalculatorPuzzle/`

### `GameLink.cs`

- `GamePuzzle` gets `int Warp` (default 0). `Parse` reads `warp` when present (a number ≥ 0), else 0.

### `MainForm.cs`

- Next to `appliedStepRound`, add `int appliedStepWarp`.
- In `OnGameFileChanged`, the rule-14 guard becomes:

```csharp
if (stepRule && appliedStepRound == puzzle.Round && appliedStepWarp == puzzle.Warp)
    return;
appliedStepRound = stepRule ? puzzle.Round : null;
appliedStepWarp = puzzle.Warp;
```

- Status prefix: when `Warp > 0`, `Game: round R, S → G, warp n.` (find where the `Game: round` prefix is built and add the part there).

Nothing else changes: a new `seq` already triggers `ApplyGamePuzzle` and a new search, which cancels the running one.

## Test plan

Unit tests (`CalculatorPuzzle.Tests/GameLinkTests.cs`):

1. `Parse` with `"warp": 2` gives `Warp == 2`.
2. `Parse` without `warp` gives 0.
3. `Parse` with `"warp": -1` or `"warp": "x"`: treat as 0 (same tolerance as other optional fields; follow whatever `Parse` does for `last`).

The rule-14 guard lives in `MainForm`. If it is easy, pull the decision into a small static helper (`GameLink.IsSameStepPuzzle(prev round, prev warp, puzzle)`) and test: same round same warp → skip; same round new warp → apply; new round → apply. Otherwise check it by hand with fake files.

By hand, no game (Follow game on):

- Write a `puzzle.json` with `bossRule` 14, round 3, `warp` 0, then the same with a new `seq` → ignored; then `warp` 1 → applied, status shows `warp 1`.

In game: Test S (see `NEXT_PROMPT.md`):

- **S1.** Press the warp in a normal round. Expected: one `warp 1: buttons re-rolled` and one `puzzle ready (…, warp 1)` line in `Latest.log`; `puzzle.json` has `warp` 1, the new buttons with `uses` 1, the warp's own `uses` one lower; the app's Buttons box matches the screen and a new search runs.
- **S2.** Press the warp again. Expected: `warp` 2, another solve.
- **S3.** Next round. Expected: `warp` 0.
- **S4.** (Boss rule 14, if it comes up.) Expected: the goal in the file is one higher than before the press, and the app solves again.
- **S5.** (Boss rule 2, if it comes up.) Expected: the two newly blocked buttons have `"blocked": true` and the status lists them.
- **S6.** Press the warp after an operator (for example `÷` then warp). Write down what the display shows and what the app solves from. This only records behaviour.
