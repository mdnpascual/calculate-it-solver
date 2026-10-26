# Button labels: requirements

## Goal

The solver app and the mod overlays show solver tokens (`c12`, `flip`, `sqr`, `money`, …). Several of them don't look like the label on the in-game button, so a line from the Lines box or a cell in the overlay is hard to match to the button on screen. While auditing the tokens, the user listed the in-game labels to use instead.

1. Show the in-game label (table below) everywhere a person reads a button: the solver app and both mod overlays.
2. Keep the solver's internal tokens unchanged (search, simulator, `LeftTransforms`, tests). The labels are a display layer.
3. The Buttons box and the Required box accept both the label and the old token, so old text and tests keep working.

## Labels

| Game id | Token now | Label | Notes |
|---------|-----------|-------|-------|
| `number_charge` | `c<n>` (`c12`), bare `c` | `volt<n>` (`volt12`), bare `volt` | the value part stays live |
| `number_quest` | `q<d>` (`q8`), bare `q` | `unk<d>` (`unk8`), bare `unk` | |
| `number_mw` | `m<n>` (`m5`), empty `mw` | `mem<n>` (`mem5`), empty `mem` | |
| `operator_money` | `money` | `$` | |
| `operator_reverse` | `flip` | `reverse` | |
| `operator_round` | `round` | `->)` | the button label (the tooltip says "approximate") |
| `operator_sup` | `sup` | `combine` | |
| `operator_sqr` | `sqr` | `X²` (U+00B2) | only if `²` renders in both the WinForms box and the IMGUI overlay; otherwise keep `sqr` |
| `operator_mediana` | `median` | `_2_` | |
| `operator_flip` | `25\|69` | `mirror` | |
| `operator_equal` | `==` | `equalize` | |
| `operator_<` | `<<` | `X<<` | |
| `operator_>` | `>>` | `X>>` | |
| `operator_near` | `near` | `+-10` | |
| `operator_to0` | `reset` | `x->0` | same family as `to25` |
| `operator_to25` | `to25` | `x->25` | lowercase `x` (the `X<<`, `X>>`, `X²` labels are uppercase) |

Every other token keeps its current text as its label (`÷` still shows as `/` in the Lines box, `p8081` stays, unmapped ids still show `(name)` in the overlay).

## Where labels show

Solver app (`CalculatorPuzzle`):

- Lines box rows (each press, and the `no <token>` note).
- Buttons box text written by Follow game (`volt12:12, X²:3, $:1`).
- Required box text written by Follow game.
- Status notes that name a button: `use <token>`, `no line can use <token>`, `blocked: …`, `mw empty`.

Mod (`GameHook.MelonLoader`):

- Calculator overlay cells, including the live forms (`volt12`, `unk8`, `mem5`).
- Shop panel offers.

Not changed: `puzzle.json` / `state.json` (they carry game ids), the `MelonLoader` log line, the solver's internal token strings and test inputs.

## Input

- `Buttons.Parse` and the Required box take either form, case-insensitive like today: `volt12:12` = `c12:12`, `x²:3` = `sqr:3`, `$:1` = `money:1`, `reverse:2` = `flip:2`, `mirror:1` = `25|69:1`, `->):1` = `round:1`, `x->0:1` = `reset:1`.
- No label may equal a different button's token (checked by a test). `x->25`, `x->0` and `->)` can't clash with digit maps (`d->d`), `+-10` can't clash with `X+k` / `X-k`.

## Examples

- Line `c12 c11 ÷ =` (charge key) shows `volt12 volt11 / =`.
- Line `8X ÷ 10 = flip` shows `8X / 10 = reverse`.
- Overlay cell for `operator_sqr` shows `X²`; for `operator_equal` shows `equalize`.
- Follow game with `operator_money` (2 charges) writes `$:2` to the Buttons box; pressing Enter parses it as `money:2`.

## Decisions (2026-10-26)

1. `round` → `->)`, the button label (an earlier draft had `approx`, from the tooltip "approximate").
2. `reset` (`operator_to0`) → `x->0`, like `to25` → `x->25`.
3. The empty mw key shows `mem`.
4. Case as given: lowercase `x` in `x-><n>`, uppercase `X` in `X<<`, `X>>`, `X²`.

## Out of scope

- Renaming the internal tokens in code and tests.
- Labels for ids the solver doesn't map.
- Changing `docs/boss-rules.md` or old spec folders (they keep tokens; history).
