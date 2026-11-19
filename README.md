# Lanorim

Lanorim is a single-player, tabletop-style RPG. You play one hero, with no party to manage and no companion AI
to babysit, and a GM runs you through a branching campaign on a virtual tabletop: a grid map, painted minis, a GM
screen and a tray of physics dice. Campaigns are moddable and shareable.

The internal working title is *Maps & Math Rocks*. Anything player-facing says **Lanorim**.

## Status

Pre-v1. The design is settled and most of the game is in place:

- **Rules engine:** combat with reactions and line/cone areas, 123 SRD 5.2.1 spells (cast with either spell slots
  or spell points), and Wild Shape. All unit-tested, with the SRD data checked against the SRD text.
- **Systems:** the map model, save games, and the narrative layer (branching dialogue, hidden GM rolls, encounter
  and loot tables).
- **Screens:** everything is wired up in Godot. That covers the title and campaign book, character creation, the
  table (map, minis, GM screen, dice tray), dialogue, the combat HUD, the character sheet, pack and merchant,
  level-up, pause, death and the ending. The map builder and the credits are reachable from the book.
- **End to end:** the sample campaign plays from its first line to its last, headless, on the real table.

What's left is mostly presentation (layout, lighting, models), the Workshop, Steam, and the real campaigns.

If you're new to the project, start with `docs/`.

## Repository layout

- **`docs/`** is the design record. Start with `docs/README.md`, then `decisions_checklist.md` (what the game is)
  and `v1_build_checklist.md` (what gets built, and what "done" means).
- **`THIRD_PARTY.md`** lists the provenance and licence of every third-party asset and of the ruleset.
- **`LICENSE`**: proprietary, all rights reserved.
- **`assets/`** holds raw art and audio downloads. It's not committed because it's large; `THIRD_PARTY.md` says
  where to get everything again.

### The code

```
core/         Pure C#, no Godot references. The rules.
  Dice/       Polyhedral dice, the RNG, dice expressions like "2d6+3"
  Space/      Grid, cells, walls along cell edges, pathing, line of sight, the map file format
  Resolution/ d20 against a target: advantage, the DC ladder, the nat-1/nat-20 consequence pool
  Characters/ Ability scores, skills, conditions, damage types, armor, hit points, Actor
  Rules/      The public entry point: checks, saves, death saves, attacks
  Combat/     Turn loop, initiative, the 2/1/1 action economy, battlefield, reactions, zones, fleeing, enemy AI
  Magic/      Spell-effect primitives, spell slots and spell points, concentration, casting
  Tables/     Random-encounter and loot tables, and the GM's hidden rolls
  Localization/  Localization keys (core never produces display text)
  Statistics/ Chi-squared test for dice fairness

content/      Also pure C#. The game data, and everything built on top of the rules.
  srd/        The SRD 5.2.1 data, embedded in the assembly: spells, items, classes, species,
              backgrounds, monsters
  Schema/     JSON reading, the locale reader/writer, the Library
  Spells/ Items/ Classes/ Species/ Monsters/   Readers and catalogues
  Sheet/      The full character: Actor + class + pack + spells, Wild Shape, the sheet view
  Creation/   Character creation logic
  Inventory/  Forty slots, stacking, equipping, the merchant, looting into the pack
  Maps/       The map builder's model: paint, props, spawns, undo, save
  Campaigns/  Campaign packages: manifest (read and write), loader, the shelf, the demo edition
  Workshop/   Workshop logic: campaign drafts and their files, the wizard, undo, autosave,
              Recently deleted, plain-language problem reports
  Dialogue/   YarnSpinner conversations, barks, hints, and the commands a story sends to the table
  Encounters/ Loot/   Readers for a campaign's encounter and loot tables
  Saves/      Save games and heroes, stored as ids plus a history of what happened

game/         The Godot 4 / .NET project, and the only code that touches Godot. See game/README.md.
  Dice/       d4-d20 generated as geometry, and the rigid body that throws and settles them
  Tray/       The tray the dice land in, and its hook into the rules
  Board/      The map, its tiles, and the minis on it
  Table/      The table, the camera, and the glue between the dice and the rules
  Dev/        Probes, the demo table, screenshots and the flag registry (excluded from release builds)
  Campaigns/  Finds campaign folders on disk (built-in first, then My campaigns) and loads their locale
  Workshop/   The Workshop UI: the hub, the new-campaign wizard, a campaign's page and its problems panel
  audio/ Access/ Localization/ shaders/
tools/        Scripts: palette bake, model puller, impact slicer, duplicate finder, content importers
checks/       Check scripts, plus _common.ps1 which they share
core.tests/   xUnit
content.tests/ xUnit
sim/          Balance harness and dev tools
campaigns/    Campaign packages, kept loose on disk so they can be edited in the Workshop
              (sample_millbrook is the test campaign)
```

