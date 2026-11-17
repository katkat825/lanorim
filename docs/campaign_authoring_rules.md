# Campaign authoring rules

*Started 2026-10-08 (`cc_task_h` 3.5). Short on purpose; it will grow. A campaign is written for **one hero**, not a
party: these rules are what that changes. The campaign check (`Package.Read`, run by the game when it loads a
campaign and by `content.tests`) **warns** where it can see a rule broken. It never refuses: the call is yours.*

## 1. The solo budget, and the enemy cap

Give each fight the level it's meant for and how hard it's meant to be:

```json
{ "id": "goblin_camp", "kind": "fight", "level": 3, "difficulty": "moderate",
  "monsters": [ { "monster": "goblin_boss" } ] }
```

- `difficulty` is the SRD's Combat Encounter Difficulty: `low`, `moderate` (when unsaid) or `high`.
- The check holds the fight's monsters to SRD 5.2.1's **XP Budget per Character for one character** (level 1: Low
  50, Moderate 75, High 100), with each band counted at the most its dice can come to.
- **The enemy cap:** no more than **2** enemies at levels 1-2, **3** at 3-4, **4** at 5-10, **5** at 11 and up. Action
  economy is what kills a solo hero; a fight within budget but over the cap still warns.
- A fight with no `level` gets a note saying it can't be checked.

## 2. Two keys

A solo hero has one set of skills. **A check the story can't get past on a fail is a door the wrong class can never
open.** Give every check that gates the story a second key:

- another skill on a **different ability** (Athletics *or* Sleight of Hand), or
- a way through **without a check** (a choice: `-> Look for another way`).

The check warns where it can tell: a node whose `<<if $passed>>` block is all it has left, where the pass jumps on and
the fail doesn't, or where the fail `<<stop>>`s. A dead end built another way (a fail that jumps to an ending, a
variable read three nodes later) it can't see, so read your fail branches yourself.

## 3. Failing moves the story forward

A failed check is never "nothing happens, try again". Something changes: the door won't budge, so you go down the
coal chute and land badly; the guard spots you, so the fight starts on their terms. The sample campaign's cellar is the
model (`campaigns/sample_millbrook/dialogue/story.yarn`, node `cellar`). A natural 1 or 20 also draws from the
consequence pool on its own; you don't write that.

## 4. Morale, per fight

A fight may say when its enemies break (`"morale"`), and mark its leader:

```json
{ "id": "bandit_camp", "kind": "fight", "morale": "leader",
  "monsters": [ { "monster": "bandit", "count": "2" }, { "monster": "bandit_captain", "leader": true } ] }
```

- `none` (when unsaid): they fight to the end.
- `leader`: the rest break when the leader falls - the one marked `"leader": true`, or the first monster if none is.
- `half`: they break when half of them, rounded up, are down.

Broken, they run for the nearest way off the map (a Disengage if the hero is next to them, else a Dash) and step off
it. **A map with no way off, and they surrender.** Either way the fight is won and its loot is the loot. It's yours to
decide fight by fight: beasts and bandits break, a cornered cultist or an undead thing doesn't.

## 5. What the story can read

The table writes these Yarn variables when it answers a command (`RequestVariables`, `content/Dialogue/Requests.cs`):

| Variable | Set by | Holds |
|---|---|---|
| `$passed` | `<<check>>`, `<<save>>` | the roll made its DC |
| `$total` | `<<check>>`, `<<save>>` | the total rolled |
| `$natural` | `<<check>>`, `<<save>>` | the d20's face |
| `$consequence` | `<<check>>`, `<<save>>` | a natural 1 or 20: the consequence pool drew |
| `$roll` | `<<roll>>` | the total |
| `$encounter` | `<<encounter>>` | the entry the table picked, `""` for nothing |
| `$encounter_fight` | `<<encounter>>` | that entry is a fight |
| `$fight` | `<<fight>>` | `"won"`, `"lost"`, `"fled"` or `"yielded"` (`"lost"` and `"yielded"` only where defeat continues, §8) |
| `$fight_broke` | `<<fight>>` | won because the enemies' morale broke (a rout or a surrender), not because the last fell |
| `$loot_gold` | `<<loot>>` | what it found, in whole gold, rounded down (99 copper is 0) |
| `$loot_copper` | `<<loot>>` | everything it found, gold, silver and copper together, in copper: "a few coppers" is `$loot_copper > 0 and $loot_gold == 0` |

