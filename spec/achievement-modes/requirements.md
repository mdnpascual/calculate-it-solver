# Requirements: Achievement modes (School mathematics, Just a one)

**Status:** Done (2026-10-25). Test T: T1, T2, T4, T5 confirmed in game; T3 (`0` / `10` keep the operator's credit) untested because the achievement was already unlocked.

## Why

Two Steam achievements need lines the normal solver never looks for:

- **School mathematics** (Steam id `SCHOOL`): win a round with `+`, `-`, `×` and `÷` each used at least once.
- **Just a one** (Steam id `UNIT`): make an `=` raise the number by exactly 1, with 4 different buttons over a run.

The app gets an **Achievements** section with one radio button per achievement. With one of them on, the search looks for that achievement's lines instead of the normal ones.

## Game facts (static analysis, `Assembly-CSharp.dll` and the scene files, 2026-10-25)

### School mathematics (`SCHOOL`)

| What | Where | Detail |
|------|-------|--------|
| Flags | `OperationsCounter.operations[0..3]` on the calculator (`sum`, `mul`, `sub`, `div` in the scene) | `MainCalc.Send` (the `=` handler) sets `operations[0]` after a `+`, `[1]` after `×`, `[2]` after `-`, `[3]` after `÷`. `mod`, `%`, `a->c`, `b->c` set nothing. |
| When it counts | `MainCalc.Send` | The flag is set by the `=` that applies the operator, whatever the result. `× 1 =`, `÷ 1 =`, `+ 0 =` and `- 0 =` count, even though the number doesn't change. |
| Reset | `StartLvl.NextLvl` | `OperationsCounter.Reset()` at every round start. All four must happen in the same round. A rule-13 re-roll doesn't reset them. |
| Unlock | `StartLvl.Update` | When the round is won (the normal win check), all four flags set → `Achieve("SCHOOL")`. |

So the line must reach G (a normal win) and must press `+`, `-`, `×`, `÷`, each followed by an operand and `=`. That needs at least 4 operator segments.

### Just a one (`UNIT`)

| What | Where | Detail |
|------|-------|--------|
| Check | `MainCalc.Count` (the score animation started by `=`) | `if (score_need - score_is == 1.0) operators_used[LastButId] = true;` |
| `score_need` | `MainCalc.Send` | The result of this `=`. |
| `score_is` | `MainCalc.Count`, round start | The result of the **previous** `=` (the score counter). Left-side presses don't change it. At a round start it is S (the previous round was won by an `=` that gave S). |
| `LastButId` | `CountUses.SubUse` | The `Infos.id` of the **last charged button pressed** (written in a `try`; a button with no `Infos` leaves it unchanged). `=` has no charges, so it doesn't change it. |
| Unlock | `DifferentOperatorsCount.Update` | 4 or more different `operators_used` entries set → `Achieve("UNIT")`. |
| Reset | none | `DifferentOperatorsCount.Reset()` is never called. The array is saved with the run (`RunSave`), so it fills up over the whole run, not one round. |

Ids from the scene files (`Infos.id`, read with UnityPy):

| Button | Id | Notes |
|--------|----|-------|
| digit keys `1`–`9` | 0 | all nine share id 0 |
| `0`, `10` | none | no `Infos`: pressing them doesn't change `LastButId` |
| `+` | 1 | |
| `-` | 2 | |
| `×` | 3 | |
| `÷` | 4 | |
| `mod` | 13 | |
| `q` (`number_quest`) | 21 | |
| `c` (`number_charge`) | 32 | |
| `%` | 34 | |

So the "operator" that gets the credit is the **last operand key with an id** before the `=`, or the operator itself when every operand key is `0` / `10`:

Examples on S = 4 (the `=` must give 5):

| Line | Result | Credited | Why |
|------|--------|----------|-----|
| `+ 1 =` | 4 + 1 = 5 | digit (id 0) | `1` was the last charged press |
| `5X ÷ 10 =` | 54 ÷ 10 = 5 | `÷` (id 4) | `10` has no id, so `÷` stays |
| `X5 mod 10 =` | 45 mod 10 = 5 | `mod` (id 13) | same |
| `X5 - 4 0 =` | 45 − 40 = 5 | digit (id 0) | `0` has no id, so `4` stays |
| `X1 ÷ 10 =` | 41 ÷ 10 = 4 | none | +0, not +1 |

Two consequences:

1. The 4 digit keys `1`–`9` are **one** entry, not nine. To get 4 entries the player mostly needs lines whose operand is typed with `10` (or `0`) only, so the operator gets the credit, plus at most one digit-key line.
2. Only the `=` that raises the number counts, and only against the previous `=` result. A line with one `=` at the very end, starting from the round's S, must make that `=` give **S + 1**. Left edits after that `=` don't matter for the credit.

Other details:

- The credit happens whether or not the round is won. The player presses the line, gets the credit, and then solves the round from S + 1 (the display now shows S + 1).
- Boss rule 12 (every `=` adds 1): `Send` sets `score_need` before the +1 and only updates it when the result differs from `score_is`. Rule 12 is out of scope here.
- The score animation also runs the check on each of its ~100 steps, so a jump of exactly 100, 50 or 25 might also credit (floating-point steps of 1.0, 0.5, 0.25 land on `need − is == 1`). Not used by the solver; not tested.

## The UI

1. A new collapsible section under the Buttons row (above the status line), titled **Achievements**. WinForms has no expander control: a flat header button `▸ Achievements` that shows or hides a panel (`▾` when open). Collapsed by default. The form already auto-sizes, so it grows and shrinks.
2. Inside the panel, two radio buttons:
   - `(+, -, /, *) solve` with the achievement name after it: `(+, -, /, *) solve (School mathematics)`.
   - `+1 with 4 ops (Just a one)`.
3. At most one is on. Both can be off (the normal solver). Clicking the one that is on turns it off.
4. While a mode is on and the section is collapsed, the header says which: `▸ Achievements: School mathematics`. A mode is never hidden.
5. Changing the selection restarts the search right away (no Enter needed; it is a click, not typing). Follow game keeps the selection and solves each new puzzle in that mode.

## What the solver must do

### Mode 1: School mathematics

1. Find lines from S to G that press `+`, `-`, `×` and `÷`, each one as `op operand =`, at least once. Other operators (`mod`, `%`, remorph) may appear too. Left edits may appear before, between and after the segments.
2. **n-operator lines**: up to 12 operator segments per line (the cap, so the search can't run for minutes). The shortest line is at least 4 segments.
3. Lines are checked in the simulator (`EngineeringCalculator.Run`) and against the charges, like every other line.
4. The Lines box shows the 10 cheapest, ranked like today (cost, drained, spare). Cost counts every press except `=`, as today.
5. The search can be stopped with Esc and streams lines as it finds them, like the other phases.
6. Status: `School mathematics: N line(s), …` with the usual parts (rejected, notes). No line: `No line reaches G with + - × ÷ in 12 operators or fewer.` Missing an operator (no `÷` in Buttons, or 0 charges): `School mathematics needs + - × ÷ (missing: ÷).`

Examples (S 41, G 48):

- Buttons `+ - × ÷` and digit keys `0`–`9`: `+ 7 = × 1 = ÷ 1 = - 0 =` (8 presses, the least possible: 4 segments of 2 presses).
- Early game, buttons `+ - × ÷`, `10`, `X8`: a line like `÷ 10 = X8 × 10 = ÷ 10 = + 10 = - 10 =` (11 presses). `× 10 = ÷ 10 =` and `+ 10 = - 10 =` are filler pairs that give the number back (while × 10 stays at or below 999 999).

### Mode 2: Just a one

1. **The goal is ignored.** The search solves for S + 1, with these limits:
   - the line ends with `=`, and that is its only `=` (`EngineeringCalculator.OneSend` and a last press of `=`);
   - so the final `op operand =` gives exactly S + 1, starting from S with left edits before the operator.
2. Phases: arithmetic (no remorph lines: they have no `=`), division and modulo, for goal S + 1. No two-operator phase (it has a middle `=`). No edits-only lines.
3. Each line shows which entry it credits, after the count: `[+]`, `[-]`, `[×]`, `[÷]`, `[mod]`, `[%]`, `[digit]`, `[q]`, `[c]`.
4. Ranking: the cheapest line of each credited entry first (by cost), then the rest by the normal ranking. With 10 lines shown, the player sees one line per entry that can be credited this round.
5. Status: `Just a one: S → S+1, credits: ÷, +, digit` (the entries found, cheapest first). No line: `No line makes S+1 with one = this round.` S = 999 999: `Just a one can't go above 999 999.`
6. The app doesn't know which entries this run already has. The player picks a line for one they still need.

### Both modes

- Required, boss rules 11, 12, 13 and 14 are ignored, with a note in the status (`achievement mode ignores boss rule N` / `ignores Required`). Rules 2 and 6 still apply (they only change charges and are applied to Buttons before the search).
- Last works as today (the `last` button).
- With no mode on, nothing changes.

## Out of scope

- Telling which `operators_used` entries the run already has. The mod could publish the array (it is a public field, so reading it stays read-only). Follow-up spec.
- Credits from a middle `=` (a line whose second `=` is the +1).
- The score-animation credit for jumps of 100 / 50 / 25.
- Boss rules 11–14 together with an achievement mode.
- Other achievements (`WITHOUT`, `NONUMBERS`, `SPEEDRUNNER`, …).

## Open questions (Test T)

- T1: School mathematics: a line with `× 1 =` and `÷ 1 =` fillers wins the round and unlocks `SCHOOL`?
- T2: Just a one: `score_is` at a round start equals S (also after a boss round, a shop, a warp)?
- T3: `0` and `10` really don't change `LastButId` (the scene scan found no `Infos` on them): `+ 10 =`-style lines credit the operator?
- T4: after a Just a one line, the round goes on normally from S + 1.
