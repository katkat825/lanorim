# Lanorim

A single-player, GM-run **tabletop-style RPG**. You play one hero — no party to manage, no companion AI to
babysit — led through a branching campaign by a referee behind the screen, on a real tabletop presentation
(grid map, dice, GM screen). Campaigns are moddable and shareable.

*Internal / working title: **Maps & Math Rocks**. The player-facing name is always **Lanorim**.*

**Status:** pre-v1 — the design is settled. The rules engine (combat with reactions and line/cone areas,
123 SRD 5.2.1 spells with a choice of slots or points, Wild Shape), the SRD data checked against the SRD
text, the map model, saves, and the narrative layer (branching dialogue, hidden GM rolls, encounter and
loot tables) are in and unit-tested. Every screen is wired in Godot: the title and campaign book, character
creation, the table with its map, minis, GM screen and dice tray, the dialogue card, the combat HUD, the
sheet, pack and merchant, level-up, pause, death and the end. A sample test campaign plays from its first
line to its last, headless, on the real table. The map builder is at the table too (the book's
"Map builder"), and the book has the credits. What's left is mostly how it looks (layout, lighting, models), the
Workshop, Steam, and the real campaigns. Start in `docs/`.

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
  Campaigns/  the campaign package: manifest (read and written), loader, the shelf, the demo edition
  Workshop/   the Workshop as logic: a campaign's draft and its files, the wizard, undo, autosave, Recently deleted,
              problems in plain words
  Dialogue/   YarnSpinner conversations, barks, hints, and the commands a story sends the table
  Encounters/ Loot/   readers for a campaign's encounter and loot tables
  Saves/      save games and heroes, as ids and what happened

game/         the Godot 4 / .NET project. the only thing that touches Godot. see game/README.md
  Dice/       d4-d20 generated as geometry; the rigid body that throws and settles them
  Tray/       the vessel they land in, and the seam to the rules
  Board/      the map, its tiles, and the minis standing on it
  Table/      the table, the camera, and the ten lines that join dice to rules
  Dev/        the probes, the demonstration table, the picture and the flag registry - not in a release build
  Campaigns/  finding campaign folders on disk (the game's own, then My campaigns) and loading their locale
  Workshop/   the book's Workshop: the hub, the new-campaign wizard, a campaign's page and its problems panel
  audio/ Access/ Localization/ shaders/
tools/        the palette bake, the model puller, the impact slicer, the duplicate finder, the bark importer
checks/       the check scripts, and _common.ps1, what they share
core.tests/   xUnit
content.tests/ xUnit
sim/          the balance harness and the dev tools
campaigns/    campaign packages, loose on disk so a Workshop author can edit them (sample_millbrook is the test one)
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
.\checks\check-locale.ps1     # does every key the engine emits have English
.\checks\check-content.ps1    # does every SRD file load, and match the design docs
.\checks\check-fairness.ps1   # are the dice uniform
.\checks\check-slice.ps1      # character -> your map -> goblin -> level up, plus the balance tables
.\checks\check-duplicates.ps1 # is any method written twice (Python; reads the source, builds nothing)
.\checks\check-flags.ps1      # does the README list every flag, and the registry every flag the game reads (Python)
```

`game/locale/game.csv` is written by hand. `sim locale` only reads it and says which keys have no English.

### The companions' barks

Kathleen writes the barks in her vault, one file per pool per companion
(`../campaign_authoring/Bark Pools/<Pool>/<companion>.md`). To bring them into the game:

```bash
python tools/import_barks.py
```

then re-import the locale (`godot --headless --path game --import`) and play. It reads the vault and never writes to it;
it writes `content/srd/barks/<companion>.json` and the lines into `game/locale/game.csv` (every other row untouched),
and prints each pool's size against the minimum in `_design_docs/DIALOG_POOLS.md`, anything it skipped, and a warning
for a pool folder the game doesn't know yet. Run it as often as you like; `.\checks\check-barks.ps1` runs it with
`--check` (writes nothing, fails if the game is behind the vault).

`dotnet run --project sim -- rattles [plays] [class]` counts how often the GM screen rattles in the sample campaign.

These need Godot, and find it themselves under `C:\Godot` (or `$env:GODOT_ROOT`):

```powershell
.\checks\check-table.ps1     # does the table come up, do the dice roll, does core read the felt
.\checks\check-play.ps1      # the whole game headless: launch -> table -> the sample campaign to its end, dice on the tray
.\checks\check-dice.ps1      # is the PHYSICS d20 fair - slow, minutes, run it after touching the dice
.\checks\check-controls.ps1  # every act on the Controls page, and Change rebinds
.\checks\check-creation.ps1  # creation: no hover text, a description under each list, cantrips and spells apart, the scroll kept
.\checks\check-layout.ps1    # every screen at six window sizes: nothing off screen or squeezed, no HUD overlaps
.\checks\check-workshop.ps1   # the Workshop, driven: a campaign through the wizard, a chapter and a map, saved, in the book, played to its line
.\checks\check-mapbuilder.ps1 # the map builder, driven: paint, walls, a prop, start, spawn, undo, save; the file loads in its campaign
.\checks\check-export.ps1    # export the Windows Release preset, run it headless to the book, print its size (-Dev: the dev preset)
```

`check-export.ps1` needs Godot's 4.7.1 .NET export templates (Editor > Manage Export Templates in the Godot editor).
The presets are `game/export_presets.cfg`; builds land in `build/` (ignored). An exported game finds campaigns in a
`campaigns` folder beside its exe, so a release build has none until one is put there; `-Dev` copies the sample.

`dotnet run --project sim -- classes [runs] [class]` plays every class at levels 1-5 against the sample
campaign's fights; `sim trace <class> <level> <fight>` shows one of them line by line.

## Godot

The Godot project is opened and run from the editor, not the CLI. The main scene is **`launch.tscn`** —
the title, the campaign book, character creation - which goes to **`table.tscn`**, the table with the map
and the dice tray on it, where the campaign is played. `table.tscn` opened on its own still runs its
demonstration throw. `boot.tscn` is the smaller smoke test that only prints what loaded.

Flags after `--` (for looking and checking): `-- --begin sample_millbrook fighter --auto` plays the sample campaign to
its end, `res://table.tscn -- --dice d20 --throws N` is the fairness sweep (put `--fixed-fps 60` before `--path` to run
it as fast as it can), `--shot <file>` saves a picture and quits. One class reads the command line, `game/DevArgs.cs`;
what each flag adds to a scene is the dev registry's, `game/Dev/DevHooks.cs`, which is also where every flag is written
down. The list below is made from it (`python tools/dev_flags.py`; `checks/check-flags.ps1` fails when they differ).