And these about the hero, read-only and always current (`HeroVariables`, `content/Dialogue/HeroVariables.cs`; cc_task_i
4.1). Setting one does nothing, and the campaign check says so:

| Variable | Holds |
|---|---|
| `$hero_class` | the class id: `"fighter"`, `"mage"` |
| `$hero_level` | the hero's level |
| `$hero_casts` | true if the hero has any spells |
| `$hero_figure` | which of the class's two minis the player picked: 1 or 2 |
| `$hero_name` | the name the player gave |

And these about the arena (`ArenaVariables`, `content/Dialogue/ArenaVariables.cs`; cc_task_cg), read-only in the same
way. Section 13 says how the arena works:

| Variable | Holds |
|---|---|
| `$reputation` | the hero's reputation, 0 and up |
| `$challenger` | who `<<challengers>>` picked, what `<<fight {$challenger}>>` fights; `""` when the player went back to the story |
| `$challenger_name` | the picked challenger's name: `{$challenger_name}` in a line shows the name itself ("Grik the Knife") |
| `$challenger_special` | true when the pick is one of your hand-written special fights |

And one function: **`has_item("rope")`** is true when the item is in the pack or worn.

```
<<if $hero_casts>>
    The runes on the door hum at you. #line:runes_hum
<<elseif has_item("crowbar")>>
    You could lever it. #line:lever_it
<<endif>>
```

## 6. Commands added by task I

| Command | What it does |
|---|---|
| `<<roll 1d20>>` | the GM rolls behind the screen, **silently** (it used to rattle the screen; nearly every roll did) |
| `<<roll 1d20 heard>>` | the same, and the player hears it: the GM screen rattles, a caption says so, and the companion may react. Use it when the roll should feel like a secret |
| `<<bark pool>>` | the companion says a line from one of its bark pools: `<<bark nearby>>`, `<<bark danger>>` (§7) |
| `<<armory id>>` | a loadout screen, not a shop: a merchant with `"armory": true`, §8 |
| `<<xp 50>>` | experience points, in a campaign that levels by XP; ignored with a warning in a milestone one |
| `<<level 3>>` | as before, milestone levelling; ignored with a warning in a campaign that levels by XP |
| `<<challengers>>` | the arena's roster (cc_task_cg, §13): the player picks a challenger or goes back to the story. `$challenger` says who |
| `<<map id>>` | the scene's backdrop becomes that map of the campaign's, with its look and props (2026-11-16, `cc_task_tf`). Nothing to answer; the story goes straight on. The same map again changes nothing. A map the campaign doesn't have is a campaign check problem naming the file, node and line |
| `<<tip section>>` | opens the F1 help card at that section, as if the player had pressed F1 and gone there (2026-11-16, `cc_task_nt`). The section is one of the card's own: `story`, `dice`, `turn`, `move`, `attack`, `more`, `keys`. Nothing on the screen is highlighted. The story waits until the card is closed (F1 or Esc), then goes on. Any other section is a campaign check error naming the line |

**A story's `<<bark>>` always plays** (2026-11-16, `cc_task_nt`): whatever the chatter cooldown or the last line, and
it still starts the cooldown, so an ordinary bark straight after it is dropped. Only the Off setting silences it, and
"Some" never thins it. **Every engine command is now checked as the story is read:** one written wrong (`<<bark
nowhere>>`, `<<tip sheet>>`) is an error that names its file, node and line. A command with a `{$variable}` in it is
left to the playthrough.