Two rules hold across the codebase:

- **`core/` and `content/` never reference Godot.** That's what makes headless tests and overnight balance runs
  possible, and it means a rule can change without opening the editor.
- **`core/` emits localization keys, never player-visible text.** English is just another locale file, and
  `check-locale.ps1` (plus a test) enforces that.

## Prerequisites

- .NET 8 SDK
- Godot 4.7.1, .NET build. The check scripts look for it under `C:\Godot`, or wherever `$env:GODOT_ROOT` points.
- PowerShell, for the check scripts
- Python 3, for the scripts in `tools/` and two of the checks
- Godot 4.7.1 .NET export templates, only if you want to export builds (Editor > Manage Export Templates)

## Build and test

From the repo root:

```bash
dotnet test
```

This runs both test suites: the rules, the data, the locale audit, and the first playable slice end to end.

The `sim` project plays the game headless and prints numbers:

```bash
dotnet run --project sim -- balance 2000            # plays fights and prints the win tables
dotnet run --project sim -- classes [runs] [class]  # every class at levels 1-5 against the sample campaign's fights
dotnet run --project sim -- trace <class> <level> <fight>   # one of those fights, line by line
dotnet run --project sim -- rattles [plays] [class] # how often the GM screen rattles in the sample campaign
dotnet run --project sim -- bouts                   # how many won arena bouts each level takes
dotnet run --project sim -- locale                  # which keys in game/locale/game.csv have no English
```

`game/locale/game.csv` is maintained by hand; `sim locale` only reads it.

### Check scripts

Each check builds first, then prints a one-line pass/fail verdict.

These don't need Godot:

```powershell
.\checks\check-locale.ps1     # every key the engine emits has English
.\checks\check-content.ps1    # every SRD file loads and matches the design docs
.\checks\check-fairness.ps1   # the dice are uniform
.\checks\check-slice.ps1      # character -> map -> goblin -> level up, plus the balance tables
.\checks\check-barks.ps1      # the game's barks are up to date with the vault (see below)
.\checks\check-duplicates.ps1 # no method is written twice (Python; reads the source, builds nothing)
.\checks\check-flags.ps1      # the flag list in this README matches the dev registry (Python)
```

These need Godot:

```powershell
.\checks\check-table.ps1      # the table comes up, the dice roll, core reads the result
.\checks\check-play.ps1       # the whole game headless: launch -> table -> sample campaign to the end
.\checks\check-dice.ps1       # the physics d20 is fair (slow, takes minutes; run it after touching the dice)
.\checks\check-controls.ps1   # every action on the Controls page, and rebinding
.\checks\check-creation.ps1   # character creation: layout, descriptions, cantrips vs. spells, scroll position
.\checks\check-layout.ps1     # every screen at six window sizes: nothing off screen or squeezed, no HUD overlaps
.\checks\check-workshop.ps1   # the Workshop end to end: wizard, a chapter and a map, save, play
.\checks\check-mapbuilder.ps1 # the map builder end to end: paint, walls, props, spawns, undo, save, load
.\checks\check-export.ps1     # exports the Windows Release preset, runs it headless, prints its size (-Dev for the dev preset)
```

### Exporting

Export presets live in `game/export_presets.cfg` and builds land in `build/` (gitignored). An exported game looks
for campaigns in a `campaigns` folder next to the exe, so a release build ships with none until you put one there.
`check-export.ps1 -Dev` copies the sample campaign in.

## Running the game

Open `game/` in the Godot editor and run it from there; it isn't launched from the CLI for normal play.

- **`launch.tscn`** is the main scene: the title, the campaign book and character creation. It hands off to
  **`table.tscn`**, where the campaign is actually played.
- `table.tscn` run on its own plays a demo dice throw.
- `boot.tscn` is a small smoke test that just prints what loaded.

Physics runs at **120 ticks per second** (set in `project.godot`). At Godot's default of 60, a d4 or d6 resting
on a face keeps rocking on its corners forever (see `game/Dice/DieBody.Wobble.cs`).

