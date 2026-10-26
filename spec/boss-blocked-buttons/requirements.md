# Requirements: Boss rules 2 and 9, blocked buttons

**Status:** Complete (2026-10-25). Test P passed in game.

## Why

Boss rule 2 (see [`docs/boss-rules.md`](../../docs/boss-rules.md)) shows ``You can`t use buttons "<A>" and "<B>"``. Two buttons can't be pressed for the whole round, but they still have charges. Today the mod publishes them with their charges, the app puts them in the Buttons box (`X8:3`), and the solver happily uses them. The top line can then be a line the player can't press. The status only says `boss rule 2 not supported`.

Boss rule 9 (``You can't use legendary buttons``) blocks buttons the same way (same flag), so this spec covers it too.

## Game facts (static analysis, `Assembly-CSharp.dll`, ilspycmd 9.1, 2026-10-25)

| What | Where | Detail |
|------|-------|--------|
| Rule active | `StartLvl.NextLvl`, `rule == 2` (also 1, 3, 4, never picked) | Only in a boss round. Left out of challenge 8 (Market Demand), so never together with a Market Demand required button. One boss rule per round, so never with rule 11. |
| Which buttons | `StartLvl.rules_tempus[0]`, `[1]` | A = random `number_tag` object (`nums`), or a random `operator_tag` object (`buttons`) when there is no number key. B = random `operator_tag` object. If A == B, A is re-picked from `buttons` up to 3 times (so A == B is still possible, with 1 blocked button). |
| How it blocks | `Blockirator.inf_blocked = true` on both | `Blockirator.Update`: with `inf_blocked`, the `Button` is disabled and the `blocked` cover is shown. The press can't happen. |
| Charges | `CountUses.uses` unchanged | The mod sees normal `uses` (for example 3). |
| Stays blocked | `CountUses.AddUses` | Adding charges clears `inf_blocked`, **except** for a `rules_tempus` button in a rule 1–4 boss round. So a shop purchase or a charge refund doesn't unblock it. |
| End of round | `StartLvl.ResetPreviousBossEffects` → `ReleaseBossBlock` | `inf_blocked = uses <= 0` (blocked again only if empty). |
| Mid-round re-roll | `StartLvl.D20` → `Reblock` | The d20 button rebuilds the calculator and picks two new blocked buttons. Rare; see Out of scope. |

| Rule 9 | `StartLvl.NextLvl`, `rule == 9` | Sets `inf_blocked` on `operator_complement`, `operator_mediana`, `operator_cut`, `operator_equal`, `operator_near` (`Infos.id` 46, 47, 11, 28, 10). Released by `ResetPreviousBossEffects` like rule 2. Left out of challenge 8. |

`inf_blocked` is the general "this button can't be pressed" flag. The game also sets it on a button with 0 charges and on one random basic operator in difficulty type 8 (`diff9blocked`).

## What the solver must do

1. **The mod publishes the block.** Each button in `puzzle.json` and `state.json` gets `"blocked": true|false`, read from `Blockirator.inf_blocked` (false when the button has no `Blockirator`). It is read once when the puzzle settles, like `uses`. The mod stays read-only.
2. **A blocked button is not available.** `GameLink.Parse` writes a blocked button with `:0` (left out, like an empty button) and lists its token in a new `GamePuzzle.Blocked`. The Buttons box shows `X8:0`, so the user can see it and can still edit it by hand. Unmapped blocked ids are ignored like today (they are in `Ignored`).
3. **The general flag, any round.** The app trusts `blocked` in every round, not only in rule 2. A blocked button can't be pressed whatever the reason (rule 2, rule 9, difficulty 8). Glass and blocked together: blocked wins (`:0`).
4. **Status.** When the applied puzzle has blocked buttons with charges, the status gets the note `blocked: X8, ÷` (tokens in file order, written as in the Buttons box, like the other notes). Rules 2 and 9 are removed from the `boss rule N not supported` list. A blocked button with 0 charges isn't listed (it was empty anyway).
5. **Rule 9.** The five legendary buttons aren't mapped by the solver today (`GameLink.MapButton` gives null, so they go to `Ignored` and are never used). So rule 9 needs nothing more than the flag: its blocked buttons stay in `Ignored` and don't appear in the `blocked:` note. If a legendary button is mapped later, the flag already keeps it out in rule 9.
6. **Typed by hand.** With Boss 2 typed by hand and no game feed, the app doesn't know the blocked buttons: the user writes them as `:0`. No note, normal search.
7. **Older mod.** No `blocked` field → not blocked (today's behaviour).

## Examples

S 41, G 48, rule 2 blocks `X8` and `5`.

| `puzzle.json` buttons | Buttons box | Top line |
|-----------------------|-------------|----------|
| `operator/` 3, `number10` 4, `operator_x8` 2 **blocked**, `operator_8x` 2, `operator_reverse` 1 | `÷:3, 10:4, X8:0, 8X:2, flip:1` | `8X ÷ 10 = flip` (4 presses), not `÷ 10 = X8` (3 presses) |
| same, `X8` not blocked | `÷:3, 10:4, X8:2, 8X:2, flip:1` | `÷ 10 = X8` |

Status for the first row ends with `blocked: X8`.

## Out of scope

- The d20 re-roll in the middle of a round (`Reblock`): followed since `spec/warp-rescan/`: the mod publishes again after the warp, with the new blocked pair.
- Bomb mode (`StartLvl.destroing`) clears `inf_blocked` on every button for a moment; a puzzle published at that moment would miss the block. Not seen in play.
- Showing blocked buttons in the calculator overlay (`redacted-overlay/`). The game already shows its own `blocked` cover.

## Assumptions (decided 2026-10-25)

- Rule 2 can block any button except `=`. A round without `=` can't be won, so `=` is never blocked.
- That includes the charge key `c` and the quest key `q`: they are handled like any other blocked button (`c:0`, `q<digit>:0`).
