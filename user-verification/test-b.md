# Test B: mod loads and reads the state (done, 2026-10-22)

| Loader | Game starts | Plugin loads | Values correct | Notes |
|--------|-------------|--------------|----------------|-------|
| BepInEx 5.4.23.5 | not tested | not tested | not tested | dropped |
| MelonLoader 0.7.3 | yes | yes (after moving the DLL out of a subfolder) | yes | Engineering Calculator. The 4 → 24 solve `X1 ÷ 10 = X2 flip` matched the log press by press, charges included. On Continue, a fresh round (98 → 63) is logged before the restored one (4 → 24). |

Findings: `operand=999999999` means no operand is pending, and `op=4` is ÷.
