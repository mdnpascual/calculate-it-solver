# Design: near, median, sup, money, equal, mw and complement buttons

**Status:** Complete (2026-10-25, 740 tests). Test V done: coins, sup, money, median, near, mw, AX and `==` confirmed in game. Optional V7 (boss rule 14 + near/median) and V8 (boss rule 10 + money) were not met; the simulator follows the decompiled `SubUse`-first order.

Read `requirements.md` first. This follows the patterns of `left-transforms/` (transform catalogue), `more-buttons/` (remorph, quest key, `last`) and `prime-button/` (preimages, overlay).

## 1. Transforms (`CalculatorPuzzle/LeftTransforms.cs`)

### Bound values

`near` and `median` need the goal, `money` needs the money amount. Like `LeftTransforms.Last(value)`, they are built from a value and are not cached by token:

```csharp
public static LeftTransform Near(long goal);
public static LeftTransform Median(long goal);
public static LeftTransform Money(long money);
```

`LeftTransform` gets two members so the simulator can rebind them to the value at the press (rules 9 and 10) without switching on the token:

```csharp
/// <summary>What the transform reads besides the display: None, Goal (near, median) or Money (money).</summary>
public Reads Reads { get; }
/// <summary>The same transform for another goal or money amount; null when Reads is None.</summary>
public Func<long, LeftTransform>? Bind { get; }

public enum Reads { None, Goal, Money }
```

Equality stays by token, so `Buttons.Transforms.Contains(Near(49))` is true when the puzzle has `near` bound to 48.

`sup` doesn't read anything else, so it goes into `Create` and the cache like `p`.

### Apply and Preimages

| Token | Apply(display) | Preimages(v), ascending | Notes |
|-------|----------------|-------------------------|-------|
| `near` | `\|G − n\| ≤ 10` → G, else n + 10 | v = G: max(0, G − 10) … G + 10. Else v − 10 when v − 10 ≥ 0 and \|v − 10 − G\| > 10 | via `Number` |
| `median` | ceil((n + G) / 2) = (n + G + 1) / 2 | 2v − G − 1 and 2v − G, those ≥ 0 | via `Number` |
| `sup` | null when `display.Length ≥ 6`; else digit sum of n | v ≤ 45: every u ≤ 99 999 with digit sum v, upward (lazy); none above 45 | raw display check first |
| `money` | null when `display.Length ≥ 5`; else n + M | v − M when 0 ≤ v − M ≤ 9 999 (M = 0: just v) | raw display check first |

n is the display as a number (leading zeros drop), as in `Number`. `sup` and `money` look at the raw text length before they parse it, so they wrap `Number` (or build the `LeftTransform` directly) instead of using it unchanged. A display with leading zeros has preimages only as plain numbers, like the other number transforms (`LeftSearch.Join` matches by value).

`median` preimage check: ceil((u + G) / 2) = v ⇔ u + G ∈ {2v − 1, 2v}. So `median` reaches G only from G − 1 or G, which matches the game.

`sup` preimages can be many (about 4 900 five-digit numbers for v = 22). `LeftSearch`'s state limit (100 000) and the lazy upward order keep this bounded. If the arithmetic phase gets slow with `sup`, cap its preimages at 4 digits (u ≤ 9 999) and note it in "As built".

## 2. Buttons and parsing (`CalculatorPuzzle/EngineeringCalculator.cs`)

### `Buttons.Parse(text, last, goal, money)`

New optional parameters `long? goal = null, long? money = null`.

