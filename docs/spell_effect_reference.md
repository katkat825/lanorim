# Spell effect vocabulary: every key a content file can use

Every key a reader accepts in a spell, class, feature, item, species, form, background, consequence or monster
file, with what it means, what it replaced and why no existing key would do. A spell is a list of **effects**;
each names one **primitive** (damage, heal, afflict, sway, zone, ...) and fills in the keys that primitive takes.
A **boon** is what a creature carries - from a spell's sway, an item, or a feature's `boons` - in one vocabulary.
The tables below are held to the readers by `content.tests/VocabularyGuardTests.cs`, which finds them under the
heading "The vocabulary".

## The vocabulary

*Written 2026-09-27 by Claude Code for `_design_docs/cc_task_dedupe-effects.md`. The audit of 2026-09-25 that
led to it, in the old names, is `_design_docs/done/spell_effect_audit_2026-09-25.md`; this is the vocabulary now. Every key a reader
accepts in a spell, class, feature, item, species, form, background, consequence or monster file has one
row here, and a test
(`content.tests/VocabularyGuardTests.cs`) fails if a reader accepts a key with no row, or a row names a key no
reader accepts. So adding a key means adding a row, with a reason in the last column why no existing key would do.
A key in no data file is marked `reserved` with a reason; a second test fails on an unused key that isn't.*

**Proposed, not yet approved.** The task asked for this section to be approved before any data changed. Kathleen
asked for every phase to be done in one run, so the data was rewritten to these names straight away. The one-time
migration script and its retired-key table were deleted on 2026-09-28 (no files outside the repo used the old keys),
so an old key is now refused like any unknown key, with the nearest known key suggested. A name she wants changed
is a rename in the reader, the data and this table.

### How to read the tables

- **Where a key sits.** Each table's heading names the object: `effect` is one entry in a spell's `effects` list;
  `effect.upcast` is the record under an effect's `upcast` key; `boon` is the keys that describe a boon, which sit
  directly on a `sway` effect, and on each entry of a class or species feature's `boons` or an item's `boons`.
- **Used by** says which file types and which primitives take the key. "every effect" means the key is common to
  all primitives; otherwise the key belongs to the named primitive's handler (`core/Magic/Primitives/`) and the
  reader refuses it anywhere else.
- **Replaces** names the old keys a merged key took the place of. "(new)" is a key this task added in place of
  something written only in code.
- **Why not an existing key** is filled in for each new key. "existing" means the key was already in the files
  before this task and kept its name.

### The Boon vocabulary: which word was kept, and why