**Not in a release build.** `game/Dev/` (the probes, the demonstration table, the line-up, the picture and the
registry) isn't compiled for an `--export-release` (`game/Lanorim.csproj`); `game/DevHooks.Release.cs` answers the
scenes with nothing instead. A release still reads the flags marked "yes": the first screen (`--show`, `--open`), which
is how `check-export.ps1` runs it, and the ones the game's own classes read.

<!-- dev flags: written by `python tools/dev_flags.py` from game/Dev/DevHooks.cs - edit the registry, not this -->

*The launch screen: what comes up first*

| flag | where | in a release | what it does |
|---|---|---|---|
| `--show <screen>` | launch.tscn | yes | opens a launch screen: book, tutorials, builder, settings, credits (title otherwise); controls and create not in a release |
| `--open <campaign>` | launch.tscn | yes | with --show book, that campaign's two pages; with --show builder, that campaign's maps |
| `--book-overlay` | launch.tscn |  | the menu on panels over the book, as when the pages are too small |
| `--ask-delete` | launch.tscn |  | with --show book, the first character's Delete pressed, for a picture of the question |
| `--page <step>` | launch.tscn |  | with --show create, that creation page, part filled in |
| `--class <class>` | wherever it applies |  | with --show create, that class (mage otherwise); on the demonstration table, the hero's class (rogue otherwise) |
| `--longest` | launch.tscn |  | with --show create --page, the picks that make that page longest (equipment, traits, cantrips, spells; check-layout's worst case) |
| `--species <species|lineage>` | wherever it applies |  | with --show create, that species (and lineage) picked; on the demonstration table, the hero's (halfling otherwise) |
| `--begin <campaign> [class]` | launch.tscn |  | a character with every default (fighter otherwise), straight to the table; probe saves are kept apart |
| `--learn <spell,spell>` | launch.tscn |  | with --begin, those spells learned first |
| `--gold` | launch.tscn |  | with --begin, the gold instead of the gear, so the run opens in the starting shop |
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
| `--lineup` | table.tscn on its own |  | a goblin, a bandit and a giant rat in a row beside the hero, as their minis |
| `--companion [look]` | table.tscn on its own |  | the class's companion by the GM screen, or any look by name |
| `--mood <mood>` | table.tscn on its own |  | with --companion, it feels that (pleased, alert, quiet, watching) |
| `--mood-after <seconds>` | table.tscn on its own |  | with --mood, so many seconds in (2 otherwise) |
| `--corners` | table.tscn on its own |  | a cross of walls on the map, so every kind of wall post is on the table |
| `--wall-styles` | table.tscn on its own |  | every wall style in a row of its own, two walls, its door (where it has one), two more and a corner, on grass (cc_task_m3) |
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

The physics runs at **120 ticks a second** (`project.godot`): at Godot's default 60 a d4 or d6 lying on a face rocks
on its corners for good (`game/Dice/DieBody.Wobble.cs`).

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
