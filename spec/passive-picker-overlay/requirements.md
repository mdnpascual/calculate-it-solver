# Requirements: Passive picker overlay (challenge 9)

**Status:** Done (2026-10-27). Implemented and tested in game (Test Z, except Z6). Size 75% of the screen width, at most 25% of its height: confirmed good in Z7.

## Goal

In challenge **9** (the game's *Alchemy*, called *redacted* in this repo: one mode, not two) the game shows the wrong text for passives. When the passive picker opens (before round 1, and after the boss of round 5, 10, 15, …), each of the 3 offered passives has a name and description, but in challenge 9 they are Latin button texts, not the passive's own text.

Extend the read-only mod: while the passive picker is open in challenge 9, draw an overlay at the top center of the screen with 3 boxes, one per offered passive, in the same left-to-right order as the game. Each box has the passive's real title and, below it, its real description.

The solver app doesn't change.

## Background (REA `inspect_managed_members`, evidence `ev_816b6fd3…`, ilspycmd and UnityPy on the game files, 2026-10-27)

| What | Where in the game |
|------|-------------------|
| Picker panel | `StartLvl.passiveshop` (public `GameObject`). `StartLvl.Update` turns it on when `round % 5 == 0` and the player owns fewer than 4 passives (`passive_tag` objects) and it isn't the tutorial (`passiveshop.active = true`, `notwork = true`). The tutorial turns it off. No code turns it on for the first pick, so before round 1 it is on because the scene has it on (inferred; Test Z checks it). After a pick (`PassivePool.CreatePassive`, `StartLvl.PassiveChoosed`) the game turns it off. |
| The 3 offers | `PassivePool` on `passiveshop` (`passiveshop.GetComponent<PassivePool>()`, as `RunSave` does). `PassivePool.passives` is a `RandomisatorPassives[]`; `OnEnable` sets `passives[0..2].id` to 3 unique passive indexes (0-based, `SeedMaker.Seed` order; skips owned and locked passives). `RandomisatorPassives.OnEnable` sets the offer's `Infos.id = id + 1`. |
| Owned passives | `PassivePool.CreatePassive` puts them under `pas_parent`, up to 4. Not part of this overlay. |
| Text of a passive | `Infos.Update` (passive, no `Randomisator`): `name = TextsLoader.pasnames_loc[id - 1]`, `description = pasdis_loc[id - 1]` (`r…` arrays when `lang` is `rus`). The hover box (`Descriptionframe`) shows `Infos.name` and `Infos.description`. |
| Why the text is wrong | `TextsLoader.Awake` in scene `main`, challenge 9: it loads **`textAsset_lat`** (asset `Localisation_eng_latin`, the Latin text of the 47 **buttons**) into the button arrays **and into the passive arrays**. So passive id n shows the Latin text of button n: passive 1 (Near1) shows `Accretio: Unum numerum alteri addit` (the Sum button). In other modes it loads `textAsset_eng_p` (`Localisation_eng passives`) and `textAsset_p` (`Localisation_rus passives`) for passives. |
| The real text | `TextsLoader.textAsset_eng_p` and `textAsset_p` are public `TextAsset` fields, still set in challenge 9 (only not read). `TextsLoader` is on `GameObject.Find("TextsManager")`. Format, one passive per line: `1. Near1: You win the round, even if you are 1 short of the required amount;`. The game's parser (`ReadTextAsset`): split on `\n` (empty lines dropped), take the text between the first `.` and the first `;`, split on `:`, `[0]` is the name and `[1]` the description, both trimmed. Line i is passive index i. 24 passives (Near1 … Hp Up). |
| Alchemy masking | `AlchemyChallenge.LateUpdate` masks calculator buttons and `Randomisator` offers only. The passive icons are not masked; only the text is wrong. |

## Rules

### Mod stays read-only

1. IMGUI boxes and labels only (no `GUI.Button`), no new game objects or components, no new Harmony patch, no game method calls except Unity reads (`GetComponent`, `GameObject.Find`, `TextAsset.text`). No assignment to `Infos`, `TextsLoader` or any game field: the overlay doesn't fix the game's text, it draws the real text over the game.

### When the overlay shows

2. The passive panel is drawn when **all** of these hold:
   - challenge is 9 (`GamePrefs.GetInt("challenge") == 9`), checked every refresh;
   - the mod has the level (`StartLvl`, scene `main`);
   - the picker is open: `StartLvl.passiveshop.activeInHierarchy`;
   - at least one offer has a text (rule 5);
   - the overlay is on (`_overlayOn`, the F9 toggle; it turns on by itself when a challenge 9 run starts, as today). In challenge 9, F9 hides and shows the passive panel together with the other panels (decided).
3. **Only in challenge 9, never anywhere else.** In every other challenge (and in the menu) the passive panel is not drawn, even when F9 has turned the other overlays on. F9 there still toggles the calculator and shop panels as today. The challenge check is in the draw path itself (not only in the refresh), so a stale offer list from an earlier challenge 9 run can't show either.

### Content

4. Three boxes, one per entry of `PassivePool.passives` whose object is `activeInHierarchy`, ordered left to right by their screen x (`RectTransform.position.x`), so the boxes match the game's order even if the array order differs.
5. Each box: the passive's **title** (bold, larger) on top and its **description** below, word-wrapped, both from the game's own passive text asset for index `RandomisatorPassives.id`: `textAsset_eng_p`, or `textAsset_p` when `GamePrefs.GetString("lang") == "rus"`. Parsed the same way as `TextsLoader.ReadTextAsset`. An index outside the parsed list, or an empty text, gives a box with `passive <id + 1>` as the title and no description.
6. The text assets are parsed once per `TextsLoader` instance and language (cache), not every frame.
7. **Header** above the boxes: `Passives` (small, like the other panel headers).

### Placement and size

8. Anchored at the **top center**: centered horizontally, 16 px from the top (1080p units, same scale `s = Screen.height / 1080` as the other panels). It must not block the game's picker (the icons and their hover boxes): that is what the height cap is for, and the user checks it in Test Z7.
9. Width: **75% of the screen width** (a named constant so it is easy to change after playtesting). The 3 boxes split it evenly with the usual 4 px gaps and 8 px padding.
10. Height: fits the tallest description (all 3 boxes the same height), but the whole panel (header included) is **at most 25% of the screen height** (a named constant too). Font sizes: title 18, description 14 at 1080p (constants). If the text doesn't fit under the cap, both font sizes go down 1 at a time (description not below 10, title not below 12) until it fits; if it still doesn't fit, the panel stays at the cap and the descriptions are clipped at the bottom of their boxes.
11. Same look as the other panels (dark semi-transparent background, `_boxStyle`-like), so it reads as part of the mod.

### Data

12. `state.json` gets `passivePicker`: `null` when the panel would not show (closed, or not challenge 9), else `{ "open": true, "offers": [ { "id": 0, "name": "Near1", "description": "…", "x": … } ] }`. The change line that `Poll` logs gets ` passives=[Near1, Odd, Lucky]` while it is open, so each picker is logged once. `puzzle.json` doesn't change.
13. **Errors:** a failure reading the picker or parsing the text is logged once through `Report` and the panel is skipped; the other overlays keep working.

## Examples

Challenge 9, round 5 boss beaten, picker offers ids 0, 8, 18. The game shows `Accretio`, `Fors`, `Gradus` with Latin descriptions. The overlay, top center, 75% of the screen width:

```
 Passives
┌──────────────────────────┬──────────────────────────┬──────────────────────────┐
│ Near1                    │ Odd                      │ Lucky                    │
│ You win the round, even  │ When you get an odd      │ When you get a number    │
│ if you are 1 short of    │ number, get one coin     │ that is a multiple of 7, │
│ the required amount      │                          │ you will get 1 coin      │
└──────────────────────────┴──────────────────────────┴──────────────────────────┘
```

Same picker in challenge 8 with F9 on: no passive panel (the game's text is correct there).

## Out of scope

- The owned-passive strip (`pas_parent`) and its hover text, which is wrong in challenge 9 too (same arrays). Possible follow-up: show it in the same panel.
- The collection screen.
- Fixing the game's text (would write game fields).
- Advice on which passive to pick.

## Open questions

1. (Answered, 2026-10-27.) F9 hides the passive panel in challenge 9 too, like the other panels. It still never shows outside challenge 9 (rule 3).
2. (Answered, 2026-10-27, Z1.) Yes: the picker is open before round 1 and the panel shows.
3. (Answered, 2026-10-27, Z7.) It must not block the game's picker: 75% of the screen width, at most 25% of its height (rule 10). Size confirmed good in game.
4. (Not verified.) Russian (`textAsset_p`, `lang == "rus"`): the code path exists and costs nothing, but Z6 was skipped, so Cyrillic rendering in the default IMGUI font is untested. Only English is officially supported.
