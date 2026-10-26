# Calculator Puzzle Solver

## Purpose

This repo solves "broken calculator" puzzles. Each puzzle gives:

- a **starting number** (the value already on the display),
- a **goal number** to reach,
- a **set of available buttons**: digits, operations, and special keys that may be limited, broken, or behave unusually.

The solver must find a sequence of button presses that turns the starting number into the goal number. Shorter sequences are better.

## Puzzle examples

### Easy: standard calculator

- Start: `41`, Goal: `48`
- Buttons: digits `0-9`, `+`, `-`, `*`, `/`
- Solution: `+` `7` `=` → `41 + 7 = 48`

### Hard: Engineering Calculator

The game mode with `mod`, `÷`, `10`, `reset` and digit prepend/append buttons.

- The **left side** is the display before an operator: the start number, or a result after `=`.
- The **right side** is the operand after an operator, up to `=`.

| Button | Where it works | Effect | Cost |
|--------|----------------|--------|------|
| `dX` (e.g. `4X`) | Left side only | Prepend digit d: `41` → `441` | 1 press |
| `Xd` (e.g. `X4`) | Left side only | Append digit d: `41` → `414` | 1 press |
| Digit keys: `10` early game; single keys `0`–`9` can be added later | Right side only | Append the key's digits to the operand: `÷` `10` `10` → operand `1010`; `÷` `4` `5` → operand `45`. On the left side they do nothing (the press isn't allowed), so the left side gets digits only from `dX` and `Xd` | 1 press |
| `X++`, `X--`, `X+k`, `X-k`, `X*2`, `X/2`, `<<`, `>>`, `sqr`, `sqrt`, `a->b`, `to25`, `round`, `p`, `near`, `median`, `sup`, `money` | Left side only | Change the value in one press (`41` → `42`, `9` → `81`, `3->0` on `35` → `05`; `-` stops at 0; digit maps keep leading zeros, the others drop them; `X+k`, `X*2`, `<<`, `sqr` clamp results above 999 999 to 999 999 and play goes on; `p` and `round` aren't clamped). `near`: the goal if within 10, else + 10. `median`: `ceil((L + G) / 2)`. `sup`: digit sum, display under 6 characters. `money`: `+` the player's coins, display under 5 characters (on a longer display the press does nothing but still uses its charge) | 1 press |
| `100-X`, `25\|69`, `last` | Left side only | `100-X`: `100 - L` (0 above 100). `25\|69`: swaps 2↔5 and 6↔9. `last`: the previous round's goal (the mod publishes it; the app has a Last box) | 1 press |
| `swap` | Left side only | The display and the goal trade places, then the game sends (`operator_swap`). The solver uses it as the first press only, at most once: the line is `swap` plus a line from G to S | 1 press |
| `reset` | Left side only (right side untested, believed not to work) | Set the entry to `0` | 1 press |
| `flip` | Left side only | Reverse the number: `41` → `14`, `123` → `321`, `120` → `21` (leading zeros drop; then `1X` → `121`) | 1 press |
| `÷`, `mod`, `+`, `-`, `×` | Left side | Start an operation; the right side begins. One operation per `=`; `-` below 0 gives 0; `+` and `×` results above 999 999 become 999 999 and play goes on | 1 press |
| `%` | Left side starts it | `left × operand / 100`, fraction dropped, `=` applies it; results above 2^24 rejected | 1 press |
| `c` (charge key) | Right side only | Types its current charge count (`c12 c11` at 12 charges → operand `1211`); the value drops by 1 per press | 1 press |
| `q` (quest key) | Right side only | Types its current digit (`q8`); then the digit is re-rolled, so at most one press per line | 1 press |
| `mD` (mw key, once it stores a number) | Right side only | Types its stored number (`m5` types 5); its charges are its own. An empty mw is ignored | 1 press |
| `a->c`, `b->c` | Left side starts it | Operator plus exactly one right-side key, no `=`: `a->c` gives `(L mod 10) + 10k`, `b->c` gives `(L / 10)·10 + k` (`483` `a->c` `7` → `73`) | 1 press + the key |
| `==`, `AX` | Left side starts it | Operator plus exactly one right-side key, no `=`: `==` turns every digit of `L` into the key's first digit (`483` `==` `7` → `777`); `AX` (display under 6 characters) prepends the key's whole number (`41` `AX` `10` → `1041`) | 1 press + the key |
| `=` | Right side | Apply the operation; the result is the left side again | Free, unlimited |

**Labels.** The solver app and the mod overlays show the in-game label, not the token used in this table: `c12` → `volt12`, `q8` → `unk8`, `m5` → `mem5` (empty `mem`), `money` → `$`, `flip` → `reverse`, `round` → `->)`, `sup` → `combine`, `sqr` → `X²`, `median` → `_2_`, `25|69` → `mirror`, `==` → `equalize`, `<<` / `>>` → `X<<` / `X>>`, `near` → `+-10`, `reset` → `x->0`, `to25` → `x->25`. The Buttons and Required boxes accept both (`spec/button-labels/`).

Each `dX` and `Xd` is its own button, so a puzzle can have `4X` without `X4`. The display holds 6 characters: `dX` and `Xd` do nothing on a 6-character display (leading zeros count), and an operand key appends only while the operand has fewer than 5 characters (`10 10 10` = `101010` is the longest `10` operand). The solver follows both rules (`EngineeringCalculator.Max`, `MaxDisplay`, `MaxOperandTyping`; `spec/display-cap/`). `Xd` and digit maps edit the display text, so leading zeros stay (`0` `X4` = `04`, `35` `3->0` = `05`). `dX`, `flip`, the number transforms and the operators read the number, so they drop them (`04` `1X` = `14`, `04` `flip` = `4`). In the early game the only digit key is `10`, so an operand can only be 10, 1010, 101010, … Division is whole-number only: the fraction is dropped (`8411 ÷ 1010 = 8`).

Solutions for start `41`, goal `48`:

- Division, early game: `÷`, `10` → operand `10`, `=` → `4`, `X8` → `48` (3 presses). `÷ 10` drops the last digit.
- Division with `flip`, no `X8`: `8X` → `841`, `÷`, `10`, `=` → `84`, `flip` → `48` (4 presses)
- Division, early game: `X1` → `411`, `8X` → `8411`, `÷`, `10`, `10` → operand `1010`, `=` → `8`, `4X` → `48` (6 presses)
- Modulo, needs digit key `3`: `2X` → `241`, `3X` → `3241`, `mod`, `10`, `3` → operand `103`, `=` → `3241 mod 103 = 48` (5 presses)

Things the hard example shows:

- `dX` / `Xd` edit the left side only: the start number, the number being built before an operator, and a result after `=`. They can't touch an operand.
- An operand is built from digit keys only: after an operator, only digit keys can be pressed. `10` can be its seed, followed by more digit keys.
- Digit keys (`10` and `0`–`9`) work on the right side only. They can't edit the start number or a result; use `Xd` to append a digit there.
- Operations beyond `+ - * /` (such as `mod`) are in scope, and the button set changes from puzzle to puzzle.

## Design principles (for when implementation begins)

- **Buttons as data.** Each puzzle defines its own button set. Model every button as a state transformation, so new or weird buttons can be added without touching the search logic.
- **Calculator state is more than one number.** Track the displayed/accumulated value, any pending operator, and the operand being built, so that sequences like `mod` → `10` → `3` → `=` are modeled correctly.
- **Search, don't hand-solve.** Use a search such as BFS or iterative deepening over button presses so the first solution found is the shortest. Prune with bounds (max press count, max magnitude, visited states).
- **Edge cases to handle explicitly:** division/mod by zero, non-integer results (decide per puzzle whether they are allowed), negative numbers, and how prepend/append treat negatives and zero.
- **Output a press sequence** a human can follow, showing the intermediate value after each press.

## Status

Prototype stage. Not a general solver yet; it lists candidate solutions from known formulas.

- `CalculatorPuzzle/`: C# WinForms solver app (.NET 9). Phases: arithmetic, then two-operator lines, then Division Shortcut, then Modulo Shortcut (boss rule 14 wraps them in `GoalStepSearch`: goal G + k for k = 1, 2, …; the Buttons box takes `:1g` for glass buttons), all checked in the press simulator (`EngineeringCalculator.Run`) and against button charges. Per-file notes: `CalculatorPuzzle/README.md` ("Files"); read it before changing solver code, and update a file's note there when you add or change that file.
- `CalculatorPuzzle.Tests/`: xUnit tests for the shortcut logic, the simulator and the left-edit search.
- `GameHook.MelonLoader/`: MelonLoader mod for Calculate It that publishes each settled puzzle to `%LOCALAPPDATA%\CalcHook\puzzle.json` (the app's Follow game reads it) and draws overlays. Not in the solution; build with `dotnet build GameHook.MelonLoader -c Release`. It must stay read-only: the game unlocks Steam achievements, so it never presses buttons, changes charges or writes game or save files. Game fields, JSON fields and overlays: `GameHook.MelonLoader/README.md`.
- `docs/`: `button-id.md` (game button ids) and `boss-rules.md` (every boss rule: `bossRule`, the text the game shows, what it does, solver status).
- `user-verification/`: manual in-game tests, one file per test (`test-b.md` … `test-j.md`), an index in `README.md`, and `install-and-remove.md` (install and remove MelonLoader). Read only the file you need. New manual test = new `test-<next letter>.md` plus a row in the index; never append to a shared file. After Z the letters continue like spreadsheet columns: `test-aa.md`, `test-ab.md`, … Order comes from the index table, not the folder listing (`test-aa` sorts before `test-b`). Spec `NEXT_PROMPT.md` files say "add Test X as `user-verification/test-x.md`".
- `spec/`: one folder per feature spec (see "Specs" below). Status of every spec (what it covers, complete or not, test counts, follow-ups) is in `spec/README.md`; read it when a task names a spec or touches a feature that may have one.
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project CalculatorPuzzle`
- Git repo on `main`, remote `origin` = `https://github.com/mdnpascual/calculate-it-solver.git`. License: GPLv3 (`LICENSE`).

## Specs

Each feature spec gets its own folder, `spec/<feature-name>/` (kebab-case), with three files:

- `requirements.md`: what the feature must do and why. Inputs, rules, expected output, examples, open questions, out of scope.
- `design.md`: how it will be built. Types, where code goes, algorithms, test plan.
- `NEXT_PROMPT.md`: a self-contained prompt for the next agent to start implementing the spec. It names the files to read first (requirements, design, related code), the first steps, and how to check the work (build, tests).

Always create all three files. If a part isn't ready yet (for example, the feature is just an idea), still create the file with a heading and a `**Status:** Deferred.` line saying what it is waiting on. Fill in the files in order: requirements, then design, then next prompt.

Add a line for each new spec to `spec/README.md`, and update it when the spec's status changes. Don't put spec status in this file.
