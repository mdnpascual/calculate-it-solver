# Design: More buttons

**Status:** Complete (2026-10-24). See "As built" at the end.

**Corrected 2026-10-25:** the 10^18 checks below don't match the game. It clamps `+`, `×` and some transforms at 999 999 (not `%`), and an operand key appends only while the operand has fewer than 5 characters. See [`display-cap/`](../display-cap/).

## Overview

| Part | File | Change |
|------|------|--------|
| Keys and presses | `EngineeringCalculator.cs` | `Key.Charge`, `Key.Quest`, `Key.Percent`, `Key.RemorphA`, `Key.RemorphB`; `Press.ChargeKey` |
| Buttons | `EngineeringCalculator.cs` | `ChargeStart`, `QuestDigit`, `Percent`, `RemorphA`, `RemorphB`, `LastIgnored`; `Parse(text, last)`; `Fits` / `Spare` / `Drained` / `Uses` / `Token` by `ChargeKey` |
| Simulator | `EngineeringCalculator.cs` | Charge and quest keys, `%`, remorph, in `Run` |
| Transforms | `LeftTransforms.cs` | `100-X`, `25\|69` in the catalogue; `LeftTransforms.Last(value)` |
| Operand typing | `DivisionShortcut.cs` | `TypeOperand` and `CheapestDivisor` learn the charge and quest keys; `CheapestDivisor` takes a minimum |
| Arithmetic phase | `ArithmeticSearch.cs` | `%` lines, `a->c` / `b->c` lines |
| Division phase | `DivisionShortcut.cs` | Runs when only charge or quest keys can type an operand |
| Game feed | `GameLink.cs` | `MapButton` ids; `Parse` writes `cV:n` / `qD:n`; `GamePuzzle.Last` |
| UI | `MainForm.cs` | Last box (below Required); status note when `last` is ignored |
| Mod | `CalcHookCore.cs`, `ButtonSymbols.cs` | `last` and `digit` in both JSON files; live `c12` / `q8` overlay cells; new tokens |

## 1. Keys and `Press`

```csharp
public enum Key { Prepend, Append, Ten, Digit, Charge, Quest, Reset, Flip, Transform,
                  Divide, Mod, Add, Subtract, Multiply, Percent, RemorphA, RemorphB, Equals }
```

- `Press(Key.Charge, value)`: `Digit` holds the **value typed** (it can be 10 or more). `Press(Key.Quest, digit)`: `Digit` is the quest digit.
- `ToString`: `c{Digit}`, `q{Digit}`, `%`, `a->c`, `b->c`. Lines show this text (requirements R4).
- New `public string ChargeKey => Key switch { Key.Charge => "c", Key.Quest => "q", _ => ToString() };`. It's the key into `Buttons.Charges`.
- A "right-side key" means `Ten`, `Digit`, `Charge` or `Quest`. Add a helper `static bool IsOperandKey(Key k)` and use it everywhere that checks for `Ten`/`Digit` today.

## 2. `Buttons`

New optional record parameters (defaults keep every existing call compiling):

```csharp
int? ChargeStart = null,   // the charge key's value now (V); null = no charge key
int? QuestDigit = null,    // the quest key's digit; null = no quest key
bool Percent = false, bool RemorphA = false, bool RemorphB = false,
bool LastIgnored = false   // `last` had charges but no Last value was given
```

- `Has(Key)` also answers `Percent`, `RemorphA`, `RemorphB`.
- `public bool HasOperandKeys => Ten || DigitKeys.Count > 0 || ChargeStart != null || QuestDigit != null;` replaces the `!buttons.Ten && buttons.DigitKeys.Count == 0` checks in `ArithmeticSearch.Find` and `DivisionShortcut.Search`.
- `Fits`, `Spare`, `Drained` and `Uses` group by `press.ChargeKey` instead of `press.ToString()`.
- `Buttons.All` gets `ChargeStart: 12, QuestDigit: 7, Percent/RemorphA/RemorphB: true` only in new tests. **Keep `Buttons.All` itself unchanged**, so the existing timing and tests stay comparable.