**The GM screen rattles rarely now** (cc_task_i Part 1): for an encounter table (`<<encounter>>`, or a `<<fight>>` that
names a table) and a `<<roll ... heard>>`, and never more than once every five minutes of play. An enemy's attacks,
damage and saves are in the fight's log, so they make no sound behind the screen. An encounter table that shouldn't be
heard says so: `"rolled": "quiet"` (beside `"hidden"`, the default, and `"shown"`).

## 7. The companion's barks

Each companion says short lines from bark pools: game-wide ones from Kathleen's vault (`tools/import_barks.py`), and a
campaign can add lines on top in its `barks/` folder - the same file shape as `content/srd/barks/`, one file per
companion, its lines keyed `dialogue.<companion>.bark.<pool>.<campaign>.001` in the campaign's locale.

| Pool | When it plays |
|---|---|
| `snag` | a natural 1 on the hero's attack, check or save |
| `maxed` | a natural 20 on the hero's attack, check or save |
| `danger` | `<<bark danger>>`; and at a fight's start, one time in three, unless the story barked in the last ten seconds. Never when the hero is Bloodied: its lines warn of something coming |
| `victory` | a fight is won |
| `knock_out` | a bout lost by a knockout: the hero fails the death save in a fight whose defeat continues (2026-11-16). It always plays, like a story's bark. A yield isn't a knockout. The fight's log says the hero "is knocked out" |
| `secret` | the GM screen rattles (§6) |
| `camp` | `<<rest short>>` or `<<rest long>>` |
| `nearby` | only `<<bark nearby>>` |

One line at a time, then twenty seconds of quiet; a higher pool in that list cuts in on a lower one, and a lower one is
dropped, not saved for later. The player's "Companion chatter" setting can turn them down (Some halves Victory, Snag
and Maxed) or off. For the moments with no pool in v1 (the hero hurt, a level up, a quest offered), write the line
into the story as `#speaker:companion`.

## 8. How the campaign plays (pack.json)

Each of these is optional; left out, the campaign plays as the game always has.

| Field | Values | What it does |
|---|---|---|
| `defeat` | `"death"` (default), `"continues"` | continues: a lost fight goes back to the story with `$fight` `"lost"`, the hero brought round on 1 hit point. The keepsake still catches the first drop; the next drop makes the single death save, and **failing it is a knockout, not death**. A fight entry can say `"defeat"` for itself |
| `leveling` | `"milestone"` (default), `"xp"` | xp: a won fight gives its monsters' XP (the SRD's XP by challenge rating, all of it to the solo hero), `<<xp n>>` gives more, and the hero levels at the SRD's Character Advancement thresholds (300, 900, 2,700...). XP shows on the sheet. A lost, yielded or fled fight gives none |
| `characters` | 1 to 20 (default 5) | how many characters the campaign holds |
| `starting_shop` | `true` (default), `false` | false: a new hero goes straight into the story, wearing the best of what it has |
| `challengers` | an object (§13) | the arena's numbers: reputation for a win and a loss, the roster's size, a won bout's XP |
| `tags` | a list of words | `"test"` (at the back of the book, labelled), `"tutorial_beginner"` and the other two (the tutorial picker), `"arena"` (its own line in the contents, §13), `"mature"` (§11) |
| `consequences` | `"add"` (default), `"replace"` | the campaign's own `consequences/*.json` (the shape of `content/srd/consequences/pool.json`) are added to the game's nat-1 / nat-20 pool while it runs, or replace it. An entry can be flavour only: `"kind": "flavour"`, a line and no effect. Added ones can't reuse a game id; a replacing pool needs a boon and a bane |

**Yield:** in a fight where defeat continues and the map has no way off, "Yield" is in More actions. It ends the bout
at once, no knockout: `$fight` is `"yielded"`.

