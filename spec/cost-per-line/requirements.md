# Requirements: Cost per solution line

**Status:** Idea, deferred. Build this once the solver knows the puzzle's full button set.

## Goal

For each candidate solution (for example `3241 mod 103`), work out:

1. The exact **button press sequence** that produces it.
2. The **cost**: total presses, plus how many times each button is used.
3. Whether it is **feasible**: every button's use count fits within the puzzle's limit for that button.

Then rank the candidates so the cheapest feasible solution is shown first, and mark infeasible ones.

## Inputs

A puzzle definition, in addition to S and G:

- **Buttons available**, for example `0X`–`9X` (prepend), `X0`–`X9` (append), `mod`, `÷`, `10`, `reset`, `=`. See the Engineering Calculator table in `CLAUDE.md`.
- **Allowed presses per button.** Each button has a limit, or is unlimited. Each `dX` and `Xd` is its own button and may have its own limit (for example, `3X` usable twice).

Puzzles change from level to level, so the definition is data, not code.

## Calculator entry rules

These rules decide which press sequences are valid and what they cost:

- The **left side** is the display before an operator (the start number, or a result after `=`). The **right side** is the operand after an operator, up to `=`.
- The left-side entry is a **digit string**, not just a number. Leading zeros are kept: `0` → `X4` → `04` (value 4) → `1X` → `104`.
- `dX` / `Xd` each take **1 press** and work on the **left side only**.
- Digit keys (`10` in the early game; `0`–`9` once added) work on the **right side only**, appending their digits to the operand: `÷` `10` `10` → operand `1010`. They are the only way to build an operand, and they can't edit the left side (pressing one there isn't allowed).
- `reset` sets the left-side entry to `0`. It doesn't work on the right side (untested, but believed): after an operator, only digit keys can be pressed.
- An operator (`mod`, `÷`), an operand, then `=` applies the operation. `=` is **free and unlimited**, and the result is the left side again.
- Division is whole-number only: the fraction is dropped.

### Open questions about the rules

- Can operations chain (for example, `X mod m ÷ n`) before `=`?

## Building the press sequence for the "10d" Modulo Shortcut

For a line `(prepend (M + L) to S, padded to k places) mod (10^k + N)`, where the lift L = A when S < G and A − 1 when S > G:

1. **Pad S:** prepend `0` (k − len(S)) times.
2. **Prepend M + L:** prepend its digits from right to left. When L = 0 (S > G at A = 1), this is just M.
3. **mod**
4. **Modulus `10^k + N`**, written as `1` followed by N zero-padded to k digits. Only digit keys work here, so:
   - Press `10`, then the digit keys for the remaining k − 1 digits. Only possible when N < 10^(k−1), and each digit needs its key.
   - Or press the digit keys for every digit of the modulus. Works for any modulus, such as 131.
   - In the early game (only `10`), the modulus must be 10, 1010, 101010, … so most "10d" lines need mid-game digit keys.
5. **`=`** (free).

### Examples

| Line | Sequence | Presses |
|------|----------|---------|
| `3241 mod 103` | `2X 3X mod 10 3 =` | 5 |
| `441 mod 131` | `4X mod 1 3 1 =` | 5 |
| `2005 mod 103` (S = 5, k = 2) | `0X 0X 2X mod 10 3 =` | 6 |
| `431 mod 105` (S = 31, G = 11) | `4X mod 10 5 =` | 4 |

Uses per button for `3241 mod 103`: `2X` ×1, `3X` ×1, mod ×1, `10` ×1, digit key `3` ×1.

Every row needs at least one single digit key (`3`, `1` or `5`), so none of them works with early-game buttons alone. Building the modulus costs 1 press per digit key, and `10` covers a `10` pair in 1 press.

## Output

For each solution line, show:

- The expression (`3241 mod 103`), as now.
- The total press count.
- Uses per button, highlighting any that go over the limit.
- A feasible / infeasible marker.

Default the factor dropdown to the cheapest feasible pair instead of the most balanced one.

Keep every A and k as candidates, for both S < G and S > G. The lowest A usually gives the fewest presses, but press limits can make it infeasible (for example, it needs a digit that has run out), so a longer line from a higher A may be the only one that fits.

## Out of scope for this spec

- Searching across shortcuts or arbitrary press sequences (a general solver).