**After changing `game/locale/game.csv`, re-import it:**

```bash
godot --headless --path game --import
```

Godot reads the compiled `.translation` file next to the CSV, not the CSV itself. A new key will be missing at
runtime even though `check-locale.ps1` passes. If that's happened, `boot.tscn` prints `LOCALE STALE`.

## Importing content from the authoring vault

I write campaign material in a separate Obsidian vault that sits next to this repo (`../campaign_authoring/`).
Two scripts pull content from it into the game. Both only read the vault and never write to it, both accept
`--vault <path>` if the vault is somewhere else, and both accept `--check`, which writes nothing and fails if the
game is behind the vault.

### Companion barks

Barks live in the vault as one file per pool per companion (`Bark Pools/<Pool>/<companion>.md`). To import them:

```bash
python tools/import_barks.py
godot --headless --path game --import
```

The script writes `content/srd/barks/<companion>.json` and adds the lines to `game/locale/game.csv`, leaving every
other row alone. It reports each pool's size against the minimum set in the internal design notes
(`_design_docs/DIALOG_POOLS.md`, not committed), anything it skipped, and any pool folder the game doesn't know
about yet. It's safe to run repeatedly. `check-barks.ps1` runs it with `--check`.

### Arena challengers

Challenger lists live in the vault under `The Crucible/Challengers/*.md`. To import them into a campaign:

```bash
python tools/import_challengers.py campaigns/<campaign>
```

This writes `<campaign>/challengers/<file>.json` and adds names and descriptions to the campaign's own locale
(`challenger.<type>.name.001` and so on), leaving every other row alone. If it hits a `stats:` line it can't parse,
it stops, reports the file and line, and writes nothing. The challenger shapes, the roster and the bout XP rule are
in `docs/campaign_authoring_rules.md` §13.

## Command-line flags

The game accepts dev and test flags after `--`. A few examples:

```bash
godot --path game -- --begin sample_millbrook fighter --auto      # play the sample campaign to the end
godot --path game res://table.tscn -- --dice d20 --throws 500      # dice fairness sweep
godot --path game res://table.tscn -- --shot table.png             # save a screenshot and quit (not headless)
```

Add `--fixed-fps 60` before `--path` to run the fairness sweep as fast as possible.

All command-line parsing happens in one class, `game/DevArgs.cs`. What each flag does to a scene is defined in the
dev registry, `game/Dev/DevHooks.cs`, which is also where each flag is documented. The table below is generated
from that registry by `python tools/dev_flags.py`, and `check-flags.ps1` fails if it falls out of date, so edit
the registry rather than this file.

**Release builds.** `game/Dev/` isn't compiled into an `--export-release` build (see `game/Lanorim.csproj`);
`game/DevHooks.Release.cs` stands in for it and does nothing. A release build still honours the flags marked
"yes" below: the ones that pick the first screen (`--show`, `--open`, which is how `check-export.ps1` drives it)
and the ones the game's own classes read.

<!-- dev flags: written by `python tools/dev_flags.py` from game/Dev/DevHooks.cs - edit the registry, not this -->

*The launch screen: what comes up first*

