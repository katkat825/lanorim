# Lanorim

A single-player, GM-run **tabletop-style RPG**. You play one hero — no party to manage, no companion AI to
babysit — led through a branching campaign by a referee behind the screen, on a real tabletop presentation
(grid map, dice, GM screen). Campaigns are moddable and shareable.

*Internal / working title: **Maps & Math Rocks**. The player-facing name is always **Lanorim**.*

**Status:** pre-v1 — the design is settled. The rules engine (combat with reactions and line/cone areas,
spells with a choice of slots or points, Wild Shape), the SRD data, the map model, saves, and the
narrative layer (branching dialogue, hidden GM rolls, encounter and loot tables) are in and unit-tested.
The table comes up in Godot with a map, a few real minis and a d20 tumbling in a tray; there are no
screens (menu, sheet, inventory) yet. Start in `docs/`.

## Where things are

- **`docs/`** — the design record. Start with `docs/README.md`, then `decisions_checklist.md` (what the
  game is) and `v1_build_checklist.md` (what gets built + the definition of "done").
- **`THIRD_PARTY.md`** — provenance and licences for every third-party asset and for the ruleset.
- **`LICENSE`** — proprietary; all rights reserved.
- **`assets/`** — raw art/audio downloads. Not committed (large); `THIRD_PARTY.md` is how they're re-obtained.

## The code

```
core/         pure C#, never references Godot. the rules.
  Dice/       polyhedral dice, the RNG, "2d6+3"
  Space/      grid, cells, walls on the lines, routes, sight, the map file format
  Resolution/ d20 vs a number: advantage, the DC ladder, the nat-1/20 consequence pool
  Characters/ ability scores, skills, conditions, damage types, armor, hit points, an Actor
  Rules/      the front door: checks, saves, death saves, attacks
  Combat/     turn loop, initiative, 2/1/1 actions, the battlefield, reactions, zones, fleeing, enemy AI
  Magic/      the spell-effect primitives, spell slots / spell points, concentration, casting
  Tables/     random-encounter and loot tables, and the GM's hidden rolls
  Localization/  keys, never text
  Statistics/ chi-squared dice fairness

content/      pure C# too. the data, and everything built on top of the rules.
  srd/        the SRD 5.2.1 data, embedded in the assembly: spells, items, classes,
              species, backgrounds, monsters
  Schema/     the JSON reading layer, the locale reader/writer, the Library
  Spells/ Items/ Classes/ Species/ Monsters/   readers and catalogues
  Sheet/      the whole character: Actor + class + pack + spells, Wild Shape, the sheet view
  Creation/   the character creator, as logic
  Inventory/  forty slots, stacking, equipping, the merchant, loot into the pack
  Maps/       the map builder's model: paint, props, spawns, undo, save
  Campaigns/  the campaign package: manifest, loader, the shelf, the demo edition
  Dialogue/   YarnSpinner conversations, barks, hints, and the commands a story sends the table
  Encounters/ Loot/   readers for a campaign's encounter and loot tables
  Saves/      save games and heroes, as ids and what happened

game/         the Godot 4 / .NET project. the only thing that touches Godot. see game/README.md
  Dice/       d4-d20 generated as geometry; the rigid body that throws and settles them
  Tray/       the vessel they land in, and the seam to the rules
  Board/      the map, its tiles, and the minis standing on it
  Table/      the table, the camera, and the ten lines that join dice to rules
  Campaigns/  finding campaign folders on disk and loading their locale
  audio/ Access/ Localization/ shaders/
tools/        the palette bake, the model puller, the impact slicer
core.tests/   xUnit
content.tests/ xUnit
sim/          the balance harness and the dev tools
campaigns/    campaign packages, loose on disk so a Workshop author can edit them (not created yet)
```

**`core/` and `content/` never reference Godot.** That is what buys headless tests, an overnight balance
sim, and the ability to change a rule without opening the editor.

**`core/` emits localization keys, never player-visible text.** English is a locale file like every other
language, and `check-locale.ps1` (and a test) enforce it.

## Build and test

From this folder:

```bash
dotnet test
```

That runs both suites — the rules, the data, the locale audit, and the first playable slice end to end.

```bash
dotnet run --project sim -- balance 2000
```

Plays fights headless and prints the win tables.

The check scripts each **build first**, then print a one-line verdict:

```powershell
.\check-locale.ps1     # does every key the engine emits have English
.\check-content.ps1    # does every SRD file load, and match the design docs
.\check-fairness.ps1   # are the dice uniform
.\check-slice.ps1      # character -> your map -> goblin -> level up, plus the balance tables
```

`sim locale` rewrites `game/locale/game.csv`, keeping every row that already has text and scaffolding
first-pass English for anything new.

Three more need Godot, and find it themselves under `C:\Godot` (or `$env:GODOT_ROOT`):

```powershell
.\check-table.ps1     # does the table come up, do the dice roll, does core read the felt
.\check-play.ps1      # the whole game headless: launch -> table -> the sample campaign to its end, dice on the tray
.\check-dice.ps1      # is the PHYSICS d20 fair - slow, minutes, run it after touching the dice
```

`dotnet run --project sim -- classes [runs] [class]` plays every class at levels 1-5 against the sample
campaign's fights; `sim trace <class> <level> <fight>` shows one of them line by line.

## Godot

The Godot project is opened and run from the editor, not the CLI. The main scene is **`launch.tscn`** —
the title, the campaign book, character creation - which goes to **`table.tscn`**, the table with the map
and the dice tray on it, where the campaign is played. `table.tscn` opened on its own still runs its
demonstration throw. `boot.tscn` is the smaller smoke test that only prints what loaded.

Flags after `--` (for looking and checking): `--begin <campaign> [class]` makes a character with the defaults
and goes to the table; `--auto` plays it to the end; `--autodice` / `--autostory` throw and read on by
themselves but leave the fights to you; `--start <node>` starts the story elsewhere; `--show
book|tutorials|create|settings` opens a launch screen; `--shot <file> --after <frames>` saves a picture and quits.

**After `game/locale/game.csv` changes, re-import it:**

```bash
godot --headless --path game --import
```

Godot reads the `.translation` binary beside the CSV, so a new key is missing at runtime while
`check-locale.ps1` still passes. `boot.tscn` prints `LOCALE STALE` when that has happened.

## Ruleset & legal

The rules are built on the **D&D System Reference Document 5.2.1**, (c) Wizards of the Coast LLC, used under
**CC BY 4.0**. This project is **not affiliated with, endorsed by, or sponsored by Wizards of the Coast**,
and uses none of its trademarks or Product Identity — it is a *tabletop-style* RPG, not a "D&D" product.
Full attribution and asset licences are in `THIRD_PARTY.md`.

The original code and content in this repository are **proprietary — all rights reserved** (`LICENSE`).
Third-party components keep their own licences.
