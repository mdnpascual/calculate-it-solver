# Design: Boss rules 2 and 9, blocked buttons

**Status:** Complete (2026-10-25). Test P passed in game.

## Overview

| Part | Change |
|------|--------|
| Mod (`GameHook.MelonLoader/CalcHookCore.cs`) | `ButtonInfo.Blocked` from `Blockirator.inf_blocked`; `"blocked"` per button in `puzzle.json` and `state.json` |
| `GameLink.cs` | a blocked button is written `:0`; new `GamePuzzle.Blocked` (tokens of blocked buttons that had charges) |
| `MainForm.cs` | keeps the applied puzzle's blocked list; adds the `blocked: …` note; rules 2 and 9 leave the "not supported" list |

The solver (`Buttons`, the simulator and all search phases) doesn't change: a `:0` button is already left out.

## Mod

`ReadButtons`: add

```csharp
Blocked = cu.TryGetComponent<Blockirator>(out var block) && block.inf_blocked,
```

and a `bool Blocked` field on `ButtonInfo`. `WritePuzzle` and `WriteJson` append `, "blocked": true|false` right after `"glass"` (same style). Nothing else: the settle check doesn't need it (rule 2 sets the block in `NextLvl`, before the puzzle settles), and the publish trigger stays (round, goal).

Update `GameHook.MelonLoader/README.md` ("Per button": add `blocked`).

## GameLink

In the button loop of `Parse`:

```csharp
bool blocked = button.TryGetProperty("blocked", out var bl) && bl.ValueKind == JsonValueKind.True;
```

For a mapped token:

- `blocked` → the charge is 0, with no `g` (`X8:0`, `c:0`, `q<digit>:0`; `q` with no digit stays in `Ignored` as today). If `uses > 0`, add the token to `blocked` list.
- not blocked → today's code.

Put the check before the glass count is built, so blocked + glass gives `:0` and the token is not added to `Glass`.

`GamePuzzle` gets `IReadOnlyList<string>? Blocked = null` as the last parameter (existing calls still compile); `Parse` passes the list (empty when none). Update the record's doc comment.

## MainForm

- New field `IReadOnlyList<string> gameBlocked = []`. `ApplyGamePuzzle` sets it to `puzzle.Blocked ?? []`.
- A hand edit must not keep a stale note: in `StartDivisionSearch`, use the list only when every listed token still has charge 0 in the parsed `buttons` (`buttons.Charges.GetValueOrDefault(t) <= 0` — a `:0` token is left out, so it's simply absent). If the user typed a charge back for a token, drop that token from the note.
- Note: when the list is not empty, add `blocked: {string.Join(", ", list)}` to `requiredNote`, the same way `no Last value, last ignored` is added.
- Change `if (bossRule is 2 or 6 or 9)` to `if (bossRule is 6)` (or remove the line if `boss-double-charge/` is done first).

## Tests (`CalculatorPuzzle.Tests/GameLinkTests.cs`, or a new `BlockedButtonTests.cs`)

1. `blocked: true`, `uses: 2`, `operator_x8` → `ButtonsText` has `X8:0`; `Blocked` = [`X8`].
2. `blocked: true`, `uses: 0` → `X8:0`; `Blocked` is empty.
3. `blocked: false` and missing `blocked` → `X8:2` (today's text); `Blocked` empty.
4. Blocked glass (`glass: true`, `uses: 1`, `blocked: true`) → `X8:0` (no `g`); not in `Glass`; in `Blocked`.
5. Blocked charge key (`number_charge`, `uses: 12`) → `c:0`; `Blocked` = [`c`].
6. Blocked unmapped id (`operator_near`) → still in `Ignored`, not in `Blocked`.
7. End to end: parse a rule-2 file (S 41, G 48, `÷:3, 10:4, X8` blocked, `8X:2`, `flip:1`), `Buttons.Parse(puzzle.ButtonsText)`, `DivisionShortcut.Solve` → no line presses `X8`; the best line costs 4 presses (for example `8X ÷ 10 = flip`). The same file without `blocked` gives `÷ 10 = X8` (3 presses).

The note and the hand-edit rule live in `MainForm` (no UI tests); Test P checks them.

## Manual test

Test P (`user-verification/test-p.md`), challenge 4 (every round is a boss) until the text reads ``You can`t use buttons "…" and "…"``:

- P1: `puzzle.json` has `"blocked": true` on the two named buttons (and false on the others with charges). Write down the two names.
- P2: the app's Buttons box shows both as `:0`; the status ends with `blocked: <A>, <B>`, and has no `boss rule 2 not supported`.
- P3: the top line doesn't press A or B and wins the round.
- P4: next round (not a boss): no `blocked` note; the two buttons are back with their charges.
- P5 (rule 9, if a round reads ``You can't use legendary buttons``; easier with a legendary button bought): the Boss box shows 9, no `boss rule 9 not supported` note, `puzzle.json` has `"blocked": true` on the legendary buttons, and the top line wins the round.
