# Requirements: Prime button (`operator_prime`)

**Status:** Complete (2026-10-24). Test I confirmed in-game.

## Goal

Support the `operator_prime` button. It sets the left side to the **nearest prime**; when two primes are equally near, it takes the **lower** one.

1. **Solver:** a new left-side transform with token `p` (Buttons box `p:2`, game id `operator_prime`). The arithmetic phase finds lines that use it, like the other transforms.
2. **Mod overlay:** the calculator overlay shows the button as `p<nearest prime>` for the current left side (`p8081` while the left side is `8084`), like `c12` and `q8`. The shop overlay shows the offer as `p`.

## Background (ilspycmd on `Assembly-CSharp.dll`, 2026-10-24)

| What | Where in the game |
|------|-------------------|
| Press | `MainCalc.Prime()`: `total = FindNearestPrime(total)`, `output.text = total.ToString()`, unblocks its own button. It reads the number (`total`), so leading zeros drop. |
| Nearest prime | `MainCalc.FindNearestPrime(long n)`: `n < 2` gives `2`. Otherwise it checks `n`, `n`, then `n − 1`, `n + 1`, then `n − 2`, `n + 2`, …, and the **lower candidate is checked first** at each distance. So a tie goes down. |
| Prime test | `MainCalc.IsPrime(long n)`: `n < 2` is not prime; trial division by `i = 2, 3, …` while `(float)i <= Mathf.Sqrt(n)` (float math). |
| Hover text | `PrimeControll.Update` adds `(<nearest prime>)` to the button's description every frame. So the game already shows the value on hover; the overlay shows the same number. |
| Not like the other transforms | `Prime()` has no `if (base.enabled)` check, doesn't set `can_send = false` and doesn't block the number keys or unblock the operator buttons, unlike `Round()`, `Reverse()`, `Last()`. No effect on solver lines. |
| Left side only | Confirmed by the user in-game (2026-10-24): it does nothing on the right side. |
| Winning | The round is won only after `=` (user, 2026-10-24), also when `p` puts the goal on the display. Lines don't show that final `=`; the player presses it anyway, as for every line. |

Checked against the user's in-game tests (a Python copy of `FindNearestPrime` / `IsPrime` with float32 math gives the same results):

| Left side | Result | Why |
|-----------|--------|-----|
| 8084 | 8081 | 8081 and 8087 are both 3 away: tie, lower one |
| 41 | 41 | already prime |
| 93 | 89 | 89 and 97 are both 4 away: tie, lower one |
| 33 | 31 | 31 is 2 away, 37 is 4 away |
| 55 | 53 | |
| 88 | 89 | 89 is 1 away, 83 is 5 away: nearest, not always down |
| 6 | 5 | 5 and 7: tie, lower one |
| 80 | 79 | |
| 0, 1 | 2 | `n < 2` gives 2 |
| 999 999 | 1 000 003 | |

**The float bound:** `IsPrime` stops at `Mathf.Sqrt((float)n)`. Below 2^24 (16 777 216) `n` is exact as a float and the square root rounds correctly, so no divisor is skipped. Above it the rounding error is about a quarter of a float step at the root, which rounds back up to the integer root, so the game's test equals true primality there too. A sweep of p² for every prime p from 4 097 to 20 000 found no difference. The solver uses true primality.

## Rules

### Solver

1. **Token:** `p`. `GameLink.MapButton("operator_prime")` returns `p`. The Buttons box accepts `p` with a charge (`p:2`); parsing is the transform catalogue's (any case).
2. **Apply:** `v < 2` → `2`; otherwise the nearest prime to `v`, ties going down. A result of 10^18 or more is `max digits reached` (Apply returns null), like the other transforms. (Corrected 2026-10-25: the game has no `max digits reached`, and `p` isn't clamped: `999999` → `1000003`; see [`display-cap/`](../display-cap/).)
3. **Reads the number:** leading zeros drop (`05` → `5`), like `flip`, `round` and the other number transforms.
4. **Left side only** (confirmed in-game), one press, uses one charge. A line that ends with `p` doesn't add a final `=` (the player presses it to win, as for every line).
5. **Preimages (backward search):** for a prime `p`, every `v` that maps to `p`: from `floor((q + p) / 2) + 1` to `floor((p + r) / 2)`, where `q` is the previous prime and `r` the next one (for `p = 2`: from `0`). Ascending. A non-prime, or a display with a leading zero, has none. Examples: `2` ← `0, 1, 2`; `3` ← `3, 4`; `5` ← `5, 6`; `89` ← `87 … 93`.
6. **Search phases:** the arithmetic phase uses it through `LeftSearch` (forward and backward), like every transform. The division and modulo phases don't change (same as `left-transforms/`).
7. **Primality** is exact for every `v` below 10^18 (deterministic Miller–Rabin), fast enough for the BFS.

### Mod (stays read-only)

8. **Calculator overlay:** the `operator_prime` cell shows `p` followed by the nearest prime of the current left side (`MainCalc.total`), for example `p8081`. It updates when the left side changes. The mod computes the number itself (`ButtonSymbols.NearestPrime`) and caches it per left value; it doesn't call game methods.
9. **Shop overlay:** an `operator_prime` offer shows `p`.
10. `ButtonSymbols.Token("operator_prime")` returns `p` (it mirrors `GameLink.MapButton`; the linked test checks both).
11. `puzzle.json` and `state.json` don't change: the button id was already published.

## Examples

Buttons `p:2, X1:2, ÷:2, 10:5`, start `88`, goal `89`: `p` (1 press).

Start `80`, goal `79`, Buttons `p:1`: `p` (1 press).

Start `120`, goal `113`, Buttons `p:1`: `p` (1 press). 113 and 127 are both 7 away, so the tie goes down.

Start `42`, goal `43`, Buttons `p:1, X++:1`: `X++` (1 press). `p` gives `41` here (41 and 43 tie), so it must not be offered.

## Out of scope

- Using `p` in the division and modulo phases' left edits (the transforms aren't used there yet).
- The game's slow trial division on very large left sides (the game, not the solver, could stall).

## Open questions

None. (Answered by the user, 2026-10-24: `p` works on the left side only; the round is won after `=`, which lines leave implied.)
