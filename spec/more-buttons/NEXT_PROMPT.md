# Next prompt: More buttons

**Status:** Done (2026-10-24). Kept for reference; Test H is done except H1, which stays unconfirmed.

Add eight Calculate It buttons to the calculator puzzle solver and its game mod:

- the charge key `number_charge` (token `c`; it types its charge count, so lines show `c12 c11`)
- the quest key `number_quest` (`q8`; it types a random digit that's picked again after each press, so at most one press per line)
- percent `operator%` (`%`: left × operand / 100, fraction dropped)
- `operator_last` (`last`: the previous round's goal; the mod publishes it, and the app gets a Last box)
- `operator_remorph_a` / `_b` (`a->c` / `b->c`: an operator plus one key, with no `=`)
- `operator_100-x` (`100-X`)
- `operator_flip` (`25|69`: swaps 2↔5 and 6↔9)

The mod's Alchemy overlay shows the live `c12` / `q8` values and the new tokens. The repo is C#: .NET 9 WinForms + xUnit, plus a net472 MelonLoader mod. Not a git repo; don't run `git init`.

## Read first

1. `CLAUDE.md`: project rules, the Engineering Calculator button table, and the Status list.
2. `spec/more-buttons/requirements.md` and `design.md`. The game rules there come from the decompiled game code (`MainCalc.Percent`, `Remorph`, `AddNumber`, `Last`, `Flip`, `X_ops_Y`; `UsesNumber`, `QuestionNumber`; `StartLvl.last`). To look again: `ilspycmd -t MainCalc "D:\SteamLibrary\steamapps\common\Calculate It\CalculateIt_Data\Managed\Assembly-CSharp.dll"`.
3. `docs/button-id.md`: the game ids.
4. Code: `CalculatorPuzzle/EngineeringCalculator.cs` (`Key`, `Press`, `Buttons`, `Parse`, `Run`), `LeftTransforms.cs`, `DivisionShortcut.cs` (`TypeOperand`, `CheapestDivisor`, `Search`), `ArithmeticSearch.cs` (`Find`), `GameLink.cs`, `MainForm.cs` (Required box, `ApplyGamePuzzle`, `StartDivisionSearch`), `GameHook.MelonLoader/CalcHookCore.cs` (`ReadButtons`, `WritePuzzle`, the state check, the overlay draw) and `ButtonSymbols.cs`.
5. `CalculatorPuzzle.Tests/` for the test style (`TestButtons.Parse` adds `:9` to tokens without a count). `ButtonSymbolsTests.cs` is the mirror test between `ButtonSymbols.Token` and `GameLink.MapButton`.

## Steps

1. Run `dotnet build` and `dotnet test` and note that everything passes (352 tests on 2026-10-24).
2. Open question 2: check the `operator_100-x` onClick argument with UnityPy (see the `unity-re-tools` notes: find the MonoBehaviours first, then set the typetree generator). Adjust the token if it isn't 100.
3. Keys, `Press.ChargeKey`, the `Buttons` fields, `Parse(text, last)`, `Token`, and `Fits`/`Spare`/`Drained`/`Uses` by `ChargeKey` (design §1–2), with their tests.
4. The simulator (design §3) and its tests.
5. `100-X`, `25|69` and `LeftTransforms.Last` (design §4) and their tests.
6. `TypeOperand` and `CheapestDivisor` (design §5). All existing division and modulo tests must pass unchanged.
7. The arithmetic phase: `%` lines, then remorph lines (design §6). Time 41 → 483 as described there (Release).
8. The division phase gate (design §7).
9. `GameLink` (design §8) and the Last box (design §9).
10. The mod (design §10). Build it with `dotnet build GameHook.MelonLoader -c Release`. It must stay read-only.
11. Run the manual checks with `dotnet run --project CalculatorPuzzle`. Write Test H into `user-verification/` (the user runs it in-game).
12. Update the Status lines in all three spec files, add an "As built" section to the design (timing, anything that differs), and update `CLAUDE.md` (button table, Status list) and `docs/button-id.md`.

## Check the work

- `dotnet build` with no warnings. The mod builds in Release.
- `dotnet test`: all tests pass, old and new.
- Manual: 95 → 23 with `%:1, 2:1, 5:1` shows `% 2 5 =` first. 483 → 73 with `a->c:1, 7:1` shows `a->c 7`. 10 → 1221 with `+:1, c:12` shows `+ c12 c11 =`. `last:1` with an empty Last box shows the `no Last value` note.
- Esc still stops the search, and the division and modulo phases still run afterwards.