| flag | where | in a release | what it does |
|---|---|---|---|
| `--show <screen>` | launch.tscn | yes | opens a launch screen: book, tutorials, builder, settings, credits (title otherwise); controls and create not in a release |
| `--open <campaign>` | launch.tscn | yes | with --show book, that campaign's two pages; with --show builder, that campaign's maps |
| `--book-overlay` | launch.tscn |  | the menu on panels over the book, as when the pages are too small |
| `--ask-delete` | launch.tscn |  | with --show book, the first character's Delete pressed, for a picture of the question |
| `--page <step>` | launch.tscn |  | with --show create, that creation page, part filled in; with --show book --open, that page of the campaign's characters (from 1) |
| `--class <class>` | wherever it applies |  | with --show create, that class (mage otherwise); on the demonstration table, the hero's class (rogue otherwise) |
| `--look <skin,hair>` | launch.tscn |  | with --show create, that skin tone and hair colour picked (FigureSkins, from 0) |
| `--longest` | launch.tscn |  | with --show create --page, the picks that make that page longest (equipment, traits, cantrips, spells; check-layout's worst case) |
| `--species <species|lineage>` | wherever it applies |  | with --show create, that species (and lineage) picked; on the demonstration table, the hero's (halfling otherwise) |
| `--begin <campaign> [class]` | launch.tscn |  | a character with every default (fighter otherwise), straight to the table; probe saves are kept apart |
| `--learn <spell,spell>` | launch.tscn |  | with --begin, those spells learned first |
| `--gold` | launch.tscn |  | with --begin, the gold instead of the gear, so the run opens in the starting shop |
| `--level <n>` | launch.tscn |  | with --begin, the character raised to that level before the table (a level 20 sheet) |
| `--build <campaign|mine> [map]` | launch.tscn |  | that map, or a new one called probe_map, in the map builder; mine is My maps |
| `--template <id>` | launch.tscn |  | with --build, a new map starts as that template (dungeon_room otherwise) |
| `--tool <tool>` | in the map builder |  | the builder opens with that tool in hand (select, paint, fill, wall, prop...) |
| `--picker <picker>` | in the map builder |  | the builder opens with that picker or pop-up open: buildings, npcs, props, walls, problems |
| `--trial-panel` | in the map builder |  | the builder opens its Play this map panel |
| `--prop-icons` | launch.tscn |  | renders every prop's palette thumbnail into game/ui/prop_icons/ and quits; not headless |
| `--mine <folder>` | launch.tscn |  | My campaigns and My maps kept in <folder>/campaigns and <folder>/maps, not the player's |
| `--wizard <page>` | launch.tscn |  | with --show workshop, the new-campaign wizard at that page (about, rules, first_chapter), filled in |
| `--tab <tab>` | launch.tscn |  | with --show workshop --open <campaign>, the campaign's page at that tab (about, chapters, characters, story, fights, encounters, loot, shops, lucky) |
| `--card <kind:id>` | launch.tscn |  | with --tab, that thing's card open and scrolled to (fight:goblin_camp, encounters:mill_road, loot:rat_nest, shop:millbrook_store, luck:id) |
| `--scroll <px>` | launch.tscn |  | with --card, scrolled that much further down |
| `--press <A,B>` | wherever it applies |  | buttons pressed by name, in order, as each comes up (at launch and on at the table): pictures of what they open |

*Play*

| flag | where | in a release | what it does |
|---|---|---|---|
| `--auto` | at the table, in a campaign | yes | plays itself to the end: first choices, the AutoPlayer in fights, dice thrown when asked (check-play) |
| `--autodice` | at the table, in a campaign | yes | the dice thrown as soon as they're asked for, the fights left to you |
| `--autostory` | at the table, in a campaign | yes | the story's lines and first choices taken as they come, past the starting shop |
| `--start <node>` | at the table, in a campaign | yes | the story starts at that node (cellar, road, camp in the sample) |
| `--pause` | at the table, in a campaign | yes | the pause menu opened on the hero's first turn in a fight |
| `--sheet <front|back|spell|feature>[:spell]` | at the table, in a campaign |  | on the hero's first turn in a fight, the character sheet at that side, or a spell's or a feature's card over it; :spell casts that spell at the nearest foe and holds it first |
| `--say-bark <pool>` | at the table, in a campaign | yes | a line from that bark pool every few seconds, past the chatter and the starting shop |
| `--gm-screen <skin>` | at the table, in a campaign | yes | another of the GM screens at the table |

*How things look, wherever they are*

| flag | where | in a release | what it does |
|---|---|---|---|
| `--gm-tallness <0..1>` | wherever it applies | yes | the GM screen at that height (1 is as authored) |
| `--tray-footprint <0.4..3>` | wherever it applies | yes | the dice tray's footprint, scaled (DiceTray.Sizing) |
| `--tray-walls <0.4..3>` | wherever it applies | yes | the dice tray's walls, scaled |
| `--old-hero` | wherever it applies | yes | the board's own hero figure, not the class's mini |
| `--plain-pose` | wherever it applies | yes | every hero mini in Idle_A |
| `--pose <clip>` | wherever it applies | yes | every hero mini held in that clip (Attack, Sword), for a picture mid-swing |
| `--pose-at <seconds>` | wherever it applies | yes | with --pose, so far into the clip |
| `--empty-handed` | wherever it applies | yes | hero minis holding nothing |
| `--figure <0|1>` | wherever it applies | yes | the class's second figure |
| `--head <model>` | wherever it applies | yes | any head on the hero's body, its headgear off (kaykit/Mage.glb) |

*The picture*

| flag | where | in a release | what it does |
|---|---|---|---|
| `--shot [file]` | wherever it applies |  | saves a picture of the screen (table.png otherwise) and quits; not headless |
| `--after <frames>` | wherever it applies |  | with --shot, wait so many frames first (150 otherwise) |
| `--size <WxH>` | wherever it applies |  | with --shot, the window made that size first |
| `--zoom <0..1>` | wherever it applies |  | with --shot, the camera at that zoom (1 all the way in) |
| `--quarter <n>` | wherever it applies |  | with --shot, the table turned so many quarters |
| `--focus <name>` | wherever it applies |  | with --shot, the camera following the first mini (or node) of that name |
| `--tray-up` | wherever it applies |  | with --shot, wait for the tray to come up and its dice to be read |
| `--frames <n>` | wherever it applies |  | with --shot, so many pictures, one a frame (tools/flicker.ps1) |

*The demonstration table*

| flag | where | in a release | what it does |
|---|---|---|---|
| `--lineup [ids]` | table.tscn on its own |  | a goblin, a bandit and a giant rat in a row beside the hero, as their minis; or the statblocks named, comma-separated |
| `--companion [look]` | table.tscn on its own |  | the class's companion by the GM screen, or any look by name |
| `--mood <mood>` | table.tscn on its own |  | with --companion, it feels that (pleased, alert, quiet, watching) |
| `--mood-after <seconds>` | table.tscn on its own |  | with --mood, so many seconds in (2 otherwise) |
| `--corners` | table.tscn on its own |  | a cross of walls on the map, so every kind of wall post is on the table |
| `--wall-styles` | table.tscn on its own |  | every wall style in a row of its own: walls, its door, archway and window (where it has them), and a corner, on grass (cc_task_m3, m4) |
| `--open-door` | table.tscn on its own |  | the map's door swung open |

*The probes and the checks*

| flag | where | in a release | what it does |
|---|---|---|---|
| `--controls` | launch.tscn |  | every act on the Controls page, and Change rebinds (check-controls) |
| `--creation` | launch.tscn |  | character creation, driven (check-creation) |
| `--map-probe` | launch.tscn |  | the map builder driven: a map from each template in My maps, ground, walls, a building, a scatter of trees, undo, save, reopen, copy to a campaign, and play it (check-mapbuilder; with --auto) |
| `--workshop-probe` | launch.tscn |  | the Workshop driven: a campaign made through the wizard, a chapter and a map added, saved, found in the book, and played to its one line (check-workshop; with --auto) |
| `--layout` | wherever it applies |  | every launch screen, or the table's HUD, pause menu, sheet and pack, at six window sizes (check-layout) |
| `--cast <spell[:mode]>` | at the table, in a campaign |  | in a fight, the hero casts that spell (with that mode) at the nearest foe or square, and the table is watched for its line, its sight, a Command's word obeyed (check-play) |
| `--prone-probe` | at the table, in a campaign |  | in a fight, Prone lies down in its square and Stand Up stands it up (check-play) |
| `--access-probe` | at the table, in a campaign |  | presses F2, F1, Tab, Shift+Tab and F3 at the table (check-controls) |
| `--locale` | table.tscn on its own |  | does Godot have English for every key the engine emits (check-table) |
| `--figures-probe` | table.tscn on its own |  | stands every class's two minis up and looks at them (check-table) |
| `--dice <die>` | table.tscn on its own |  | the fairness sweep: throws that die and tallies the faces (check-dice) |
| `--throws <n>` | table.tscn on its own |  | with --dice, so many throws (200 otherwise) |
| `--handful <n>` | table.tscn on its own |  | with --dice, so many at once, up to the tray's seats (8 otherwise) |
| `--tray-skin <skin>` | table.tscn on its own |  | with --dice, sweep another tray skin |
| `--alternate <die>` | table.tscn on its own |  | with --dice, a handful of that die between the counted throws |

<!-- end of dev flags -->

## Ruleset & legal

The rules are built on the **D&D System Reference Document 5.2.1**, (c) Wizards of the Coast LLC, used under
**CC BY 4.0**. This project is **not affiliated with, endorsed by, or sponsored by Wizards of the Coast**,
and uses none of its trademarks or Product Identity. It is a *tabletop-style* RPG, not a "D&D" product.
Full attribution and asset licences are in `THIRD_PARTY.md`.

The original code and content in this repository are **proprietary, all rights reserved** (`LICENSE`).
Third-party components keep their own licences.
