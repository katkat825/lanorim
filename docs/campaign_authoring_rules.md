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
| `$fight` | `<<fight>>` | `"won"`, `"lost"` or `"fled"` |
| `$fight_broke` | `<<fight>>` | won because the enemies' morale broke (a rout or a surrender), not because the last fell |
| `$loot_gold` | `<<loot>>` | the gold it found |