**An armory** is a merchant with `"armory": true` and a stock of things to wear (no consumables: the check refuses
them). Its stock can be the word **`"stock": "all_mundane"`** instead of a list (2026-11-16): every non-magical item in
the library that is worn or held, so an arena's quartermaster needn't list them. Whichever way it's written, the racks
show each hero only what isn't magical and what their class and level can use (an item's `classes` and
`minimum_level`). Magical means a boon while worn, a spell (a wand) or a bonus to hit. `<<armory id>>` opens it: the hero puts on anything in it for free, as often as they like; what it replaces goes
back to the racks (if it was lent) or into the pack (if it was theirs). No gold moves and nothing borrowed goes in the
pack; a loan taken off goes back, and can't be sold.

## 9. Money: gold, silver and copper

*cc_task_k, 2026-10-12.* **The purse is copper.** The SRD's coins are 1 GP = 10 SP = 100 CP, and the game holds every
amount in copper and shows it as gold, silver and copper. Write money in whichever coin reads right; they all land in
the same purse.

| Command | What it does |
|---|---|
| `<<gold 50>>` | gives 50 gold; `<<gold -50>>` takes it |
| `<<silver 12>>` | gives 12 silver; `<<silver -25>>` takes it |
| `<<copper 5>>` | gives 5 copper; `<<copper -5>>` takes it |

Taking more than the hero has takes what there is: a toll doesn't go into debt.

A loot table's find can hold `"gold"`, `"silver"` and `"copper"`, any or all of them, each written the same way, as
dice or as dice times a number:

```json
{ "id": "mara_reward", "kind": "find", "silver": "2d6" },
{ "id": "fat_purse", "kind": "find", "gold": "1d4", "silver": { "roll": "3d6", "times": 10 }, "copper": "5" }
```

A find needs `items` or at least one coin. `<<loot>>` answers `$loot_gold` and `$loot_copper` (§5).

Still in gold only: an item's cost in an item file (fractions work: `0.5` is 5 silver), a class's or background's
starting gold, and a consequence's `"kind": "gold"`.

## 10. Maps

*cc_task_l, 2026-10-12.* A map is `maps/<id>.map` in the campaign, made in the map builder (the book's **Map
builder**) and readable in a text editor. A map that belongs to no campaign lives in **My maps** (`user://maps`; on
Windows `%APPDATA%\Godot\app_userdata\Lanorim\maps`), and the builder's **Copy to campaign...** puts it in one.

**Which map a story scene shows** (2026-11-16): the last `<<map>>` or the last fight's map, whichever came later; with neither, the chapter's first map.

**Format 2** adds how the map looks. Format 1 still reads, as stone floor and stone walls, so older maps load unchanged.