### `Parse(string text, long? last = null)`

New tokens, matched after lowercasing and before `LeftTransforms.Parse`:

| Token | Effect | Charge key |
|-------|--------|------------|
| `c` / `cV` (`^c(\d{1,9})?$`) | `ChargeStart = V` (or n when V is missing); error when V = 0 or n > V ("charge key c12 can't have more than 12 presses") | `c` |
| `qD` (`^q(\d)$`) | `QuestDigit = D` | `q` |
| `q` | error: `Quest key needs its digit (write qD:n)` | |
| `%` | `Percent = true` | `%` |
| `a->c`, `b->c` | `RemorphA` / `RemorphB` | `a->c`, `b->c` |
| `last` | adds `LeftTransforms.Last(last)` to the transforms when `last` is given; otherwise sets `LastIgnored = true` and adds no charge | `last` |
| `100-x`, `25\|69` | through `LeftTransforms.Parse` (§4) | `100-X`, `25\|69` |

`:0` takes them back out, as today. Watch for `x` → `×`: the switch only maps the whole token `x`, so `100-x` isn't touched. Also `"25|69"` has no `,` or `:`, so the splitting is safe.

`Token(text)` (used for Required) must return `c`, `q` and `last` for the bare words, even though `Parse("q:1")` is an error and `Parse("last:1")` has no value. Special-case those three before the parse round trip. `c12` → `c` and `q8` → `q` go through the normal path.

## 3. Simulator (`Run`)

New state: `int chargeUsed = 0`, `bool questUsed = false`.