- `near`, `median`: with `goal` → `LeftTransforms.Near(goal)` / `Median(goal)`; without it → left out, `GoalIgnored = true` (status: `near needs G`). In practice the UI always has G.
- `money`: with `money` → `LeftTransforms.Money(money)`; without → left out, `MoneyIgnored = true` (like `LastIgnored`).
- `sup`: through `LeftTransforms.Parse` like the other transforms.
- `==`: new `bool AllDigits` (like `RemorphA`), `Key.AllDigits`, token `==`, `Has(Key.AllDigits)`. The token parser must match `==` as a whole token before it treats `=` as the free equals key (`==:1` is the button, `=` is never in the Buttons box). `Press.ToString` gives `==`, so lines read `== 7 X1`.
- `mD` (`^m(\d+)$`, D from 0 to 999 999, no leading zero unless D is 0): new `int? MemoryValue` (like `QuestDigit`), `Key.Memory`, charge key `m`. One mw button at most (the game has one); a second `mD` is an error.
- `AX`: new `bool Complement` (like `RemorphA`), `Key.Complement`, token `AX` (any case). It must not reach the `dX` pattern (`A` is not a digit, so a `^\dX$` test already skips it; check the parser's order). `Press.ToString` gives `AX`, so lines read `AX 7`.

`Buttons.WithGoal(long goal)` returns a copy with `near` / `median` rebound (`t.Bind(goal)` for `Reads.Goal`). `GoalStepSearch` uses it for each k (goal G + k); `MainForm` builds the first one with G.

### Keys and presses

- `Key` gets `AllDigits`, `Memory` and `Complement`. `Press.ToString`: `==`, `m{Digit}` (Digit holds D), `AX`. `ChargeKey`: `m` for `Key.Memory`.
- `Press.IsOperandKey` adds `Key.Memory`. `HasOperandKeys` adds `MemoryValue != null`. `OperandWidth` takes the max with `MemoryValue.ToString().Length`.
- `Charges` key for mw: `m` (one charge pool, like `c` and `q`).

### Simulator (`Run`)

New optional parameter `LineContext? context = null`:

```csharp
/// <summary>The game state a line runs in, for buttons that read it.</summary>
public sealed record LineContext(long Goal, bool GoalStep = false, long Money = 0, MoneyRules? MoneyRules = null);

/// <summary>How money changes during a line. Drain2: diff_type >= 7. Rule10: boss rule 10. Passives: the coin passives present.</summary>
public sealed record MoneyRules(bool DrainFromThird, bool Rule10, IReadOnlySet<string> Passives);
```

`Run` keeps `charged` (charged presses so far: every press except `=`; glass counts) and `money`:

1. Before each charged press's effect: `charged++`; if `DrainFromThird && charged >= 3 && money > 0` → `money--`; if `Rule10 && money > 0` → `money--`.
2. A `Key.Transform` press with `t.Reads == Goal` applies `t.Bind(goalNow)`, where `goalNow = Goal + (GoalStep ? charged : 0)`; with `Reads.Money` it applies `t.Bind(money)`. Without a context it applies `t` as bound (today's behaviour).
3. After each `=`, on the result **before** the rule-12 + 1: + 10 for `passive777` at 777; + 1 each for `passive_lucky` (% 7 == 0), `passive_perfect` (% 10 == 0), `passive_odd` (odd).
4. `Key.AllDigits` starts like `RemorphA` (needs `op == null`, `Has`), sets `op`, `left`. The typed key then ends it like remorph: `display = AllDigits(left, typed)` where `AllDigits(l, t) = long.Parse(new string(t[0], l.ToString().Length)).ToString()`.
4a. `Key.Complement` starts like `RemorphA` (needs `op == null`, `Has`) plus `display.Length < MaxDisplay`; sets `op`, `left`. The typed key ends it like remorph: `display = Complement(left, typed)` where `Complement(l, t) = long.Parse(t + l.ToString()).ToString()` (`t` is the key's whole typed text). The line is invalid when the result is longer than `MaxDisplay` (requirements rule 8a). Put `Complement` next to `AllDigits` so the operator + one key handling is shared (the `op is Key.RemorphA or Key.RemorphB` test at the remorph block grows to all four).
5. `Key.Memory` in `TypedText`: allowed when `buttons == null || buttons.MemoryValue == press.Digit`; returns `press.Digit.ToString()`. No once-per-line rule (no re-roll).

`Cost`/`Sent` and every caller that passes `plusOne` also pass the context through. Phases that call `Run` to check lines get the context from their caller; when none is given, behaviour is today's (searches assume the money they were built with).

## 3. Search phases

- **Arithmetic (`ArithmeticSearch`)**: `near`, `median`, `sup`, `money` come in through `buttons.Transforms`, so `LeftSearch` uses them with no change. `RemorphKeys` already lists one press per operand key (`10`, digits, `c`, `q`); add the mw key. The remorph block (`remorphs`, the `y` formula) gets `Key.AllDigits` with `y = AllDigits(x, key's first digit)` and `Key.Complement` with `y = Complement(x, key's text)` (only when x's display has fewer than 6 characters and y at most 6); the line is `[.. pre, ==, key, .. post]` / `[.. pre, AX, key, .. post]`, built like remorph.
- **Two-operator (`TwoOperatorSearch`)**: `ops` adds `Key.AllDigits` and `Key.Complement`; `IsRemorph` becomes "operator + one key" (`RemorphA`, `RemorphB`, `AllDigits`, `Complement`), and the middle value for `AllDigits` is `AllDigits(x, d's first digit)`, for `Complement` it is `Complement(x, d's text)` (same length checks). The operand list adds the mw key the way it adds the quest key, without the once-only flag.
- **Division (`DivisionShortcut`)**: path B runs when there is any single operand key besides `10`; add `MemoryValue != null` to that test (line 136). `TypeOperand` / `CheapestDivisor` get a `Step` for the mw key: appends `MemoryValue`'s digits, uses one `m` charge, any number of times.
- **Modulo (`ModuloSearch`)**: it types operands with `TypeOperand`, so it gets mw for free.
- **`GoalStepSearch`**: per k, `buttons.WithGoal(goal + k)` and a context with `Goal = goal`, `GoalStep = true` (the game's goal at a press is G + charged presses so far).
- **`RequiredPlan`**: `==` and `AX` are operator phases like remorph (the arithmetic and two-operator phases); `near`, `median`, `sup`, `money`, `mD` are filter-only, like other left-side buttons and operand keys.

## 4. Game link (`CalculatorPuzzle/GameLink.cs`)

- `MapButton`: `operator_near` → `near`, `operator_mediana` → `median`, `operator_sup` → `sup`, `operator_money` → `money`, `operator_equal` → `==`, `operator_complement` → `AX`, `number_mw` → `m`.
- `Parse`: for token `m`, read `stored`: present → `m{stored}:{count}`; absent → `ignored.Add("mw")` and skip (like the quest key without `digit`).
- `GamePuzzle` gets `long? Coins = null, int DiffType = 0, IReadOnlyList<string>? CoinPassives = null` (last parameters, so existing calls still compile).
- `GamePuzzle.MoneyRules(int bossRule)`: `new MoneyRules(DiffType >= 7, bossRule == 10, CoinPassives)`.

## 5. App (`CalculatorPuzzle/MainForm.cs`)

- A **Money** textbox next to Last (`AddInlineRow`). `ApplyGamePuzzle` fills it from `puzzle.Coins` (empty when null). Enter in it restarts the search, like the Last box.
- `Buttons.Parse(text, last, goal: G, money: Money box)`.
- The search tasks build one `LineContext(G, GoalStep: Boss == 14, Money, rules)` (`rules` from the last applied `GamePuzzle`, or `MoneyRules(false, Boss == 10, [])` when there is no game feed) and pass it to the phases.
- Status notes: `money $M` when the puzzle has `money`; `money needs a Money value` when `MoneyIgnored`; `mw empty` when `mw` is in `Ignored`.

## 6. Mod (`GameHook.MelonLoader/`)

Read-only: field reads and `GameObject.Find` only.

- `CalcHookCore.cs`:
  - Button snapshot: `int Stored` = `NumberReader.number` when the button has a `NumberReader` with `readed` true, else −1 (public fields, like `QuestionNumber.digit`). Written as `, "stored": N` when ≥ 0, in `puzzle.json` and `state.json`.
  - Top level: `"coins": _level.coins`, `"diffType": GamePrefs.GetInt("diff_type")`, `"coinPassives": [...]` (the four names, `GameObject.Find(name) != null`). Also in the log line (`coins=12`).
  - Fingerprint: add `|mw:` + `Stored` (so an absorb republishes). Coins are **not** in the fingerprint.
  - `SymbolOf` / overlay: `number_mw` skips the per-id cache (like the charge and quest keys) and shows `ButtonSymbols.Of(id, uses, digit, stored)`.
- `ButtonSymbols.cs`: `Token` gets the six fixed ids (`operator_complement` → `AX`); `number_mw` → `mw`; an overload or extra parameter shows `m<stored>` when stored ≥ 0.
- `README.md`: document `coins`, `diffType`, `coinPassives`, `stored`.

## 7. Docs

- `docs/button-id.md`: fill in the seven rows (for `operator_complement`: the key can be any number key, not only a single digit; ask the user before changing their wording) (token, side, effect, the length rules, `SubUse` first). Keep the user's wording where it is right; fix "adds all digit" style typos only if the user agrees (memory: offer, don't rewrite silently).
- `CLAUDE.md`: add the transforms to the transform row, `==` and `AX` next to `a->c` / `b->c`, `mD` next to `c` and `q`.
- `CalculatorPuzzle/README.md`: update the notes for `LeftTransforms.cs`, `EngineeringCalculator.cs`, `ArithmeticSearch.cs`, `TwoOperatorSearch.cs`, `DivisionShortcut.cs`, `GameLink.cs`, `MainForm.cs`, `GoalStepSearch.cs`.
- `spec/README.md`: the line for this spec.

## 8. Test plan

New file `CalculatorPuzzle.Tests/NearMedianSupMoneyTests.cs` (and `ButtonSymbolsTests`, `GameLinkTests` updates).

Transforms:
- `near` (G = 48): 41 → 48, 38 → 48, 58 → 48, 59 → 69, 0 → 10, `05` → 15. Preimages(48) = 38 … 58; Preimages(30) = [20]; Preimages(60) = [] (50 is within 10).
- `median` (G = 48): 47 → 48, 46 → 47, 49 → 49, 50 → 49, 0 → 24. Preimages(48) = [47, 48]; Preimages(49) = [49, 50].
- `sup`: 941 → 14, 99999 → 45, `0099` → 18, 123456 → null, `012345` → null. Preimages(1) = [1, 10, 100, 1000, 10000]; Preimages(46) = [].
- `money` (M = 12): 36 → 48, `0036` → 48, 12345 → null, `01234` → null. Preimages(48) = [36]; Preimages(5) = []; Preimages(10011) = [9999]; Preimages(10012) = [] (10 000 has 5 characters).
- `Bind` keeps the token and changes the value; `Equals` is by token.

Simulator:
- `==`: 483 `== 7` → 777; 483 `== 10` → 111; `05` `== 8` → 8; 52 `== 0` → 0; `==` followed by `=` or a second key is invalid; `==` without the button is invalid.
- `AX`: 41 `AX 7` → 741; 41 `AX 10` → 1041; 41 `AX c12` → 1241; `04` `AX 7` → 74; 0 `AX 7` → 70; 41 `AX 0` → 41; 123456 `AX` is invalid (6 characters); 12345 `AX 10` is invalid (7-character result); `AX` followed by `=` or a second key is invalid; `AX` on the right side or without the button is invalid.
- mw: `- m5 m5 =` from 103 → 48; `m5` on the left side is invalid; `m5` with Buttons `m4:2` is invalid; charges: `m5:1` rejects two presses.
- Money: the three requirements examples (lucky passive, rule 10, and a difficulty drain: `diff_type` 7, money 5, presses `X1 X2 money` → the third press pays, money 4).
- Rule 14 goal: S = 39, G = 48, `near` with `GoalStep` → 49; S = 30 → 40.

Searches:
- `LeftSearch` / arithmetic: every row of the requirements Examples table (cost and line).
- Division with only `m` as a typing key besides `10`: a path-B line that types `m3`.
- `GoalStepSearch`: S = 39, G = 48, `near:1` gives `near` (goal 49, k = 1).

Game link and mod:
- `MapButton` for the seven ids; `number_mw` with `stored` → `m5:2`, without → ignored `mw`; `coins`, `diffType`, `coinPassives` parsed, missing → defaults.
- `GameLinkTests` and `BlockedButtonTests` use `operator_near` as the "unmapped" sample: move them to another unmapped id (`operator_random`).
- `ButtonSymbolsTests`: remove the six ids from the unmapped list; `Of("number_mw", …, stored: 5)` → `m5`, stored −1 → `mw`.

### Test V (in-game, `user-verification/test-v.md`)

Tests T (achievement modes) and U (near-one passive) are planned by other specs; use the next free letter when this one is written.

- **V1 near:** a puzzle with `near`. Left side within 10 of the goal → the goal; farther → + 10. Expected: matches; the overlay shows `near`.
- **V2 median:** left side below the goal (rounds up) and above it (goes down, stops at G + 1). Expected: matches.
- **V3 sup:** on a 5-character display → digit sum; on a 6-character display → nothing, and the charge count drops by 1.
- **V4 money:** on a display of 4 characters → + money; on 5 characters → nothing, charge used. `puzzle.json` `coins` equals the shown money.
- **V5 ==:** `== 7` on 483 → 777; `== 10` → digits become 1; `=` is blocked between the two presses.
- **V6 mw:** store a digit (absorb), check `puzzle.json` republishes with `stored` and the overlay shows `m<digit>`; then on the right side it types the digit; it can't be pressed on the left side.
- **V6a AX:** `AX 7` on 41 → 741; `AX 10` → 1041; `AX` on `04` → 74; `=` and the other operators are blocked between the two presses; on a 6-character display `AX` does nothing and the charge count drops by 1. Expected: matches; the overlay shows `AX`.
- **V7 (optional) boss rule 14 + near/median:** the value uses the raised goal (G + presses including this one).
- **V8 (optional) boss rule 10 + money:** money adds the amount after its own press paid.

## As built (2026-10-25)

- The `LineContext` is carried by `Buttons` (`Buttons.Context`, `WithContext`) instead of a new parameter on `Run` / `Sent` and every phase, so the phases, `LeftSearch.Join` and `GoalStepSearch` check lines with the real goal and money without signature changes. `Buttons.WithGoal` and `TakeTwo` copy through one private `Copy`.
- The goal in rule 14 counts charged presses without glass (glass doesn't raise the goal); the money drains count every charged press, glass too.
- `MoneyRules.Passives` is an `IReadOnlyCollection<string>`.
- `ButtonSymbols.Of(id, uses, digit, left, stored)` is the new overload; `Of("number_mw")` alone gives `m` (the `MapButton` mirror) and the shop panel shows `mw`.
- 95 new tests are in `NearMedianSupMoneyTests.cs`; the `Operands` test from the plan became a two-operator `+ m3 = + m3 m3 =` test (`Operands` is `internal`).
- Test V result: `coins` matches the game; winning lines with `sup`, `money`, `median` and `near` worked; mw (`stored`, overlay, right-side typing), `AX` and a winning `==` line confirmed. Not met: boss rule 14 with `near`/`median` (V7), boss rule 10 with `money` (V8), so the press order (`SubUse` first) for those rules rests on the decompiled code only.
