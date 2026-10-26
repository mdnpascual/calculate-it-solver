# Design: Game state scan

**Status:** Complete (2026-10-22). See "As built" at the end.

Two parts. The mod decides *when* a puzzle is ready and writes it to a file. The app watches that file, maps the buttons and runs the search with charges. They only share a JSON file, so the app never loads game or MelonLoader code.

## 1. Mod: `GameHook.MelonLoader/`

### Files

- `CalcHookCore.cs`: add the settle logic and `puzzle.json`. Keep the existing change log and `state.json` as a debug feed.
- `Mod.cs`: no change except a call into the new code. `OnUpdate` already calls `Core.Tick()` every frame.

### Settle logic (in `CalcHookCore`)

Today `Tick` polls once a second. Split it:

- **Every frame (cheap):** read the round, goal, left side, `RunSave.Restoring` and `StartLvl.IsTransitioning`. The `StartLvl` and `MainCalc` references are cached; re-find them when null. Build a *fingerprint* string from these plus the button ids and charges. The button read is about 20 `GetComponentsInChildren` results under `calc_transform`; do it every frame only while a puzzle is pending (below), otherwise once every 0.25 s.
- **State machine:**
  - `lastPublished = (round, goal)` of the last published puzzle, or none.
  - When `(round, goal) != lastPublished`, set `pending = true`.
  - While pending, the fingerprint must be identical across frames for `SettleSeconds` (default 0.05, using `Time.realtimeSinceStartup`), with both flags false and left ≠ goal. Then publish and set `lastPublished`.
  - If the fingerprint changes, restart the settle timer.
- The 1-second change log stays as it is.

Read `RunSave.Restoring` through the static property (`RunSave.Restoring`, public static getter). If `RunSave.Instance` is null (for example in the menu), treat the game as not restoring.

### Publishing

Write `%LOCALAPPDATA%\CalcHook\puzzle.json` atomically (temp file, then replace; same helper as `state.json`):

```json
{
  "version": 1,
  "seq": 7,
  "time": "2026-10-22T15:14:49.0200000+02:00",
  "round": 3,
  "start": 64,
  "goal": 37,
  "calculator": "Calculator_Engineering",
  "scene": "Game",
  "buttons": [
    { "id": "operator_5x", "label": "5X", "uses": 2 },
    { "id": "number10", "label": "10|13", "uses": 13 }
  ]
}
```

- `seq` increments on each publish within a game session. The app uses (`seq`, `time`) to tell writes apart.
- Also log `[CalcHook] puzzle ready: round R, S -> G, N buttons`.

### Safety

Unchanged: every entry point is wrapped in try/catch, each distinct error is logged once, and the mod only reads fields. Add no new Harmony patches; the existing `NextLvl` postfix only resets the state machine (set `pending`).

## 2. App: `CalculatorPuzzle/`

### `EngineeringCalculator.cs`: charges on `Buttons`

- Add `IReadOnlyDictionary<string, int> Charges` to the `Buttons` record, keyed by the button token in `Press.ToString()` form (`"X1"`, `"4X"`, `"10"`, `"5"`, `"÷"`, `"flip"`, `"reset"`) plus `"mod"`. A missing key means unlimited. `Buttons.All` gets an empty map.
- `Buttons.Parse` accepts an optional `:n` after any token: `X1:2, 10:13, /:6`. `n` must be a whole number ≥ 0.
  - `n = 0` leaves the button out of the sets entirely, so the search never uses it.
  - `n > 0` adds it to the sets and records `Charges[token] = n`.
  - Normalise `/` to `÷` for the key.
- Add `bool Fits(IReadOnlyList<Press> presses)`: count presses per `ToString()` (skipping `=`) and check each count ≤ `Charges[token]` when present.

### `DivisionShortcut.cs`

- In `Search`, after a line passes the simulator check, call `buttons.Fits(line.Presses)`. Report it through `onLine` only if it fits, otherwise through a new optional `Action<Line>? onRejected`. Rejected lines still count toward deduplication, so the same line isn't checked twice.
- `Solve` passes no `onRejected`.

**Known limitation:** the search picks one cheapest edit path per candidate (`LeftEdits` BFS, `CheapestDivisor`). If that path is too expensive in charges, the line is rejected even when a different path that fits exists. Making `LeftEdits` charge-aware is a follow-up. Zero-charge buttons are already excluded up front, which covers the common case.

### New `GameLink.cs`

```csharp
public sealed record GamePuzzle(int Seq, int Round, long Start, long Goal,
                                string ButtonsText, IReadOnlyList<string> Ignored);

public static class GameLink
{
    public static string DefaultPath { get; }            // %LOCALAPPDATA%\CalcHook\puzzle.json
    public static string? MapButton(string id);          // table from requirements, null = ignored
    public static GamePuzzle? Parse(string json);        // System.Text.Json; null on bad/unknown version
}
```

- `MapButton` uses regexes `^operator_(\d)x$` → `dX`, `^operator_x(\d)$` → `Xd`, and `^number(\d|10)$`, plus the fixed names.
- `Parse` builds `ButtonsText` as `token:uses` joined by `, ` (for example `5X:2, X1:2, 10:13, ÷:6, flip:8, mod:8, reset:2`), so the Buttons box shows exactly what the search uses.

Watching the file:

