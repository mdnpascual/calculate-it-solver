# Calculator Puzzle Solver

[![Solver](https://github.com/mdnpascual/calculate-it-solver/actions/workflows/solver.yml/badge.svg)](https://github.com/mdnpascual/calculate-it-solver/actions/workflows/solver.yml)
[![Mod](https://github.com/mdnpascual/calculate-it-solver/actions/workflows/mod.yml/badge.svg)](https://github.com/mdnpascual/calculate-it-solver/actions/workflows/mod.yml)

Solves the "broken calculator" puzzles in the game **Calculate It**: reach a goal number from a starting number with a limited, unusual set of buttons (prepend and append digits, `flip`, `÷`, `mod`, `+`, `-`, `×`, a `10` key, and so on), and with as few presses as possible. The solver also supports some boss requirements and passive skills, and it takes each key's status and charge count into account.

The repo has two parts:

| Folder | What it does |
|--------|--------------|
| [`CalculatorPuzzle/`](CalculatorPuzzle/README.md) | The **solver**. A Windows app that takes a start number, a goal and the available buttons (with charges), searches for short press sequences, and lists the cheapest ones. It can follow the game live by watching the puzzle file the mod writes. |
| [`GameHook.MelonLoader/`](GameHook.MelonLoader/README.md) | A **read-only MelonLoader mod** for Calculate It. It reads the current puzzle (start, goal, buttons and their charges) from the running game and writes it to a JSON file for the solver. It never presses buttons or changes game state. |

## Why this project exists

I built this project with two goals:

1. **Try REA as a fast way to build a game mod.** [REA](https://github.com/morluto/rea) is a reverse-engineering toolkit that Claude Code uses as an MCP server, through the `reverse-engineer-anything` skill. With it, I inspected the game by decompiling its code to find the values the mod needs to read. Each finding was then checked in the running game against the screen (see [`user-verification/`](user-verification/)), so the mod reads what the game really does, not guesses.

2. **Steer Claude Code with spec-driven development and many tests.** Every feature started as a spec in [`spec/`](spec/): requirements first, then a design, then a self-contained prompt for the next session. Claude worked from these files instead of from memory, so each session followed the same rules and code patterns.

## Requirements

- Windows 10 or 11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0). Check it with `dotnet --version`.

For the mod only:

- Calculate It (Steam). The mod build references the game's DLLs in place. The default game folder is `D:\SteamLibrary\steamapps\common\Calculate It`; change `GameDir` in `GameHook.MelonLoader/GameRefs.props`, or pass `-p:GameDir="<path>"` to the build.
- [MelonLoader 0.7.3](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3) installed in the game. See [`user-verification/install-and-remove.md`](user-verification/install-and-remove.md) for install and removal steps.

## Project layout

| Path | Contents |
|------|----------|
| `CalculatorPuzzle.sln` | Solution with the solver and its tests (the mod is not in it) |
| `CalculatorPuzzle/` | Solver: WinForms app (.NET 9) |
| `CalculatorPuzzle.Tests/` | xUnit tests for the solver |
| `GameHook.MelonLoader/` | Read-only game mod (net472, MelonLoader 0.7.3) |
| `docs/` | Reference notes, such as the game's button ids (`button-id.md`) |
| `spec/` | One folder per feature: requirements, design and next prompt |
| `user-verification/` | Installing and removing the mod, and one file per in-game test |
| `CLAUDE.md` | Puzzle rules and project notes for AI agents |

## Build

Run these from the repo root.

```
dotnet build -c Release
dotnet build GameHook.MelonLoader -c Release
```

The first line builds the solver and its tests, the second the mod. Outputs:

| Part | Path |
|------|------|
| Solver | `CalculatorPuzzle\bin\Release\net9.0-windows\CalculatorPuzzle.exe` |
| Mod | `GameHook.MelonLoader\bin\Release\net472\CalcHook.MelonLoader.dll` (copy it into `<game>\Mods\`) |

## Testing

```
dotnet test -c Release
```

- **486 automated tests** (xUnit, in `CalculatorPuzzle.Tests/`) cover the solver, the press simulator and the search.
- **34 feature specs** in [`spec/`](spec/), each with requirements, a design and a next prompt.
- **23 manual in-game tests** in [`user-verification/`](user-verification/) check the mod against the running game.
