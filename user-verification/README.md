# User verification (in-game tests)

One file per manual in-game test. Read only the file you need. Install and removal steps: [install-and-remove.md](install-and-remove.md).

| Test | File |
|------|------|
| B | [mod loads and reads the state](test-b.md) |
| C | [game state scan](test-c.md) |
| D | [glass buttons](test-d.md) |
| E | [Market Demand](test-e.md) |
| F | [Redacted overlay](test-f.md) |
| G | [Shop overlay](test-g.md) |
| H | [More buttons](test-h.md) |
| I | [Prime button](test-i.md) |
| J | [Shop charge total](test-j.md) |
| K | [Boss required button](test-k.md) |
| L | [Boss goal per charge](test-l.md) |
| M | [Two-operator lines](test-m.md) |
| N | [Boss rule 12, every = adds 1](test-n.md) |
| O | [999 999 cap and 6-character display](test-o.md) |
| P | [Boss rules 2 and 9, blocked buttons](test-p.md) (done) |
| Q | [Boss rule 6, 2 charges per press](test-q.md) (done) |
| R | [Boss rule 13, one = per line](test-r.md) (done) |
| S | [Warp re-scan](test-s.md) (done) |
| T | [Achievement modes](test-t.md) (done) |
| U | [Near-one passive](test-u.md) (done) |
| V | [near, median, sup, money, ==, mw, AX](test-v.md) (done) |
| W | [operator_swap](test-w.md) (done) |
| X | [Button labels](test-x.md) (done) |
| Y | [CI-built mod (stub build)](test-y.md) |
| Z | [Passive picker overlay (challenge 9)](test-z.md) (done; Z6 not run) |
| AA | [coinsub (`$++`) fallback](test-aa.md) (AA1, AA3, AA4, AA6 done; rule 11 with `$++` required gave `$++ (1 press)`, `×`, `0`, `=`, `5X` for S 76, G 50 (not yet played); AA2: the loop ran to the end of the money and found no line, which is correct (the only line ended at G-2, outside Near 1); a played `$++ (n press)` line is still open; AA5 not run) |
| AB | [Blind passive in boss rule 14](test-ab.md) (AB1-AB5 all done; AB3: S 85, G 9, `mirror`, `combine` won; AB4: S 19, G 44, glass `X>>` first, solved) |
| AC | [Blind passive and the empty count](test-ac.md) (AC1-AC3 all done) |

## Adding a test

New manual test = new file `test-<next letter>.md` (heading `# Test <letter>: <name>`, steps numbered `<letter>1`, `<letter>2`, ..., each with an "Expected" line) and one row in the table above. After the user reports, put `(done, <date>; ...)` in the heading. Don't add tests to a shared file. After Z the letters continue as AA, AB, … (`test-aa.md`); the table above gives the order, not the folder listing.