- Use a `FileSystemWatcher` on the folder, filtered to `puzzle.json` (Changed, Created and Renamed, because an atomic replace shows up as Renamed).
- Watcher events are noisy, so don't act on them directly. Each event restarts a 100 ms WinForms timer. When it fires, read the file (retry up to 3 times on `IOException`, since the mod may still be replacing it), call `Parse`, and act only if `seq` or `time` differs from the last one handled.

### `MainForm.cs`

- Add a `Follow game` checkbox (checked) on the Division Shortcut row, plus the watcher and timer above.
- On a new `GamePuzzle`:
  1. Set `iteratorBox` to `1`, then `startBox`, `goalBox` and `buttonsBox`. Their `TextChanged` handlers run as usual: the Modulo Shortcut recalculates, and k resets to S's digit count.
  2. Call `StartDivisionSearch()`, which already cancels any running search.
  3. The status prefix becomes `Game: round R, S → G. Ignored: prime.`, followed by the normal search status.
- In `StartDivisionSearch`, pass `onRejected` to count rejected lines (`Interlocked.Increment`). The final status gets `, K rejected for charges` when K > 0.
- Unchecking `Follow game` stops the watcher. Re-checking it reads the current file once.

## Test plan

Unit tests in `CalculatorPuzzle.Tests/`:

- **`ButtonsParseTests`:**
  - `X1:2` gives `AppendDigits` ∋ 1 and `Charges["X1"] == 2`.
  - `X1:0` leaves 1 out of `AppendDigits`.
  - `/:6` gives key `÷`.
  - `X1:-1` and `X1:a` give an error.
  - A token without `:` has no charge entry.
- **`FitsTests`:** the press list `X2 flip` fits `X2:1, flip:1`. `X1 X1` doesn't fit `X1:1`. `=` is never counted.
- **`DivisionShortcutTests`:**
  - 41 → 48 with `X8:1, 10:1, ÷:1`: the top line is `÷ 10 = X8`.
  - 41 → 48 with `X8:0` and the other buttons unlimited: no line contains `X8`.
  - 4 → 24 with the logged charges: every reported line `Fits`.
  - A case where the cheapest line needs a button twice and the charge is 1: that line goes to `onRejected` and is not reported.
- **`GameLinkTests`:**
  - `MapButton` for every row of the table, plus `operator_prime` → null.
  - `Parse` on the sample JSON above gives the expected `ButtonsText` and `Ignored`.
  - `Parse` on version 2 or broken JSON gives null.

Manual (MelonLoader, see `user-verification/`):

1. **New run:** exactly one `puzzle ready` line per round. The app fills in S, G and Buttons and starts searching once per round. Presses and purchases don't trigger another search.
2. **Continue:** only the restored puzzle is published. If the fresh one also shows up, note the gap between the two `puzzle ready` lines; that tells us whether 50 ms or the flags need adjusting.
3. **Reroll:** a new goal in the same round publishes again.
4. **Charges:** in a puzzle where a button is at 0 or 1 charge, no line in the app needs more than that.

## As built

Implemented as designed, with these differences and additions:

- **`GamePuzzle` has a `Time` field** next to `Seq`. The app skips a file only when both `Seq` and `Time` match the last one handled, so a new game session (where `seq` restarts at 1) still counts as new.
- **Charges:** `Buttons` stores the map as `Charges` (constructor parameter `ChargeMap`). `Fits` skips `=` and treats a missing key as unlimited. `:0` removes the button from the sets.
- **`DivisionShortcut.Search`** got `onRejected`. The `Fits` check runs inside the same lock as `onLine`, so both callbacks stay single-threaded.
- **Mod:** `CalcHookCore.CheckPuzzle` runs every frame (`Tick`). Fingerprint = round, goal, left side, and each button's id and charges. It publishes after 50 ms (`SettleSeconds`) with `RunSave.Restoring` and `StartLvl.IsTransitioning` false and left ≠ goal. `RunSave.Restoring` is read in a try/catch (a null `RunSave` counts as not restoring). `NextLvl`'s postfix only sets `pending`. Both JSON files go through one `WriteAtomic` helper.
- **App:** `Follow game` and `Lock` sit on one row. The watcher only restarts a 100 ms one-shot timer (no polling). The file is read with `FileShare.ReadWrite | Delete` and retried on `IOException`. The app creates `%LOCALAPPDATA%\CalcHook\` if it is missing and reads an existing `puzzle.json` once at startup.
- **Lock (not in the original design):** while checked, the app ignores `puzzle.json`: the boxes and the search stay as they are. Unchecking re-reads the file and applies it even if it was already handled, the same as re-ticking `Follow game`. It does nothing while `Follow game` is off.
- **Tests:** `ChargesTests.cs` (`ButtonsChargesParseTests`, `FitsTests`, `DivisionShortcutChargesTests`) and `GameLinkTests.cs`. 151 tests pass.
- **Checked by hand:** with no game running, a hand-written `puzzle.json` fills the boxes, A resets to 1, the same `seq` is ignored, a new `seq` restarts the search, and Lock behaves as described. In the game: see `user-verification/` Test C.
- **Why a file and not a pipe:** the mod (net472, inside Unity's Mono) and the app (.NET 9) share nothing, and a file keeps the mod tiny and unable to block the game. It also works whichever side starts first and can be faked by hand for testing. A named pipe would only pay off for two-way traffic, which is out of scope (the mod must stay read-only).
- **Known limitation, unchanged:** the search still picks one cheapest edit path per candidate, so a line can be rejected for charges even when another path would fit.
