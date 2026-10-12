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
| `consequences` | `"add"` (default), `"replace"` | the campaign's own `consequences/*.json` (the shape of `content/srd/consequences/pool.json`) are added to the game's nat-1 / nat-20 pool while it runs, or replace it. An entry can be flavour only: `"kind": "flavour"`, a line and no effect. Added ones can't reuse a game id; a replacing pool needs a boon and a bane |

**Yield:** in a fight where defeat continues and the map has no way off, "Yield" is in More actions. It ends the bout
at once, no knockout: `$fight` is `"yielded"`.

**An armory** is a merchant with `"armory": true` and a stock of things to wear (no consumables: the check refuses
them). `<<armory id>>` opens it: the hero puts on anything in it for free, as often as they like; what it replaces goes
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

**Format 2** adds how the map looks. Format 1 still reads, as stone floor and stone walls, so older maps load unchanged.

| Key | What it is |
|---|---|
| `map` | the squares and the lines between them, one glyph each: `.` floor, `~` difficult, `#` rock, `_` deep water or a chasm (seen across, never crossed), `@` the start, `1`-`9` spawns; `\|` `-` a wall, `x` a door |
| `ground` | one letter a square, a row of letters a row of squares: `s` stone, `p` wood planks, `g` grass, `d` dirt, `c` cobblestone, `a` sand, `n` snow, `w` shallow water. Left out when the whole map is stone |
| `walls` | the `map`'s own shape, a letter on each wall or door that isn't the dungeon's stone: `t` timber, `p` plaster and beam, `f` fieldstone, `e` wooden fence, `h` hedge. Left out when every wall is stone |
| `rough_markers` | `[[x, y], ...]`: a start or spawn standing on difficult ground (the `map` writes `@` there, which can't say so) |
| `props` | `{ "id", "x", "y", "turn" }`: a prop from the builder's palette, its top-left square and its quarter turns |

**A ground is a look; the square's glyph is its rule.** Painting a ground in the builder brings its rule with it
(shallow water and snow are difficult, the rest floor), and difficult, rock or a chasm can be painted over any ground.
Deep water is shallow water's ground on a chasm square.

**A fence or a hedge** stops a walker and not an eye: the rules see bars there. A door in it is a gate, and a shut gate
blocks the view as any door does.

**A prop can cover more than one square** (a cottage is 2 x 2, a barn 4 x 4); its size is the palette's, so the map file
only says where its top-left square is and how it's turned. A prop that blocks can't share a square with another that
blocks, or cover the start or a spawn, and a campaign won't load a map where one runs off the edge.