| idea | the words there were | kept | why |
|---|---|---|---|
| a flat number | `sway` (spells), `flat` (stances, items) | `flat` | says what it is. "sway" was the primitive's name reused for its number |
| a rolled number | `sway_dice` (spells), `amount` (stances), `dice` (items) | `dice` | plainest, and `amount` already means an effect's damage or healing |
| advantage and disadvantage | `leans` (spells), `stance_advantage` + `advantage_against` (stances), `advantage_on` strings (features) | `leans`, narrowed by `ability`, `skill` or `against` | one list for every way a roll can lean; `stance_advantage` could only say three of them, and `advantage_on`'s strings were a second vocabulary for the same leans (leftovers Part A) |
| narrowed to one ability | `leans_on` (spells), `strength_only` and the `str_` in `stance_advantage` (stances), `save` (items) | `ability` | "rolls made with this ability"; a stance's Strength and Enlarge's Strength are the same idea |
| a choice left to the caster | `skill: "chosen"`, `ability: "chosen"` + `ability_choices`, `damage_type: "chosen"` + `damage_choices` | `skill_choices`, `ability_choices`, `damage_choices` | the list of what may be picked is the choice; the extra word said nothing the list didn't (3a #13) |
| what it forbids | `no_actions`, `no_reactions`, `no_attacks`, `no_casting`, `no_opportunity_attacks` | `forbids: [...]` | one list instead of five flags (3a #10); Moonbeam's no-shifting joins it |
| extra dice on weapon hits | `weapon_dice` + `weapon_dice_less` | `weapon_dice: "-1d4"` | the sign is plainest where the dice are written. A `DiceRoll` can't be negative, so in C# the minus is a `Less` flag inside `SignedDice` (3a #5) |
| resistances | `resists: "a\|b"` (spells), `resists: [a, b]` (stances), `defense` + `damage_type` (resistance features), `defenses` (statblocks) | `defenses: {"fire": "resistant"}` | the statblock's record, which says immune and vulnerable too; one reader for both (leftovers #11) |
| a mark | `mark` + the effect's `damage_type` | `mark: {"dice", "damage_type"}` | the type belongs to the mark, not to the sway effect it rides on |
| a weapon rewrite | `weapons`, `rewrite_die`, `rewrite_die_tiers`, `rewrite_damage_type` | `rewrite: {"weapons", "die_tiers", "damage_type"}` | the existing `WeaponRewrite` record, as one key (3b) |

### Choices made without stopping (cc_task_dedupe-leftovers.md, 2026-09-27)

Kathleen asked for the leftovers task in one run, so where it said "stop and show Kathleen" the plainest name was
picked. Any of them can still be renamed back in the reader and the data. The run log (`_design_docs/RUN_LOG_2026-09-27_leftovers.md`) has the longer reasons.

| idea | the words there were | kept | why |
|---|---|---|---|
| what a feature gives | boon keys on the feature itself, `advantage_on`, `unless_incapacitated`, `defense`, a stance's `flat` | `boons: [...]`, each in the boon keys | the item's word for the same list; a feature's `flat` meant a stance's bonus, a speed in feet and hit points |
| advantage on initiative, and against one condition | `"initiative"`, `"save_vs:<condition>"` strings | the leans `advantage_on_initiative`, and `against` | the two ideas `leans` couldn't say, added once to `BoonSpec` |
| a feature's feet of speed | `flat` (Fast Movement), a boon's `speed` | `speed_change` | `speed` is walking speed everywhere else; Fast Movement stays a feature (heavy-armor gate, the sheet's speed) |
| a death intercept's hit points | `flat` (Relentless Endurance), `hp_per_level` (Relentless Rage) | `stays_up_at: {"hit_points" \| "per_level"}` | one record for one number |
| a DC | `save_dc` (features), `action_dc` (statblocks) | `dc`, on the thing it is the DC of | every other DC is `dc` |
| extra damage on a hit, and how long its condition lasts | `damage` + `until_next_turn` (statblock and form `on_hit`, a form's `rider`) | `amount` + `duration: "next_turn_end"` | a feature rider's and an effect's words; one `AttackReader` reads every attack |
| limited uses | `per_day` (statblocks) | `uses` | a feature's word |
| how it comes back | `recharge: 5` (a d6), `recharge: "long"` (a rest) | `recharge: {"d6": 5}`, `recharge: "long_rest"` | one key, one idea (how it comes back), and a long rest spelled one way |
| what part of the turn | `casting_time`, a feature's `cost`, `grants`, `use_time`, and two enums | `Spend`'s words everywhere; a feature's `use_time` | `cost` is an item's price; `casting_time` keeps the SRD's name on a spell's card |
| a tag filter | extra dice `against`, gone `tags`, raises `tag` | `tag_rules: {"<tag>": "only"}` | the effect's words for a filter on tags |
| what an effect lands on | `reach` | `aim` | an attack's `reach` is squares, SRD's word |
| class training | `armor`, `weapons` | `armor_training`, `weapon_proficiencies` | SRD's names; `armor` is an item's record and `weapons` a rewrite's list |
| armor's category | `weight` | `category` | a weapon's word; `weight` is a table's odds |
| a rider's weapons | `weapon` | `needs_weapon` | `weapon` is an item's record |
| a statblock's bonus action and condition immunities | `bonus_action`, `condition_immunities` | `manoeuvres`, `immune` | a feature's and a boon's words |
| a spell's own duration | `lasts` | `duration` | the effect's word, one level up |

### The words enum-valued keys take

| key | values |
|---|---|
| `affects` | `"all"` (the default) \| `"not_caster"` \| `"foes"` \| `"allies"` |
| `lands` | `"now"` (the default) \| `"next_turn_end"` \| `"each_turn"` \| `"on_end"` \| `"now_and_on_repeat"` \| `"on_repeat"` |
| `cantrip_growth` | `"dice"` \| `"beams"` |
| `on_damage` | `"ends"` \| `"ends_if_caster_side"` \| `"ends_at_zero"` \| `"saves_again"` |
| `obscures` | `"light"` \| `"heavy"` \| `"magical_darkness"` |
| `duration` | `"instant"` \| `"concentration"` \| `"encounter"` \| `"rest"` \| `"long_rest"` \| `"next_turn"` \| `"next_turn_end"` \| `"turn_end"` \| `"caster_next_turn"` \| `"caster_next_turn_end"` \| `"permanent"` (a feature's boons) |
| `tag_rules` | a record of tag: outcome, the outcome `"only"` \| `"untouched"` \| `"auto_save"` \| `"auto_fail"` \| `"save_disadvantage"` |
| `forbids` | a list of `"actions"`, `"reactions"`, `"attacks"`, `"casting"`, `"opportunity_attacks"`, `"shifting"` |
| `leans` | pipes of `advantage_on_attacks`, `disadvantage_on_attacks`, `advantage_against`, `disadvantage_against`, `advantage_on_checks`, `disadvantage_on_checks`, `advantage_on_saves`, `disadvantage_on_saves`, `advantage_on_initiative` |
| `touches` | pipes of `attacks`, `saves`, `checks`, `damage`, `armor_class` |
| `speed_change` | feet, signed (`-10`), or `"double"` \| `"half"` \| `"zero"` |

### Before and after

**Fire Bolt** (simple: one merge).

```json
before  {"primitive": "damage", "reach": "creature", "amount": "1d10", "damage_type": "fire",
         "attack_roll": true, "cantrip_scaling": true}
after   {"primitive": "damage", "aim": "creature", "amount": "1d10", "damage_type": "fire",
         "attack_roll": true, "cantrip_growth": "dice"}
```

**Bless** (a sway: the Boon vocabulary, and the upcast record).

```json
before  {"primitive": "sway", "reach": "creatures", "targets": 3, "extra_targets_per_level": 1,
         "sway_dice": "1d4", "touches": "attacks|saves", "duration": "concentration"}
after   {"primitive": "sway", "aim": "creatures", "targets": 3, "upcast": {"targets": 1},
         "dice": "1d4", "touches": "attacks|saves", "duration": "concentration"}
```

**Sleep** (an afflict with a save track).

```json
before  {"primitive": "afflict", "reach": "burst", "radius": 1, "condition": "incapacitated",
         "save": "wis", "on_save": "negates",
         "repeat_save": true, "worsens": "unconscious", "worsens_after": 2,
         "ends_on_damage": "any", "shakeable": true,
         "except_tags": ["sleepless"], "spares_allies": true, "duration": "concentration"}
after   {"primitive": "afflict", "aim": "burst", "radius": 1, "condition": "incapacitated",
         "save": "wis", "on_save": "negates",
         "repeat_save": {"ability": "wis", "worsens": "unconscious", "worsens_after": 2},
         "on_damage": "ends", "shakeable": true,
         "tag_rules": {"sleepless": "untouched"}, "affects": "foes", "duration": "concentration"}
```

### Deleted outright (Phase 2)

`contest`, `ends_on_attack` (and `Boon.EndsOnAttack`), `rewrite_die` and the `OnSave.KeepsDamage` value: nothing used
them. `point`: a zone of one square says `"radius": 0`. `dim_radius`: nothing read it; the three spells' numbers are
in `docs/deferred.md` for when dim light is played. Each is refused by name if a file still has it.

### `spell`: a spell, in a spell file, a species feature's `spell`, or a statblock action's `spell`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the spell's id; its name and text come from the locale | | spell file, species, monster | existing |
| `level` | 0 to 9 | 0 is a cantrip | | spell file, species, monster | existing |
| `school` | school | the school of magic | | spell file, species, monster | existing |
| `range` | squares | how far it reaches; 0 is touch or self | | spell file, species, monster | existing |
| `concentration` | flag | the caster holds it, one at a time | | spell file | existing |
| `ritual` | flag | it may be cast as a ritual | | spell file | existing |
| `approximated` | flag | it doesn't do what the SRD spell does, so it ships renamed | | spell file | existing |
| `classes` | list of class ids | the class lists it is on | | spell file | existing |
| `casting_time` | `action`, `bonus_action`, `reaction` | what casting it costs out of the turn | | spell file, species | existing: the SRD's name on a spell's card; its words are `Spend`'s, the same as `use_time` and `grants` (leftovers #13) |
| `trigger` | a moment | what a reaction spell (or a smite) answers | | spell file | existing |
| `repeat` | `action`, `bonus_action` | what a later turn spends to do its repeating part again | | spell file, species | existing |
| `duration` | duration | how long a repeating spell with no concentration stays repeatable (Produce Flame) | `lasts` | spell file | an effect's word for how long, one level up (leftovers #13) |
| `shapes` | `line`, `ring` | the shapes a wall may be put down in, picked like a mode | | spell file | existing |
| `curse` | flag | Remove Curse ends it whatever its level | | spell file | existing |
| `range_scales` | flag | a cantrip whose range doubles at 5, 11 and 17 | | spell file | existing |
| `not_in_srd` | flag | not an SRD 5.2.1 spell at all, so it ships under an original name | | reserved: none since Dissonant Whispers and Dragon's Breath were found in the SRD (2026-09-25); a campaign's own spells may use it | existing |
| `answers_spell` | spell id | a reaction that also answers being targeted by this spell (Shield and Magic Missile) | | spell file | existing |
| `out_of_combat` | flag | a minute's or an hour's casting: cast out of a fight only | | spell file | existing |
| `concentration_below` | slot level | cast at this level or higher it needs no concentration (Major Image) | | spell file | existing |
| `ends_previous` | flag | casting it again ends the one already cast (Foresight) | | spell file | existing |
| `moves_when_down` | flag | its repeat moves it to a new creature once the one it is on drops (Hex) | | spell file | existing |
| `force_creation` | flag | a creation of magical force, which Disintegrate destroys | | spell file | existing |
| `dc_ability` | ability | the DC is 8 + proficiency + this ability, whoever casts it (a Dragonborn's breath) | | species | existing |
| `effects` | list of effects | what the spell does, one primitive each | | spell file, species, monster | existing |

### `effect`: one entry in a spell's `effects` list

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `primitive` | primitive | which primitive this effect is; it picks the handler | | every effect | existing |
| `note` | text | for authors: why a spell is approximated. never shown to a player | | every effect | existing |
| `mode` | word | the mode this effect belongs to, picked at the cast (Enlarge/Reduce) | | every effect | existing |
| `aim` | `caster`, `creature`, `creatures`, `burst`, `around`, `place`, `line`, `cone`, `cube`, `square`, `wall`, `zone` | who or what it lands on | `reach` | every effect | `reach` is an attack's reach in squares, SRD's word (leftovers #13) |
| `targets` | number | how many creatures a `creatures` reach may pick | | every effect | existing |
| `radius` | squares | the size of a burst, an `around`, a zone or a light. 0 on a zone is one square, on purpose | `point` | every effect | existing |
| `length` | squares | a line's, cone's or cube's length, a square's side; on a creature shift, how far it moves | | every effect | existing |
| `width` | squares | how wide a line is | | every effect | existing |
| `points` | number | how many burst centres one casting gets (Meteor Swarm) | | every effect | existing |
| `up_to` | number | an area touches at most this many creatures, foes first (Slow) | | every effect | existing |
| `affects` | `all`, `not_caster`, `foes`, `allies` | which creatures in the area or zone count | `spares_allies`, `spares_caster`, `allies_only` | every effect | one word for one idea: which creatures count (3a #1) |
| `tag_rules` | record of tag: `only`, `untouched`, `auto_save`, `auto_fail`, `save_disadvantage` | what a creature's tags do to the effect | `only_tags`, `except_tags`, `auto_save_tags`, `auto_fail_tags`, `disadvantage_tags` | every effect, monster on-hit | five lists with one shape, "creatures with these tags get X" (3a #9) |
| `hit_points` | record: `at_or_below` or `above` | only a creature on one side of a hit point line is touched (Power Word Stun) | `only_at_or_below`, `only_above` | every effect | one gate, two sides (3a #14) |
| `needs_sight` | flag | a creature that can't see is untouched (Hypnotic Pattern) | | every effect | existing |
| `max_size` | size | only a creature this size or smaller (Telekinesis) | | every effect | existing |
| `follows` | flag | lands only where the effect before it landed (Guiding Bolt's glimmer) | | every effect | existing |
| `pulses` | pipes of `appear`, `enter`, `start_turn`, `end_turn`, `each_square` | for an effect that reaches the zone: when the zone does it | | every effect | existing |
| `core_only` | flag | reaching the zone, it lands only on the wall itself (Wall of Fire) | | every effect | existing |
| `switches` | flag | a repeating effect has one target at a time (Telekinesis) | | every effect | existing |
| `dispels_darkness` | flag | ends every zone of magical darkness its area touches (Sunburst) | | every effect | existing |
| `amount` | dice | damage, healing, temporary hit points, Aid's raise | | every effect | existing |
| `upcast` | record: `amount`, `targets`, `radius`, `blocks_spells_up_to` | what each slot level above the spell's own adds | `per_extra_level`, `extra_targets_per_level`, `radius_per_extra_level`, and the Globe's rule written only in code | every effect | one record, "what a higher slot adds"; the spell card reads it, so Fog Cloud and the Globe now offer higher levels (3b) |
| `damage_type` | damage type | the damage's type; on a strike, the spell's type taken when the target takes more from it | `rewrite_damage_type` on a strike | every effect | existing |
| `damage_choices` | list of damage types | the types the caster picks one of at the cast | `damage_type: "chosen"` | every effect | existing (the list alone now means chosen, 3a #13) |
| `condition` | condition | the condition an afflict puts on or a relieve takes off | | every effect | existing |
| `save` | ability | the saving throw; none means no save | | every effect | existing |
| `on_save` | `half`, `negates`, `on_success` | what a successful save does | | every effect | existing |
| `same_save` | flag | uses the save the effect before it rolled (Meteor Swarm) | | every effect | existing |
| `save_if_unwilling` | flag | the caster's own side isn't asked to save (Enlarge/Reduce) | | every effect | existing |
| `advantage_if_fought` | flag | a hostile target saves with advantage (Charm Person) | | every effect | existing |
| `breaks_concentration` | flag | a failed save ends the target's concentration (Sleet Storm) | | every effect | existing |
| `attack_roll` | flag | a spell attack roll instead of a save | | every effect | existing |
| `duration` | duration (see the values above) | how long it lasts, and whose turns count a turn-shaped one | `until` | every effect | existing (`until: caster` became the `caster_next_turn` values, 3a #12) |
| `lands` | `now`, `next_turn_end`, `each_turn`, `on_end`, `now_and_on_repeat`, `on_repeat` | when it lands | `delayed`, `recurs`, `on_end`, `repeats`, `repeat_only` | every effect | one word for "when": five flags that never combined (3a #3) |
| `cantrip_growth` | `dice`, `beams` | how a cantrip grows at 5, 11 and 17 | `cantrip_scaling`, `cantrip_beams` | every effect | one word for how a cantrip grows (3a #4) |
| `add_modifier` | flag | adds the caster's spellcasting modifier once (Cure Wounds) | | damage, heal | existing |
| `slays_at_or_below` | hit points | at or below this, the creature dies instead of rolling damage (Power Word Kill) | | damage | existing |
| `dust` | flag | brought to 0 by it, the creature is dust (Disintegrate) | | damage | existing |
| `raises` | record: `as`, `tag` | a creature it kills rises on the caster's side (Finger of Death) | `raises_as`, `raises_tag` | damage | a pair the reader required together, as one record (3b) |
| `leaps` | squares | on matching dice it leaps to another creature this close (Chromatic Orb) | | damage | existing |
| `extra_dice` | record: `dice`, and `against` or `setting` | more dice against these tags, or in this setting | `extra_against` + `extra_amount`, `bonus_if` + `bonus_amount` | damage | two pairs of one shape, "more dice when" (3b) |
| `reverts_shape` | flag | a failed save turns a shape-shifted creature back (Moonbeam) | | damage | existing |
| `reaction_flee` | flag | a failed save spends its reaction running away (Dissonant Whispers) | | damage | existing |
| `near_first` | squares | later targets must be this close to the first (Chain Lightning) | | damage | existing |
| `near_zone` | squares | the attack comes from the spell's zone at a creature this close (Spiritual Weapon) | | damage | existing |
| `within_zone` | flag | the aimed square must be under the spell's zone (Call Lightning) | | damage | existing |
| `revives` | flag | a heal that works on the dead (Raise Dead) | | heal | existing |
| `disarms` | flag | it drops what it holds as the condition lands (Fear) | | afflict | existing |
| `pinned` | flag | it can't end the condition on itself (Hideous Laughter) | | afflict | existing |
| `banishes` | flag | off the board until the spell ends (Banishment, Maze) | | sway | existing |
| `raises_maximum` | flag | the amount raises the hit point maximum and current (Aid) | | sway | existing |
| `restores_abilities` | flag | ends every reduction to an ability score (Greater Restoration) | | relieve | existing |
| `push` | squares | pushed straight away from the caster (Thunderwave) | | shift | existing |
| `teleports` | flag | the caster's shift is magical travel: out of a Forcecage takes a save | | shift | existing |
| `passenger` | flag | a teleport brings one willing creature along (Dimension Door) | | shift | existing |
| `unseen` | flag | a teleport to any square in range, seen or not (Dimension Door) | | shift | existing |
| `rams` | flag | moving the zone rolls it into the first creature in its way (Flaming Sphere) | | shift | existing |
| `rough` | flag | difficult terrain inside the zone | | zone | existing |
| `ground` | flag | the zone is on the ground; a flyer passes over | | zone | existing |
| `each_time` | flag | the zone acts every time, not once a turn (Wall of Fire) | | zone | existing |
| `cover` | number | cover to a creature behind the zone (Blade Barrier) | | zone | existing |
| `encloses` | `bars`, `solid` | the zone's outline becomes walls (Forcecage) | | zone | existing |
| `ring_size` | squares | how many squares across a wall's or a cage's ring is | | zone | existing |
| `beside` | squares | how far beside a wall it still reaches (Wall of Fire) | | zone | existing |
| `drifts` | squares | the zone moves away from its caster each turn (Cloudkill) | | zone | existing |
| `obscures` | `light`, `heavy`, `magical_darkness` | what the zone does to sight | `magical_darkness` | zone | existing (magical darkness became its third value, 3a #11) |
| `blocks_spells_up_to` | slot level | spells of this level or lower from outside can't reach inside (Globe) | | zone | existing |
| `on_caster` | flag | the zone is centred on the caster wherever it was aimed | | zone | existing |
| `unoccupied` | flag | the zone must be put down on an empty square (Flaming Sphere) | | zone | existing |
| `curses` | flag | a dispel that ends only curses, all of them (Remove Curse) | | dispel | existing |
| `ends_force` | flag | a dispel at a square ends a creation of force there (Disintegrate) | | dispel | existing |
| `extra_tiers` | list of dice by cantrip tier | extra dice on a hit by tier (True Strike) | | strike | existing |
| `command` | `approach`, `drop`, `flee`, `grovel`, `halt` | the word a direct's target obeys (Command) | | direct | existing |
| `item` | record: `id`, `count` | what a conjure makes, and how many (Goodberry) | `item` (an id), `count` | conjure | a pair the reader required together, as one record (3b) |

### `effect.upcast`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `amount` | dice | added to the amount per slot level | `per_extra_level` | damage, heal, ward, sway | part of the `upcast` record |
| `targets` | number | more picks per slot level | `extra_targets_per_level` | afflict, sway | part of the `upcast` record |
| `radius` | squares | more zone radius per slot level | `radius_per_extra_level` | zone | part of the `upcast` record |
| `blocks_spells_up_to` | slot levels | a globe stops spells this much higher per slot level | (new: the rule was only in `SpellZone`) | zone | part of the `upcast` record |

### `effect.hit_points`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `at_or_below` | hit points | only a creature with this many or fewer | `only_at_or_below` | afflict | part of the `hit_points` gate |
| `above` | hit points | only a creature with more than this | `only_above` | sway | part of the `hit_points` gate |

### `effect.extra_dice`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `dice` | dice | the extra dice, doubled on a critical like the hit's own | `extra_amount`, `bonus_amount` | damage | part of the `extra_dice` record |
| `tag_rules` | record of tag: `only` | only a creature with one of these tags | `against` (a list of tags) | damage | the effect's words for a tag filter, where it had a third name (leftovers #13) |
| `setting` | tag | only when the fight's setting has this tag (Call Lightning's storm) | `bonus_if` | damage | part of the `extra_dice` record |

### `effect.raises`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `as` | statblock id | what the dead rise as | `raises_as` | damage | part of the `raises` record |
| `tag_rules` | record of tag: `only` | only a creature with one of these tags | `tag` (a list of tags) | damage | the effect's words for a tag filter, where it had a third name (leftovers #13) |

### `effect.item`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | item id | the item a conjure makes | `item` | conjure | part of the `item` record |
| `count` | number | how many | `count` | conjure | part of the `item` record |

### `linger`: what an effect leaves on a creature, on the effect's own object (`LingerSpec`)

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `escape` | record: `ability`, `skill`, `dc` | an action and a check to break free (Web, Maze) | `escape` (an ability), `escape_skill`, `escape_dc` | afflict, sway (with `banishes`) | three fields that always travel together, as one record (3b) |
| `repeat_save` | record: `ability`, `ends_after`, `worsens`, `worsens_after`, `only_unseen` | a save made again to end it, at the end of each turn, or right after a burning (Searing Smite) | `repeat_save` (a flag), `end_save`, `worsens`, `worsens_after`, `ends_after`, `repeat_unseen` | afflict, sway, damage (with `lands: each_turn`) | the save track: `repeat_save` and `end_save` were one idea with two spellings (3a #2, 3b) |
| `on_damage` | `ends`, `ends_if_caster_side`, `ends_at_zero`, `saves_again` | what taking damage does to it | `ends_on_damage`, `ends_at_zero`, `save_on_damage` | afflict, sway (`ends_at_zero` only) | one word for "what damage does" (3a #7) |
| `while_in_zone` | flag | it holds only while the bearer is in the spell's zone: a condition drops on leaving (Web), a sway is an aura on whoever stands inside (Spirit Guardians) | `while_in_zone`, `while_inside` | afflict, sway | one idea, the same ending rule (3a #8) |
| `ends_on_act` | flag | it ends when the bearer attacks, deals damage or casts (Invisibility) | | afflict | existing |
| `shakeable` | flag | someone within 5 feet may spend an action to end it (Sleep) | | afflict | existing |
| `flees` | flag | the creature Dashes away from the caster each turn (Fear) | | afflict | existing |
| `gone` | record: `after_rounds`, `tags` | held this long, a creature with these tags doesn't come back (Banishment) | `gone_after_rounds`, `gone_tags` | sway | a pair the reader required together, as one record (3b) |
| `permanent_after_rounds` | rounds | held this long, the condition stays (Flesh to Stone) | | afflict | existing |

### `linger.escape`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `ability` | ability | the check's ability | `escape` | afflict, sway | part of the `escape` record |
| `skill` | skill | the check's skill when it isn't the usual one (Maze's Investigation) | `escape_skill` | sway | part of the `escape` record |
| `dc` | number | the DC when it isn't the caster's (Maze's 20) | `escape_dc` | sway | part of the `escape` record |

### `linger.repeat_save`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `ability` | ability | the save's ability: the effect's own save, or one named when there was none to begin with | `repeat_save: true` (the effect's `save`), `end_save` | afflict, sway, damage | part of the save track |
| `ends_after` | number | how many successes end it (Flesh to Stone's three) | `ends_after` | afflict | part of the save track |
| `worsens` | condition | enough failures swap the condition for this one (Sleep's Unconscious) | `worsens` | afflict | part of the save track |
| `worsens_after` | number | how many failures that takes; the first save counts | `worsens_after` | afflict | part of the save track |
| `only_unseen` | flag | saved again only out of the caster's sight (Fear) | `repeat_unseen` | afflict | part of the save track |

### `linger.gone`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `after_rounds` | rounds | how long it has to be held | `gone_after_rounds` | sway | part of the `gone` record |
| `tag_rules` | record of tag: `only` | only a creature with one of these tags | `tags` (a list of tags) | sway | the effect's words for a tag filter, where it had a third name (leftovers #13) |

### `boon`: what a boon is (`BoonSpec`), on a `sway` effect, a class or species feature, or an item's boon

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `flat` | number | a flat number on the rolls it touches; on armor class, armor class | `sway` (spells) | sway, feature, item | the word stances and items already used (Lead 1) |
| `dice` | dice | a rolled number, rolled fresh each time (Bless) | `sway_dice` (spells), `amount` (stances) | sway, item | the word items already used (Lead 1) |
| `touches` | pipes of `attacks`, `saves`, `checks`, `damage`, `armor_class` | which rolls the number reaches | | sway, feature, item | existing |
| `skill` | skill | narrows the checks it touches or leans on to one skill | | sway, item | existing |
| `skill_choices` | list of skills | the skills the caster picks one of at the cast (Guidance) | `skill: "chosen"` | sway | a choice is the list of what may be chosen (3a #13) |
| `ability` | ability | narrows it to rolls made with one ability: saves, check leans, a stance's attacks and damage | `leans_on` (spells), `strength_only` and the `str_` of `stance_advantage` (stances), `save` (items) | sway, feature, item | one word for "rolls made with this ability" (Leads 1 and 4) |
| `ability_choices` | list of abilities | the abilities the caster picks one of at the cast (Hex, Enhance Ability) | `ability: "chosen"` | sway | a choice is the list of what may be chosen (3a #13) |
| `leans` | pipes of the nine leans | advantage and disadvantage, on its rolls or on rolls at it; `advantage_on_initiative` is Remarkable Athlete's | `stance_advantage`, `advantage_against` (stances), a feature's `advantage_on` strings | sway, feature, item | one set for every lean (Leads 2 and 4; leftovers Part A) |
| `against` | condition | narrows its save leans to saves against one condition (Fey Ancestry's Charmed, Brave's Frightened) | `"save_vs:<condition>"` in a feature's `advantage_on` | feature | the one narrowing a lean lacked (leftovers Part A) |
| `unless_incapacitated` | flag | its leans hold only while the bearer isn't Incapacitated (Danger Sense). Evasion's gate in `DamageHandler` is the same test, hard-coded: noted for the feature-hooks task | `unless_incapacitated` on a feature | feature | the gate belongs to the boon it gates (leftovers Part A) |
| `once` | flag | spent by the first roll it touches (Guiding Bolt's glimmer) | | sway | existing |
| `mark` | record: `dice`, `damage_type` | extra damage whenever the caster hits the bearer (Hex) | `mark` + the effect's `damage_type` | sway | the mark's type belongs to the mark |
| `unarmored_base` | number | an unarmored armor class base (Mage Armor's 13) | | sway | existing |
| `defenses` | record of damage type: `resistant`, `immune`, `vulnerable` | what damage of each type does to the bearer while it lasts | `resists` (a list; `"a\|b"` before that), a feature's `defense` + `damage_type` and `trait: resistance` | sway, feature | the statblock's word and shape, read by one helper (leftovers #11) |
| `immune` | list of conditions | conditions it can't have: while a boon lasts, for good on a feature, or a statblock's | a statblock's `condition_immunities` | sway, feature, monster | existing (one key for spells, features and statblocks, Lead 4, leftovers #2) |
| `truesight` | flag | it has Truesight (True Seeing) | | sway | existing |
| `exposes` | flag | it gets nothing from being unseen (Faerie Fire) | | sway | existing |
| `if_seen` | flag | the advantage against it counts only for an attacker that sees it | | sway | existing |
| `not_vs_truesight` | flag | its disadvantage against doesn't fool Truesight (Blur) | | sway | existing |
| `wards_spell` | spell id | the named spell's damage is turned away (Shield and Magic Missile) | | sway | existing |
| `weapon_dice` | signed dice, `"1d4"` or `"-1d4"` | dice on or off weapon damage, never below 1 (Enlarge, Reduce) | `weapon_dice` + `weapon_dice_less` | sway | the sign says on or off (3a #5) |
| `forbids` | list of `actions`, `reactions`, `attacks`, `casting`, `opportunity_attacks`, `shifting` | what the bearer may not do | `no_actions`, `no_reactions`, `no_attacks`, `no_casting`, `no_opportunity_attacks` | sway | one list for five bans (3a #10) |
| `action_or_bonus` | flag | an action or a bonus action, not both, and one attack (Slow) | | sway | existing |
| `limited_action` | flag | one extra action for a weapon attack, Dash, Disengage or Hide (Haste) | | sway | existing |
| `speed_change` | feet (signed), or `double`, `half`, `zero` | walking speed changed: feet added or taken (Ray of Frost's -10), or doubled, halved or made 0 | `speed` (feet) on a boon | sway | `speed` means a creature's walking speed everywhere else (leftovers #12) |
| `fly_speed` | feet | a fly speed while it lasts (Fly) | | sway | existing |
| `death_ward` | flag | the first drop to 0 is 1 instead (Death Ward) | | sway | existing |
| `eases_per_long_rest` | number | a penalty that shrinks each long rest (Raise Dead) | | sway | existing |
| `decoys` | number | illusory duplicates that may take a hit (Mirror Image) | | sway | existing |
| `size_step` | number | size categories up or down (Enlarge, Reduce) | | sway | existing |
| `rewrite` | record: `weapons`, `die_tiers`, `damage_type` | named weapons swing with the caster's ability and roll the die for its tier (Shillelagh) | `weapons`, `rewrite_die`, `rewrite_die_tiers`, `rewrite_damage_type` | sway | the existing `WeaponRewrite` record, as one key (3b) |

### `boon.mark`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `dice` | dice | the mark's damage | `mark` | sway | part of the `mark` record |
| `damage_type` | damage type | the mark's damage type | the effect's `damage_type` | sway | part of the `mark` record |

### `boon.rewrite`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `weapons` | list of weapon ids | the weapons it rewrites | `weapons` | sway | part of the `rewrite` record |
| `die_tiers` | list of dice by cantrip tier | the die they roll by the caster's tier | `rewrite_die_tiers` | sway | part of the `rewrite` record |
| `damage_type` | damage type | a type the wielder may deal instead | `rewrite_damage_type` | sway | part of the `rewrite` record |

### `class`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the class's id | | class | existing |
| `hit_die` | die | its hit die | | class | existing |
| `saves` | list of abilities | its two save proficiencies | | class | existing |
| `skill_choices` | list of skills | the skills it picks from | | class | existing |
| `skill_picks` | number | how many it picks | | class | existing |
| `armor_training` | list of `light`, `medium`, `heavy` | SRD's Armor Training: the armor categories it may wear | `armor` | class | SRD's name; `armor` is an item's armor record (leftovers #13) |
| `shields` | flag | shield training | | class | existing |
| `priority` | list of abilities | which abilities matter most, for the ASI and the creator's suggestion | | class | existing |
| `subclass` | id | its one subclass | | class | existing |
| `companion` | id | the companion it comes with | | class | existing |
| `starting_gear` | list of item ids | its SRD kit | | class | existing |
| `gold` | number | starting gold | | class | existing |
| `tools` | list | tool proficiencies | | class | existing |
| `weapon_proficiencies` | list of `simple`, `martial` | SRD's Weapon Proficiencies | `weapons` | class | SRD's name; `weapons` is a rewrite's list of weapon ids (leftovers #13) |
| `features` | list of features | what it gets, by level | | class | existing |
| `improvement_levels` | list of levels | its ability score improvement levels, where they differ | | class | existing |

### `feature`: a class or species feature (what it gives is its `boons`, each in the `boon` keys)

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the feature's id | | class, species | existing |
| `trait` | trait | what kind of feature it is. **Left out, the feature is a shared one**: an entry of only `id` and `level` names a feature written once in `content/srd/features/shared.json` (Extra Attack, Weapon Mastery, Expertise, Darkvision), read at that level. **`narrate`** means the feature isn't played in v1 fights yet: it is on the sheet and the level-up screen, for a campaign to use, and its `note` says only what the SRD feature does | | class, species | existing |
| `level` | level | the level it arrives at | | class, species | existing |
| `note` | text | for authors | | class, species | existing |
| `tags` | list of words | what owning it makes the creature (`evasion`, `sleepless`) | | class, species | existing |
| `amount` | dice | a rider's or a recovery's dice | | class, species | existing |
| `per_level` | dice | what each step past its level adds | | class | existing |
| `per_levels` | number | how many levels a step takes | | class | existing |
| `amount_by_level` | record of level: dice | an amount by level where it grows unevenly | | class | existing |
| `damage_type` | damage type | a rider's damage type | | class | existing |
| `condition` | condition | a rider's condition | | reserved: no class rider puts a condition on yet (Sneak Attack and Divine Strike only add damage) | existing |
| `ability` | ability | the ability it works with: spellcasting, unarmored defence, an intercept's save | | class | existing |
| `when` | `always`, `with_advantage`, `on_critical`, `when_spent` | when a rider fires | | class | existing |
| `grants` | `action`, `bonus_action`, `reaction` | which part of the turn an action grant adds to | | reserved: every grant is the default, an action | existing; `Spend`'s words, where it had its own enum (leftovers #13) |
| `boons` | list of boons (the `boon` keys) | what it gives: a stance's one boon, put on when it is switched on; any other feature's, the creature's for good | the boon keys on the feature itself, `advantage_on`, `unless_incapacitated`, `defense` + `damage_type` and `trait: resistance`, a stance's `flat` | class, species | the item's word for the same list; no feature key means one thing to a stance and another to a speed feature (leftovers Part A, #4, #11) |
| `speed_change` | feet | feet added to walking speed (Fast Movement) | `flat` on a speed feature | class | the boon's word for feet of speed; a feature, not a boon, because it needs the heavy-armor gate and changes the speed the sheet shows (leftovers #4) |
| `stays_up_at` | record: `hit_points`, `per_level` | the hit points a death intercept leaves it on: 1 (Relentless Endurance), twice the level (Relentless Rage) | `flat`, `hp_per_level` | class, species | one record for one number (leftovers #4) |
| `flat_by_level` | record of level: number | a stance's flat bonus by level (Rage); the boon's `flat` is level 1's | | class | existing |
| `flat_ability` | ability | a stance's flat bonus is this ability's modifier (Sacred Weapon) | | class | existing |
| `uses` | number | uses between rests | | class, species | existing |
| `uses_by_level` | record of level: number | uses by level | | class, species | existing |
| `recharge` | `short_rest`, `one_per_short_rest`, `long_rest` | how its uses come back: the rest that gives them back (words were `short`, `short_one`, `long`; a duration's `long_rest` is the same rest) | | class, species | existing; a statblock action's `recharge` is the same idea, a d6 record (leftovers #3) |
| `spends` | feature id | the pool a use comes out of (Channel Divinity) | | class | existing |
| `count` | number | skills an expertise picks, forms a shape has, actions a grant adds | | class | existing |
| `duration` | duration | how long a stance lasts | | class | existing |
| `skills` | list of skills | trained skills | | class, species | existing |
| `saves` | list of abilities | trained saves | | class | existing |
| `progression` | caster progression | how a spellcaster climbs the slot levels | | class | existing |
| `manoeuvres` | list of `dash`, `disengage`, `hide` | what a nimble feature frees the bonus action for | | class | existing (a statblock's too, leftovers #13) |
| `once_per_turn` | flag | a rider that fires once a turn | | class | existing |
| `while_stances` | list of feature ids | works only while these stances are up | | class | existing |
| `needs_weapon` | `finesse_or_ranged`, `melee` | the weapons a rider rides on | `weapon` | class | `weapon` is an item's weapon record (leftovers #13) |
| `forgoes` | feature id | a rider that gives up this stance's advantage (Brutal Strike) | | class | existing |
| `crit_on` | number | a critical on this natural roll or higher | | class | existing |
| `aura_ability` | ability | Aura of Protection's ability | | class | existing |
| `armored_ac` | number | the Defense style's armor class while armored | | class | existing |
| `use_time` | `bonus_action`, `action`, `free` | what switching a stance on takes out of the turn | `cost` | class, species | the item's word, in `Spend`'s words; `cost` is an item's price (leftovers #13) |
| `reaction` | word | a reaction it gives (`halve` is Uncanny Dodge) | | class | existing |
| `spells` | record of level: spell ids | spells always prepared, by level | | class | existing |
| `free_casts` | record of spell id: number | casts each long rest that cost no slot | | class | existing |
| `cantrips_by_level` | record of level: number | cantrips known by level | | class | existing |
| `known_by_level` | record of level: number | spells prepared by level | | class | existing |
| `skill_picks` | number | one more skill to pick | | class | existing |
| `expertise_from` | list of skills | the skills an expertise may choose from | | class | existing |
| `dc` | number | an intercept's save DC | `save_dc` | class | the word every other DC uses (leftovers #2) |
| `dc_step` | number | how much the DC grows each use | | class | existing |
| `only_bloodied` | flag | a recovery only a Bloodied creature can take | | class | existing |
| `cap_half` | flag | up to half its hit points | | class | existing |
| `max_hp_per_level` | number | the hit point maximum rises this much each level | | species | existing |
| `not_in_heavy_armor` | flag | a speed feature that counts only out of heavy armor | | class | existing |
| `spell` | a spell | a species' own spell, inline (the Dragonborn's breath) | | species | existing |
| `spell_abilities` | list of abilities | the abilities a species' spells may be cast with | | species | existing |

### `feature.stays_up_at`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `hit_points` | number | stays up on this many (Relentless Endurance's 1) | a death intercept's `flat` | species | part of the `stays_up_at` record |
| `per_level` | number | stays up on this many per level (Relentless Rage's 2) | `hp_per_level` | class | part of the `stays_up_at` record |

### `item`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the item's id | | item | existing |
| `kind` | kind | what kind of item | | item | existing |
| `cost` | number | its price | | item | existing |
| `stackable` | flag | it stacks in the pack | | item | existing |
| `sell_percent` | number | what a merchant pays for it | | item | existing |
| `slot` | slot | where it is worn | | item | existing |
| `weapon` | record | it is a weapon | | item | existing |
| `armor` | record | it is armor | | item | existing |
| `classes` | list of class ids | only these classes may wear it | | item | existing |
| `minimum_level` | level | the level it may be worn from | | item | existing |
| `boons` | list of boons | the boons it puts on while worn or when used | | item | existing |
| `heals` | dice | what using it heals | | item | existing |
| `casts` | spell id | a spell using it casts | | reserved: no item casts a spell yet (scrolls and wands are later content) | existing |
| `uses` | number | how many times it can be used | | reserved: comes with `casts` | existing |
| `vanishes` | `long_rest` | it goes at the next long rest (Goodberry) | | item | existing |
| `use_time` | `action`, `bonus_action` | what using it takes out of the turn | | reserved: every usable item is the default, a bonus action | existing (a feature's too, leftovers #13) |

### `item.weapon`: an item's attack (the `attack` keys) and what a weapon has besides

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `category` | `simple`, `martial` | its category | | item | existing |
| `light` | flag | Light | | item | existing |
| `heavy` | flag | Heavy | | item | existing |
| `versatile` | dice | its two-handed damage | | item | existing |

### `item.armor`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `category` | `light`, `medium`, `heavy` | SRD's armor category | `weight` | item | a weapon's word for its SRD category; `weight` is a consequence's (and a loot table's) odds (leftovers #13) |
| `base` | number | its armor class | | item | existing |
| `strength` | number | the Strength it needs | | item | existing |
| `noisy` | flag | disadvantage on Stealth | | item | existing |

### `item.boon`: one entry in an item's `boons` (it also takes the `boon` keys)

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the boon's id | | item | existing |
| `duration` | duration | how long it lasts once put on | | item | existing |

### `species`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the species' id | | species | existing |
| `speed` | feet | walking speed | | species | existing |
| `bumps` | record of ability: number | fixed ability increases | | reserved: SRD 5.2.1 species give none (the background does) | existing |
| `features` | list of features | its traits | | species | existing |
| `lineages` | list of species ids | its lineages, picked in the creator | | species | existing |
| `lineage_of` | species id | the species this is a lineage of | | species | existing |

### `background`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the background's id | | background | existing (the reader checked no keys until leftovers #8) |
| `skills` | list of skills | the two skills it trains | | background | existing |
| `abilities` | list of abilities | the three abilities its score increase may go to | | background | existing |
| `gear` | list of item ids | the gear it starts with | | background | existing |
| `gold` | number | the gold it starts with | | background | existing |
| `not_in_srd` | flag | Lanorim's own, not an SRD 5.2.1 background (Recluse, in `content/srd/backgrounds/lanorim.json`) | | background | a spell's word for the same idea, reused (2026-09-28) |

### `consequence`: one entry in the nat-1 / nat-20 pool

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the consequence's id | | consequence | existing (the reader checked no keys until leftovers #8) |
| `polarity` | `bane`, `boon` | a natural 1's or a natural 20's | | consequence | existing: an outcome, not a lasting `boon` (the sweep's note in the run log) |
| `kind` | `health`, `gold`, `ability_shift`, `condition`, `flavour` | what it changes | | consequence | existing |
| `ability` | ability | the ability an `ability_shift` moves | | consequence | existing: the ability word everywhere |
| `condition` | condition | the condition a `condition` consequence gives | | consequence | existing |
| `amount` | dice | how much: hit points, gold, the shift | | consequence | existing: the effect vocabulary's word for a number rolled |
| `scales_with_level` | flag | the amount is multiplied by the level | | consequence | existing |
| `weight` | number | how often it is drawn beside the others | | consequence | existing |

### `attack`: an attack, wherever it is written - an item's `weapon`, a statblock's `attacks`, a Wild Shape form's `attacks` (`AttackReader`)

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `damage` | dice | its damage | | item, monster, form | existing: three readers read these keys three ways, one reads them now (leftovers #6) |
| `damage_type` | damage type | its damage type | | item, monster, form | existing |
| `ability` | ability | the ability it attacks with | | item, monster, form | existing |
| `reach` | squares | its reach | | item | existing |
| `range` | squares | its normal range | | item, monster | existing |
| `long_range` | squares | its long range | | item, monster | existing |
| `finesse` | flag | the better of Strength and Dexterity | | item, monster, form | existing |
| `thrown` | flag | melee within reach, thrown past it | | item, monster | existing |
| `attack_bonus` | number | a bonus to hit: a magic weapon's, a statblock's beyond its ability and proficiency | | reserved: no +1 weapons in the SRD data yet, and every SRD statblock's bonus is its ability and proficiency | existing |
| `damage_bonus` | number | a bonus to damage | | monster | existing |
| `adds_ability` | flag | whether the ability modifier is added to damage (a priest's Radiant Flame, a cat's 1) | | monster, form | existing |
| `on_hit` | record | what a hit adds | the form's `rider` | monster, form | a statblock's word; the form's `rider` was the same record under another name (leftovers #6) |

### `attack.on_hit`: what a hit adds, on any attack

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `amount` | dice | extra damage on a hit | `damage` | monster | a feature rider's and an effect's word (leftovers #1) |
| `damage_type` | damage type | its type | | monster | existing |
| `condition` | condition | a condition on a hit | | monster | existing |
| `save` | ability | the save against the condition | | monster | existing |
| `dc` | number | its DC | | monster | existing |
| `max_size` | size | only a creature this size or smaller | | monster | existing |
| `tag_rules` | record of tag: `only`, `untouched` | what a creature's tags do to the rider (the Ghoul's claw spares undead and elves) | `except_tags` | monster | the same words a spell uses (3a #9) |
| `duration` | `next_turn_end` | the condition lasts to the end of the target's next turn; left out, until something ends it | `until_next_turn` | monster | the effect vocabulary's duration word (leftovers #1) |

### `form`: a Wild Shape card

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the card's id | | form | existing (the form reader checked no keys until leftovers #8) |
| `role` | `combat`, `scout`, `travel`, `utility` | what the card is for | | form | existing |
| `challenge_times_ten` | number | the beast's challenge rating times ten; the level it opens at comes from it | | form | existing |
| `armor_class` | number | its armor class | | form | existing |
| `speed` | feet | its walking speed | | form | existing: a creature's walking speed, the statblock's and the species' word, read by one helper (leftovers #12) |
| `climb` | feet | its climb speed | | form | existing |
| `swim` | feet | its swim speed | | form | existing |
| `fly` | feet | its fly speed | | reserved: no flying card ships in v1 | existing |
| `scores` | record: `str`, `dex`, `con` | the body's scores; the mind stays the druid's | | form | existing |
| `skills` | list of skills | its trained skills | | form | existing |
| `attacks` | list of attacks | its attacks | | form | existing |
| `mini` | id | the miniature it stands as | | reserved: Kathleen's to fill | existing |

### `form.attack`: a form's attack (the `attack` keys), named

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the attack's id | | form | existing |

### `monster`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the statblock's id | | monster | existing |
| `hit_points` | number | its hit points | | monster | existing |
| `armor_class` | number | its armor class | | monster | existing |
| `speed` | feet | its walking speed | | monster | existing |
| `challenge_times_ten` | number | its challenge rating times ten | | monster | existing |
| `hit_die` | die | its hit die | | monster | existing |
| `mini` | id | the miniature it stands as | | monster | existing |
| `size` | size | its size | | monster | existing |
| `tags` | list of words | its creature type and what else it is (`undead`, `humanoid`) | | monster | existing |
| `scores` | record of ability: score | its ability scores | | monster | existing |
| `skills` | list of skills | its trained skills | | monster | existing |
| `expertise` | list of skills | its expert skills | | monster | existing |
| `saves` | list of abilities | its trained saves | | monster | existing |
| `instincts` | list of words | how it fights | | monster | existing |
| `manoeuvres` | list of `dash`, `disengage`, `hide` | what its bonus action may be spent on (Nimble Escape) | `bonus_action` | monster | the feature's word for Cunning Action, read by one helper (leftovers #13) |
| `attacks` | list of attacks | its attacks | | monster | existing |
| `multiattack` | list of attack lists | the attacks its Attack action makes | | monster | existing |
| `defenses` | record of damage type: defense | its resistances, immunities, vulnerabilities | | monster | existing (a boon's too, one helper) |
| `immune` | list of conditions | conditions it can't have | `condition_immunities` | monster | the boon's word (leftovers #2) |
| `actions` | list of actions | its special actions, each an inline spell | | monster | existing |
| `spellcasting` | record | its spellcasting | | monster | existing |

### `monster.attack`: a statblock's attack (the `attack` keys), named, and held or not

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `id` | id | the attack's id; with a `weapon`, the weapon's id when left out | | monster | existing |
| `held` | flag | a weapon in hand, which can be dropped | | monster | existing |
| `weapon` | an SRD weapon's item id | the attack is that weapon: the attack keys it leaves out are the weapon's, so the statblock writes only what differs (a Large creature's damage, an on-hit) | | monster | the same word as an item's `weapon` record, which this names; copying the weapon's keys into each statblock was the duplicate (cc_task_godfiles-dupes-efficiency.md #10) |

### `monster.action`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `spell` | a spell | the action, as an inline spell | | monster | existing |
| `recharge` | record: `d6` | how it comes back: on this roll of a d6 or higher at the start of its turn (`{"d6": 5}` is Recharge 5-6) | `recharge` as a bare number | monster | a feature's `recharge` is also how uses come back; a record keeps the one key from meaning two kinds of value (leftovers #3) |
| `uses` | number | uses, back on a long rest | `per_day` | reserved: the one SRD special action here recharges instead | a feature's word for a limited use (leftovers #3) |
| `dc` | number | its own DC | the statblock's `action_dc` | monster | the DC sits on what it is the DC of (leftovers #2) |

### `monster.action.recharge`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `d6` | number, 2 to 6 | the lowest d6 roll that brings it back | a bare-number `recharge` | monster | the record makes `recharge` one idea (leftovers #3) |

### `monster.spellcasting`

| key | type / values | meaning in plain words | replaces (old keys) | used by | why not an existing key |
|---|---|---|---|---|---|
| `ability` | ability | the ability it casts with | | monster | existing |
| `dc` | number | its spell save DC | | monster | existing |
| `attack_bonus` | number | its spell attack bonus | | monster | existing |
| `at_will` | list of spell ids | spells it casts at will | | monster | existing |
| `uses` | record of spell id: number | spells it casts so many times, back on a long rest ("1/day each") | `per_day` | monster | a feature's word for a limited use (leftovers #3) |