- **Charge** (`op != null`, and when `buttons` is given: `ChargeStart` is set and `press.Digit == ChargeStart - chargeUsed` and that's ≥ 1): it types `press.Digit` and `chargeUsed++`. Without `buttons`, trust `press.Digit` (≥ 1). Pressing more often than the charges is `Fits`'s check, as for every button.
- **Quest** (`op != null`, `!questUsed`, and when `buttons` is given: `press.Digit == QuestDigit`): it types the digit and sets `questUsed = true`. A second press returns null.
- **Operand keys after remorph:** when `op` is `RemorphA` / `RemorphB` and `operand` is empty, the key's text k (`"10"`, a digit, the charge value or the quest digit, as a number) gives `display = (left % 10 + k * 10)` or `(left / 10 * 10 + k)`, and then `op = null`, and the new display is logged. Overflow is impossible at these sizes, but still use the 10^18 check. With `op` = remorph and `operand` non-empty, nothing can happen (the key already ended it), so a remorph never builds an operand.
- **`=` after remorph:** `op` is already null, so the existing "= with nothing pending" rule returns null. A non-key press while remorph is pending also returns null (the operator case requires `op == null`; add `op is not (RemorphA or RemorphB)` to the left-side cases, which already require `op == null`).
- **`%`:** joins the operator case (`Has(Key.Percent)`). At `=`: `result = left * d / 100` (check `left <= (Limit - 1) / d` first, as `×` does). A result above `1 << 24` returns null (requirements: the game's float isn't exact there).
- **`last`, `100-X`, `25|69`** are `Key.Transform` presses. Nothing new is needed.

## 4. Transforms (`LeftTransforms.cs`)

Add to `Create`:

```csharp
case "100-x":
    return Number("100-X", v => Math.Max(0, 100 - v),
                  v => v > 0 ? (v <= 100 ? [100 - v] : []) : HundredUp(), ascending: true);
case "25|69":
    return Number("25|69", v => Swap2569(v), v => [Swap2569(v)]);
```

- `HundredUp()`: 100, 101, …, lazily, stopping below `Limit`. Every v ≥ 100 maps to 0. `Ascending` lets the backward search stop.
- `Swap2569(v)`: map the digits of `v.ToString()` 2↔5, 6↔9, then `long.Parse`. It's its own inverse, so for a plain w the preimage is `Swap2569(w)`. Like the other number transforms, preimages with leading zeros aren't listed.
- `public static LeftTransform Last(long value) => new("last", _ => value.ToString(), _ => []);` isn't cached (the value changes from puzzle to puzzle). Its equality is by token, which is fine because a puzzle has one Last value. `Parse("last")` stays null (it needs a value), and `Buttons.Parse` calls `Last` directly.
- **Check `100-X` first (open question 2):** with UnityPy, read the `operator_100-x` button's onClick persistent call in `level1` (method `X_ops_Y`, int argument). If it isn't 100, rename the token to `<y>-X` and use y.

## 5. Operand typing (`DivisionShortcut.cs`)

### `TypeOperand(text, buttons)`

DP over `(i, c, q)`: i characters spelled, c charge presses used in this operand, q ∈ {0, 1} quest used. These lines have one operator and the keys are right-side only, so the charge value at the start of the operand is always `ChargeStart`. Moves:

- the digit key `text[i]`, and `10` (as today)
- the charge key, when `c < Charges["c"]` and `text` continues with `(ChargeStart - c).ToString()` → `(i + len, c + 1, q)`
- the quest key, when `q == 0` and `text[i] == QuestDigit` → `(i + 1, c, 1)`

The answer is the cheapest state with `i == text.Length`. On equal cost, keep today's preference (digit keys, then `10`), then quest, then charge, so the existing tests keep their lines. The table is at most `(len + 1) × (charges + 1) × 2` cells.

### `CheapestDivisor(low, high, buttons, maxCost, minimum = 2)`

- The DFS also carries `(c, q)` and steps with the charge value (`prefix × 10^len + value`, `used + len`) and the quest digit, under the same rules.
- `MinCost(remaining)` = `ceil(remaining / widest)`, where `widest` = the most digits one press can type: 2 with `10`, `len(ChargeStart)` with the charge key, otherwise 1.
- `minimum` (default 2, today's behaviour) lets the `%` search ask for D ≥ 1.

## 6. Arithmetic phase (`ArithmeticSearch.Find`)

The early return becomes `ops.Count == 0 && !remorph || !buttons.HasOperandKeys`, where `ops` now includes `Key.Percent` and `remorph` = `RemorphA || RemorphB`.

### `%` lines

In the (x, y) loop, for `op == Key.Percent`: skip x = 0 (the result is 0, and `× 0` or the edits already cover it) and y > 2^24. `low = ceil(100y / x)`, `high = floor((100y + 99) / x)`. Skip when `low > high`. Then `DivisionShortcut.CheapestDivisor(low, high, buttons, budget, minimum: 1)`, cached by `(low, high)`. The candidate's operand is the D it found, and its presses go in a separate cache keyed by D (`operandCache` keeps working for the other ops).

### Remorph lines

They aren't in the (x, y) loop, because Y is fixed by X and the key. After the candidates of the (x, y) loop:

1. `keys` = every one-press operand key: each digit key d (k = d), `10` (k = 10), the charge key (k = `ChargeStart`) and the quest key (k = `QuestDigit`), each with its `Press`.
2. `yByValue` = a dictionary from the `ys` list (value → index).
3. For each x in `xs`, each remorph op, and each key: y = the formula. If `y` is in `yByValue`, add a candidate with cost `pre + 2 + post`. Otherwise, without transforms, try `Plan(y.ToString(), g)` once per y (cached), add it to `ys`, and use it.
4. Build them the same way as the other candidates, as `[.. pre, new Press(op), keyPress, .. post]` (no `=`). `Line.Op` = the remorph key, `Line.Operand` = k.

The cost-level loop and `Keep` don't change. `Keep` simulates, so a wrong formula shows up as a test failure, not as a bad line.

### Timing

Budget: 41 → 483, `Buttons.All` plus every transform plus `%`, `a->c`, `b->c`, `c:12`, `q7:1`, Release. That's about 0.9 s today, and it should stay under about 1.5 s. If `%` makes it slower, first cap the `%` pairs to `x ≥ 1 && y ≤ x × (maximum operand) / 100` (D can't be larger than what the budget can type). Note the result in "As built".

## 7. Division phase

- `DivisionShortcut.Search` returns early on `!buttons.Divide || !buttons.HasOperandKeys`.
- Path B (digit keys) goes through `CheapestDivisor` and `TypeOperand`, so it gets the charge and quest keys from §5. Gate path B on `HasOperandKeys` instead of `DigitKeys.Count > 0`.
- Path A (only `10`) doesn't change.
- The Modulo phase types its modulus with `TypeOperand`, so it gets the new keys too. Nothing else changes.

## 8. Game feed (`GameLink.cs`)

- `MapButton`: `number_charge` → `c`, `number_quest` → `q`, `operator%` → `%`, `operator_last` → `last`, `operator_remorph_a` → `a->c`, `operator_remorph_b` → `b->c`, `operator_100-x` → `100-X`, `operator_flip` → `25|69`.
- `Parse`, inside the button loop, after `charge = glass ? min(uses, 1) : uses`:
  - token `c` → `c{uses}:{charge}`, or `c:0` when `uses` is 0
  - token `q` → `q{digit}:{charge}` when the button has a `digit` number. Otherwise add `quest` to `ignored`.
  - other tokens as today
- `GamePuzzle` gets `long? Last = null` from the root `last` (a number, otherwise null).
- Required: `MapButton` already gives `c` / `q` / `last`, and `Buttons.Token` accepts them (§2).

## 9. UI (`MainForm.cs`)

- A `lastBox` row "Last" right below Required. Enter in it starts the search, like the other boxes. Empty = no value. Text that isn't a whole number ≥ 0 gives a status error, like S and G.
- `StartDivisionSearch` calls `Buttons.Parse(buttonsBox.Text, last)`. When `buttons.LastIgnored`, add `no Last value, last ignored` to the status (next to the Required note, in the same style).
- `ApplyGamePuzzle` sets `lastBox.Text = puzzle.Last?.ToString() ?? ""` before it starts the search.
- `Lock` applies as today: while locked the file is ignored, so the Last box keeps what you typed.

## 10. Mod (`GameHook.MelonLoader`)

### `CalcHookCore.cs`

- `ButtonInfo.Digit` (int, −1 = none): for an object named `number_quest`, `cu.GetComponent<QuestionNumber>()?.digit`. Read it with the same null-safe pattern as the other components.
- `puzzle.json` and `state.json`: a top-level `"last": _level.last` (it's a public `int`), and `"digit": D` on a button only when `Digit ≥ 0`.
- The change check that writes `state.json` and the log line also compares `Digit` and `last`. A quest press then rewrites both with the new digit on that state change (the press changes `uses` anyway, but include the digit so a glass quest, whose `uses` doesn't move, also updates).
- The settle check for `puzzle.json` doesn't change. One puzzle per (round, goal), and the digit before the first press is what the solver needs.

### Overlay symbols (`ButtonSymbols.cs`)

- `Token` mirrors the new `MapButton` cases (`c`, `q`, `%`, `last`, `a->c`, `b->c`, `100-X`, `25|69`). `ButtonSymbolsTests` adds the new ids to its id list, so the mirror stays checked.
- New `public static string Of(string id, int uses, int digit)`: `number_charge` with uses > 0 → `"c" + uses`, and `number_quest` with digit ≥ 0 → `"q" + digit`. Anything else → `Of(id)`.
- The calculator overlay calls the new overload with the live `Uses` / `Digit`. The per-id `_symbols` cache must skip `number_charge` and `number_quest` (their text changes). Check how often `_overlayButtons` is re-read (`CalcHookCore.cs` ~line 457). If it isn't every frame, re-read the two live values each frame or on every state change, so a new quest digit shows by the next state change at the latest (requirements R7.2).
- The shop panel keeps `Of(GetAlchemyButtonKey(offer))`, which shows `c` / `q` for those offers.

## 11. Docs

At the end: `docs/button-id.md` (descriptions for the eight ids, with the formulas above and the `remorph_a` answer), `CLAUDE.md` (button table rows for the new buttons; the Status list entries; the `ButtonSymbols` / Last box notes), and `user-verification/` (Test H below).

## Test plan

### Unit tests (xUnit, existing files)

`EngineeringCalculatorTests.cs`:

- Parse: `c:12` → `ChargeStart` 12, `Charges["c"]` 12. `c12:1` → 12 / 1. `c3:5` → error. `c0:1` → error. `q8:4` → `QuestDigit` 8. `q:4` → the quest error. `%:1`, `a->c:1`, `b->c:1`, `100-x:1` (→ token `100-X`), `25|69:1`. `last:1` with `last: 37` → a `last` transform. Without it → `LastIgnored`, no `last` charge. `:0` removes each one.
- `Token`: `c`, `c12` → `c`. `q`, `q8` → `q`. `last` → `last`. `%` → `%`.
- Run: the simulator-only checks in the requirements (charge sequence `1211`; a wrong charge value is invalid; a second quest press is invalid; `a->c 10` on 483 → 103; `a->c =` invalid; `a->c 7 =` invalid; `b->c 7` → 487; `% 2 5 =` on 95 → 23; a `%` result above 2^24 invalid; charge and quest invalid on the left side; `a->c` invalid on the right side).
- `Fits`: `+ c12 c11 c10 =` with `c:2` → false. With `c:3` → true. `Drained` counts `c` once.

`LeftTransformTests.cs`: `100-X` (41 → 59, 100 → 0, 150 → 0, `05` → 95), `25|69` (2569 → 5296, 120 → 150, 41 → 41), `Last(37)` on anything → 37. Add `100-X` and `25|69` to the preimage property test (for 0 under `100-X`, take the first few preimages).

`DivisionShortcutTests.cs`: `TypeOperand("1211")` with `c:12` → `c12 c11`. `TypeOperand("1211")` with `c:1` → null when there are no other keys. `TypeOperand("8")` with `q8:1` → `q8`. `"88"` with only `q8` → null. `CheapestDivisor(5, 5, c:5 only, …)` → `c5`. `minimum: 1` allows D = 1.

`ArithmeticTests.cs`: the examples table in the requirements (95 → 23 `%`; 483 → 73 `a->c 7`; 483 → 487 `b->c 7`; 10 → 1221 `+ c12 c11 =`; 41 → 59 `100-X`; 41 → 37 `last` with Last 37; 2569 → 5296 `25|69`). All existing tests stay unchanged.

`DivisionShortcutTests.cs` / `ModuloSearchTests.cs`: 50 → 10 with `÷:1, c:5` gives `÷ c5 =`. 41 → 8 with `÷:1, q5:3` gives `÷ q5 =`.

`GameLinkTests.cs`: `number_charge` uses 12 → `c12:12`. Glass → `c12:1`. Uses 0 → `c:0`. `number_quest` with `digit` 8, uses 4 → `q8:4`. Without `digit` → ignored as `quest`. `last` → `GamePuzzle.Last`. A missing `last` → null. The other new ids map. Required `number_charge` → `c`.

`RequiredButtonTests.cs`: a required `c` matches a line with `c12`. A required `last` works.

`ButtonSymbolsTests.cs`: the mirror test includes the new ids. `Of("number_charge", 12, -1)` → `c12`. `Of("number_quest", 4, 8)` → `q8`. `Of("number_quest", 4, -1)` → `q`.

### Manual (app)

- Buttons `%:1, 2:1, 5:1`, 95 → 23: the first line is `% 2 5 =`.
- Buttons `last:1`, Last empty: the status says `no Last value, last ignored`. Type 37 in Last and press Enter: 41 → 37 gives `last`.
- With Follow game on, a puzzle with `last` fills the Last box.

### In-game (user-verification/test-h.md)

1. A run with `number_charge`: two presses at 12 give `1211` (open question 4). Overlay shows `c12`, then `c11`, `c10`.
2. `number_quest`: the overlay shows `q<digit>`, and after a press the new digit shows. `state.json` has the new `digit`.
3. `operator_last`: `puzzle.json` `last` equals the previous round's goal. In round 1 it's 0, or check what a loaded save gives (open question 1).
4. `remorph_a` with `10` (open question 3), `remorph_b`, `%`, `100-x`, `operator_flip` each give the solver's value.
5. Alchemy (challenge 9): every new id shows its token in the overlay and the shop panel.

## As built

Implemented 2026-10-24 as designed; 453 tests pass (352 before, 101 new in `MoreButtonsTests.cs`, `ButtonSymbolsTests.cs`, `GameLinkTests.cs`). Differences and notes:

- **Open question 2:** UnityPy shows `operator_100-x` calls `MainCalc.X_ops_Y(100)`, so the token stays `100-X`.
- **Simulator:** the right-side keys share one local `TypedText` helper in `Run`. Charge and quest presses are counted when typed. The one key after `a->c` / `b->c` ends the operation inside the same branch (the log gets the new left side). A `%` result above 2^24 returns null (invalid), not "max digits".
- **`Buttons`:** `HasOperandKeys` and a new `OperandWidth` (the most digits one press types: 2 with `10`, the charge start's length with `c`). `OperandWidth` replaces the `Ten ? 2 : 1` arithmetic in `CheapestDivisor`, `ArithmeticSearch` and `ModuloSearch.OperandBound`, so a charge key that types 2+ digits doesn't make those bounds skip lines.
- **`Buttons.Token`:** the bare words `c`, `q`, `last` are special-cased before the parse round trip, as designed.
- **`TypeOperand`:** a DP over (position, charge presses, quest used). `CheapestDivisor` carries the same two counters and takes `minimum` (default 2).
- **Division path B** runs when there are digit keys, a charge key or a quest key (not for `10` alone, which Path A covers). The Search gate is `HasOperandKeys`; the error text is unchanged.
- **`%` lines:** `percentRanges` is cached by `(low, high, budget)`; the chosen D's presses go in `percentOperands` (kept apart from `operandCache`). x = 0 and y > 2^24 are skipped.
- **Remorph lines:** the candidate's `Operand` is an index into `RemorphKeys(buttons)` (a charge or quest key can share a number with a digit key); `Line.Operand` reports the key's number k. Y values missing from `ys` get a `LeftEdits.Plan` once each (also with transforms).
- **Timing** (41 → 483, Release, every button and transform plus `%`, `a->c`, `b->c`, `c:12`, `q7:1`, `100-x`, `25|69`): about 0.9 s warm, 1.1 s cold; without the new buttons 0.8–0.9 s. No cap on the `%` pairs was needed.
- **Mod:** `ButtonInfo.Digit` is a plain field set in `ReadButtons` (a struct with an initializer doesn't compile; `-1` = none). `last` is written to both JSON files, `digit` only on the quest button. The log line gets ` last=N` and `/d<digit>`. The overlay re-reads the buttons every 0.1 s, so `SymbolOf(ButtonInfo)` skips the per-id cache for the two live keys. The state check for `puzzle.json` is unchanged.
- **Not run here:** the WinForms app wasn't launched (a copy was already running and locks the Release build); the manual checks were done with the same search code in a console harness (95 → 23 `% 2 5 =`, 483 → 73 `a->c 7`, 10 → 1221 `+ c12 c11 =`), and the Last-box note is in code only. Esc/restart code wasn't touched. Test H in `user-verification/` is for the user.