| Key | What it is |
|---|---|
| `name` | the map's name as its author typed it ("Mill Yard - North"): letters, numbers, spaces, dashes and underscores. The file's name is its id, made from the name (lowercase, a space or a dash an underscore) and free where it lives. Left out, the map is called by its id (cc_task_m2, 2026-10-22) |
| `map` | the squares and the lines between them, one glyph each: `.` floor, `~` difficult, `#` rock, `_` deep water or a chasm (seen across, never crossed), `@` the start, `1`-`9` spawns; `\|` `-` a wall, `x` a door, `a` an archway, `w` a window (2026-11-03) |
| `ground` | one letter a square, a row of letters a row of squares: `s` stone, `p` wood planks, `g` grass, `d` dirt, `c` cobblestone, `a` sand, `n` snow, `w` shallow water. Left out when the whole map is stone |
| `walls` | the `map`'s own shape, a letter on each wall or door that isn't the dungeon's stone: `t` timber, `p` plaster and beam, `f` fieldstone, `e` wooden fence, `h` hedge. Left out when every wall is stone |
| `rough_markers` | `[[x, y], ...]`: a start or spawn standing on difficult ground (the `map` writes `@` there, which can't say so) |
| `props` | `{ "id", "x", "y", "turn" }`: a prop from the builder's palette, its top-left square and its quarter turns |

**A ground is a look; the square's glyph is its rule.** Painting a ground in the builder brings its rule with it
(shallow water and snow are difficult, the rest floor), and difficult, rock or a chasm can be painted over any ground.
Deep water is shallow water's ground on a chasm square.

**A fence or a hedge** stops a walker and not an eye: the rules see bars there. A door in it is a gate, and a shut gate
blocks the view as any door does.

**An archway** is an open way that never shuts: walked through and seen through. **A window** is seen through and
never walked through (bars, as a fence is). Each comes only in the styles that have one: an archway in timber, plaster,
fieldstone and the ruins (whole, broken, overgrown); a window in plaster, fieldstone and the ruins (cc_task_m4, 2026-11-03).

**A prop can cover more than one square** (a cottage is 2 x 2, a barn 4 x 4); its size is the palette's, so the map file
only says where its top-left square is and how it's turned. A prop that blocks can't share a square with another that
blocks, or cover the start or a spawn, and a campaign won't load a map where one runs off the edge.

## 11. Campaigns made in the Workshop

*Added by cc_task_m (2026-10-12).* The Workshop writes the same files as a hand-made campaign; nothing here is a second
format, and a hand-made campaign opens in the Workshop too.

- **Where they live.** My campaigns are in `user://campaigns/<id>/` (on Windows
  `%APPDATA%\Godot\app_userdata\Lanorim\campaigns`), a campaign root read after the game's own `campaigns/`. A campaign
  whose id the game's own already has isn't loaded, and its Workshop card says so with a Rename button.
- **The id** is made from the name (lowercase, a run of anything else an underscore, a number added when it's taken).
  It names the folder and every key, so a rename changes the folder, `pack.json`'s `id` and the locale's
  `campaign.<id>.*`, `quest.<id>.*` and story-line keys together. Saves made under the old id don't follow it.
- **Mature content** is the tag `"mature"` in `tags` (the wizard's tick box). Steam's mature flag (task R) reads it.
- **The cover** is `preview.png` (640 x 360), named by `pack.json`'s `"preview"`. With none, the GM screen's picture is
  used.
- **`workshop.json`** holds what only the Workshop reads: each character's note to self and portrait
  (`assets/portraits/<id>.png`). The game never loads it; a campaign plays the same without it.
  `{ "characters": [ { "id": "oda", "note": "...", "portrait": "assets/portraits/oda.png" } ] }`
- **Characters** are the locale's `actor.<id>.name` rows; their ids are the story's speakers.
- **Dot-folders are the Workshop's.** `.draft/` in a campaign is its autosave and `user://campaigns/.deleted/` is
  Recently deleted (kept 30 days). The loader skips any folder whose name starts with a dot.
- **A chapter with no map yet** doesn't load (the check refuses it), but the Workshop still opens it with everything on
  its forms, so it can be finished there.

*Added by cc_task_n to cc_task_q (2026-10-27).*

- **The tables** (fights, random encounters, loot, shops, lucky rolls) are edited in their own files. A file is changed
  only where it was changed: an entry nobody touched keeps its own text and layout, a key the editor doesn't know is kept,
  and a file nobody touched is never written. A fight made in the Workshop goes into `encounters/fights.json`, table
  `fights`; new random tables into `encounters/random.json`, loot into `loot/loot.json`, shops into
  `merchants/merchants.json`, lucky rolls into `consequences/consequences.json`. A table's or fight's name, when it's more
  than its id, is in `workshop.json`'s `names`.
- **The story**: a card is a node. A node whose lines the editor writes back exactly opens as cards and blocks; any other
  opens as a Text card and is kept byte for byte. Editor-made cards go into `dialogue/story.yarn`, and every card is saved
  back into the file it came from, in its place. Each line gets a `#line:` id from its card's name and a number, once.
- **A line with an expression in it** (`{$hero_name}`) has `{0}`, `{1}`... in its locale row, in order.
- **What the story remembers** (`<<set $x to true>>`) must be declared before it's read; the Workshop writes
  `dialogue/workshop_memory.yarn` (a node of `<<declare>>`s it owns, rewritten on every save). A hand-written campaign
  declares its own. The phrases the author typed for them are in `workshop.json`'s `story.memory`.
- **The board** (where each card sits, its colour and name, and the view) is `workshop.json`'s `story`, never the Yarn.
- **A test** plays the campaign as the page has it from `user://workshop_test/<id>/`, its saves beside it; nothing it does
  reaches the campaign's folder or the player's saves. The test hero and what it remembers are kept per campaign in
  `.draft/test.json`.

## 12. Lines at a moment in a fight

*Added 2026-11-04 for the tutorials.* `<<fight>>` stops the story until the fight is over, so a story can't talk during
one. A node with a `cue:` header is **said at a moment in a fight** instead of being jumped to: its lines, in order, in
the companion's bubble and the fight's log, each held long enough to read. Its commands, options and branches are not
read, only its lines.

```
title: rat_first_turn
speaker: companion
cue: granary_rat hero_turn 1
---
The lit squares are where you can walk to. #line:cue_rat_lit_squares
===
```

| Moment | When |
|---|---|
| `start` | the fight's first moment, once initiative is rolled (no number) |
| `hero_turn N` | the hero's Nth turn |
| `foe_turn N` | the Nth turn any foe takes, whoever's it is |
| `hero_attack N` | the Nth time the hero's attack or spell is settled, hit or miss |

The first word is the fight's id (`<<fight granary_rat>>`, or the entry an `<<encounter>>` picked). One node a moment.
The tutorials (`campaigns/tutorial_*/dialogue/fight_cues.yarn`) are the model.


## 13. The arena: generated challengers

*Added 2026-11-04 (`cc_task_cg`).* A campaign like the Crucible offers a **roster** of challengers to fight: drawn from
your lists, fitted to the hero's level, and growing with the hero's reputation. Waves (several groups with no break)
aren't built yet.

**Humanoids only** (2026-11-16, `cc_task_ca`). Kathleen: "v1 won't have non-humanoid challengers", and "goblin is close
enough to humanoid. it just needs to be a thinking, intelligent enough being that can voluntarily decide to try to
prove it's the best warrior in the universe." So every stat block a challenger is made of carries one of the tags
**`humanoid` or `goblinoid`**. The SRD types the Goblin Warrior as Fey (Goblinoid), and the stat blocks stay faithful,
so the rule is the tags, not a retag; a later stat block that passes her test joins by adding one word to
`content/Challengers/ArenaFolk.cs`, which the importer reads too. The goblins can fight; the ogre, the beasts and the
undead can't. The importer refuses a `stats:` line that names one ("ogre isn't a humanoid: v1's challengers are
humanoids only", with the file and the line), the campaign check refuses a type or a special fight with one, and the
shared lists draw only humanoids.

**Its own line in the book** (2026-11-16): tag the campaign **`arena`** in pack.json's `tags`, and it gets a line of its
own in the book's contents, beside Tutorials, under the campaign's own name. It isn't in the ordinary campaign list.
With more than one arena campaign, the line says "Arenas" and opens a short picker.

### Your lists, and the importer

Write the lists in the vault, `campaign_authoring/The Crucible/Challengers/*.md`, one file per list. Each challenger
type is a `# heading`, then a `stats:` line, then numbered `## descriptions` and numbered `## names`. **Any name goes
with any description**, so keep both free of anything that has to match the other (pronouns, a species).

```
# goblin warrior
stats: a single goblin warrior

## descriptions
1. A wiry goblin with a chipped sword and an oversized helmet.

## names
1. Grik the Knife
```

Then bring them into the campaign:

```
python tools/import_challengers.py campaigns/<your_campaign>
python tools/import_challengers.py campaigns/<your_campaign> --check     # is the campaign behind the vault?
```

It writes `challengers/<file>.json` (`normal_fights.json` for `Normal Fights.md`) and puts every name and description
in the campaign's locale as `challenger.<type>.name.001` and `challenger.<type>.description.001`. The type's id is its
heading in snake case (`goblin_warrior`). It never changes the vault. Run it again whenever you change a list: it
rewrites those rows and leaves every other row alone. The Workshop's table editor doesn't edit challengers; the vault is
where they live.

### The `stats:` line

| Shape | Written | What the arena does |
|---|---|---|
| a single | `a single goblin warrior`, `one guard`, `a guard`, `mage` | one monster. In the fight it goes by the challenger's name ("Grik the Knife") in the turn banner and the log |
| a single, one of several | `a single thug or scout`, `gladiator or berserker` | one monster, whichever of these fits the hero's level each time one is drawn (any of them that fits, at random; the closest when none does) |
| a fixed group | `two guards`, `2 guards`, `two guards and a goblin warrior` | exactly those. The monsters keep their own statblock names in the fight |
| a team | `team of goblin warrior, goblin archer` | drawn from these when the roster is drawn, to a budget for the hero's level (below). Its `## names` are the team's names; the members keep their statblock names |

Monsters are named by their statblock name in English, as the game shows it (`monster.<id>.name`: "Goblin Warrior",
"Guard", "Goblin Archer"), or by their id in words, singular or with an -s or -es. A count left out is one. A name that
matches nothing, or more than one monster, is refused with the file and the line. The importer lists every line it
refuses, and then writes nothing.

### Names and descriptions for any stat block

A `# heading` with no `stats:` line is a **shared list**: names and descriptions that go with any stat block. Its
`## Single Challengers` and `## Teams` sections each have a `### Names` and a `### Descriptions`:

```
# Generic
these can be used for any stat block

## Single Challengers
### Names
1. Ash the Undying
### Descriptions
1. A confident challenger steps forward, eager to begin.

## Teams
### Names
1. The Rusty Blades
### Descriptions
1. A small group of challengers enter the arena.
```

*Changed 2026-11-09 (Kathleen).* They are used in two ways:

- **A type with no list of its own takes the shared one.** A type uses its own names and descriptions when it has them;
  only a type with no `## names` (or no `## descriptions`) takes the shared list for its shape: the singles' for a
  single, the teams' for a group or a team. The two are never mixed.
- **Every stat block no type claims is offered under the shared lists.** A type claims the stat blocks in its `stats:`
  line: a goblin is only ever offered as your goblin type. Every other stat block in the game (the SRD's and the
  campaign's own) can step into the arena with a shared name: as a single, whichever of them fits the hero's level, or
  as a team of one of them, drawn to a budget. So a plain gladiator, berserker or mage (next to your champions of them) is "Ash the Undying" or "The
  Rusty Blades".

In the campaign each shared list is a challenger type with no `monsters` (`generic_single`, `generic_team`: the heading
and the section's shape), keyed like any other (`challenger.generic_single.name.001`). The shared descriptions should
say nothing a particular stat block would contradict, because any stat block can wear them.

### Champions

A `kind: champion` line under a type's `stats:` line makes it **a champion of its stat blocks** instead of their owner:

```
# Champion (melee)
stats: gladiator or berserker
kind: champion
```

A champion's challengers are those stat blocks with the champion's own names and descriptions, standing as the stat
blocks' own minis. Unlike any other type, **a champion doesn't claim them**, so a plain gladiator, berserker or mage
still comes up under the shared lists as well. Without the line, the type claims its stat blocks like any other. In the
campaign it is `"kind": "champion"` (the default is `"own"`).

### Reputation and the roster's size

The hero's reputation starts at 0 and never goes below it. A won bout adds 5 and a lost one takes 2 (the Crucible's
Prose.md). **A yielded bout counts as a lost one, and so does a fled one.** Reputation sets how many challengers are
offered: 2, one more for every 10 reputation, 6 at most. `$reputation` reads it (§5). All of it can be tuned in
pack.json, each part optional:

```
"challengers": {
  "win": 5, "loss": -2,
  "roster": { "least": 2, "every": 10, "most": 6 },
  "bout_xp": { "share": 0.25, "low": 0.75, "moderate": 1, "high": 1.5 }
}
```

### The roster

- **Drawn from the run's dice and saved**, so a reload doesn't reroll it. Each challenger is a type, its next name and
  its next description. Neither list repeats until it runs out.
- **Fitted to the hero's level** by the solo budget (§1). A single or a group has the difficulty its XP comes to: low,
  moderate or high. One **over a high fight's budget, or under half a low one's**, is left out. When that leaves no
  type at all, the closest ones are offered, as low or high. A team is drawn to a difficulty picked at random (low,
  moderate or high), within the enemy cap. The roster shows each challenger's difficulty in the Workshop's words: Easy,
  Medium, Hard.
- **After each bout the roster is drawn again**, at the size the hero's reputation now allows. The one the hero beat
  leaves. One they lost to or yielded to stays, for a rematch.

### Special fights

A hand-written fight in `encounters/` can also be a challenger:

```
{ "id": "spear_trial", "kind": "fight", "map": "pit", "monsters": [ { "monster": "guard" } ],
  "challenger": { "reputation": 10, "trigger": { "roll": "1d2", "at_least": 2 } } }
```

It goes on the roster from that reputation (0 when it's left out). Each time the roster is drawn, its trigger (the
encounter table's own shape) is rolled, and it appears when the roll makes it. No trigger means every time. Specials
take their places on the roster first. It needs two locale rows, `challenger.<id>.name` and `challenger.<id>.description`,
and is otherwise an ordinary fight: its own `"defeat"`, its loot, and whatever the story does before it (its own
`<<armory>>` with only spears in it, say). Once beaten it doesn't come back. It isn't held to the level band; it's yours.

### The story's side

```
title: the_sands
---
<<challengers>>
<<if $challenger == "">>
    You head for the quartermaster. #line:to_the_racks
    <<armory crucible_racks>>
    <<jump the_sands>>
<<endif>>
<<if $challenger_special>>
    <<armory spears_only>>
<<endif>>
{$hero_name} faces {$challenger_name}! #line:announce_1
<<fight {$challenger}>>
<<jump the_sands>>
===
```

The locale's English for `announce_1` is `{0} faces {1}!`. **`<<challengers>>`** opens the roster. The player clicks a
name to see its details (description, difficulty, one opponent or a team, who steps into the arena) and presses Fight,
or goes **Back to the story**. **`<<fight {$challenger}>>`** fights the pick, and `$fight` is won, lost or yielded as
ever.

**Naming the challenger in a line:** `{$challenger_name}` holds the name's locale key, and a line shows the key's words.
That works in any line. It is also how the Announcer pool (`Line Pools/Announcer.md`, with its `{hero}` and
`{challenger}`) can come in later: each pool line becomes a Yarn line with `{$hero_name}` and `{$challenger_name}` in
it, and `{hero}` and `{challenger}` in its English become `{0}` and `{1}`. Importing the pool itself isn't built yet.

### A bout's XP

In a campaign that levels by XP (§8), **a won bout gives a quarter of the XP the hero needs to go from their level to
the next, times ¾ for a low challenger, 1 for moderate and 1½ for high. It replaces the monsters' own XP for that bout.
It doesn't add to it.** That comes to about 4 bouts a level at every level: 5 or 6 if the player always picks easy ones,
3 if they always pick hard ones. A lost or yielded bout gives none. `<<xp>>` still gives more on top. The share and the
three multipliers are pack.json's `bout_xp`. `dotnet run --project sim -- bouts` prints the table.
