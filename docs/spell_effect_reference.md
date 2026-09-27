# Spell effect settings: reference and overlap audit

*Written 2026-09-25 by Claude Code for `_design_docs/cc_task_seams-and-drop-in.md`, Steps 2 and 2b. This is
documentation only: nothing in the code or the data was changed to write it. Built from `SpellEffect`
(`core/Magic/Spell.cs`), the JSON parse in `content/Spells/SpellReader.cs` (`ReadEffect` and `Check`), what
`core/Magic/Casting.cs` does with each setting, and a scan of every effect in `content/srd/`.*

A spell is a list of **effects**. Each effect names one **primitive** (damage, heal, afflict, sway, zone, …)
and fills in whichever settings that primitive needs. This doc lists every setting, says which spells use
it, and then looks for settings that are the same idea written more than once.

**How to read the numbers.** Spell counts are over the 123 functioning spells in `content/srd/spells/`. The
Dragonborn's five breath weapons (`species.json`) and the giant spider's web (`monsters.json`) are written as
effects too, so they appear where they use a field. Method names are from the tree on 2026-09-25.

*Correction (2026-09-27): an earlier note here said the spell JSON must stay as it is. That wasn't Kathleen's
decision; the task file over-extended it. She has decided that the duplicates go away in the JSON too
(`_design_docs/cc_task_dedupe-effects.md`). The "merged version" column below still describes the C#.*

## 1. The reference

One row per JSON field on a spell effect. **Spells** counts the 123 functioning spells; the Dragonborn's breath and the giant spider's web are effects too and are named when they use a field. **Read in** names the method in `core/Magic/Casting.cs` (`Apply (damage)` is the damage case of `Incantation.Apply`; `Sway` is the method that copies a sway onto a `Boon`); other files follow "also".

### The effect itself

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `primitive` | name, required | Which of the closed list of primitives this effect is (damage, heal, ward, afflict, relieve, sway, zone, shift, …). It picks the branch of `Apply` that runs. | 123, e.g. eldritch_blast, fire_bolt + Dragonborn breath and giant spider's web | Apply, Again, Cast, Casting.Conjured, Casting.TotalDamage, Casting.TotalHealing …; also CombatSession.Kindly, CombatSession.Preview, CombatSession.TargetingOf, CombatSession.Template … |
| `note` | text, empty | Free text for authors, usually why a spell is approximated. Never shown to a player; only the sim prints it. | 107, e.g. eldritch_blast, guidance | nowhere (sim/Program.cs prints it) |
| `mode` | text, empty | The effect belongs to one of the spell's modes, picked at the cast (Blindness/Deafness, Enlarge/Reduce). A spell with modes needs at least two. | 9, e.g. lesser_restoration, dispel_magic + Dragonborn breath | InMode; also CombatSession.Preview |

### Targeting and area

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `reach` | name, `creature` | What the effect lands on: `caster`, `creature`, `creatures`, `burst`, `around`, `place`, `line`, `cone`, `cube`, `square`, `wall` or `zone`. | 123, e.g. eldritch_blast, fire_bolt + Dragonborn breath and giant spider's web | Apply (shift), Again, Brighten, MakeZone, OutOfReach, Resolve …; also CombatSession.MaxTargets, CombatSession.TargetingOf, CombatSession.Template, MonsterTactics.PlanFor … |
| `targets` | number, 1 | How many creatures a `creatures` reach may pick. The same creature may be picked twice (Scorching Ray). | 17, e.g. eldritch_blast, bless | Targets (through `TargetsAt`); also CombatSession.MaxTargets |
| `extra_targets_per_level` | number, 0 | More picks per slot level above the spell's own. | 14, e.g. bless, charm_person | Targets (through `TargetsAt`); also Spell.Upcastable, CombatSession.MaxTargets |
| `radius` | squares, 0 | The size of a burst, an `around`, a zone or a light. | 25, e.g. light, detect_magic | Brighten, Resolve, Targets, UnderTheZone; also CombatSession.Template, MonsterTactics.PlanFor, SpellCard.ReadArea, SpellZone.Radius |
| `length` | squares, 0 | The length of a line, cone or cube, the side of a square. On a shift of a creature it is also how far the creature may be moved. | 21, e.g. burning_hands, entangle + Dragonborn breath | Apply (shift), Again, InMode, Targets; also CombatSession.Preview, CombatSession.Template, MonsterTactics.PlanFor, SpellCard.ReadArea … |
| `width` | squares, 0 | How wide a line is. Only a line may have one. | lightning_bolt, sunbeam + Dragonborn breath | Targets; also CombatSession.Template, MonsterTactics.PlanFor |
| `points` | number, 1 | How many burst centres one casting gets (Meteor Swarm's four). A creature in two bursts is caught once. | meteor_swarm | Brighten, Targets; also SpellCard.ReadCaster |
| `up_to` | number, 0 (no cap) | An area touches at most this many creatures, the caster's foes first (Slow's six). | slow | Resolve |
| `near_first` | squares, 0 | Every target after the first must be within this many squares of the first (Chain Lightning). | chain_lightning | OutOfReach |
| `near_zone` | squares, 0 | The attack is made from the spell's own zone at a creature this close to it (Spiritual Weapon). | spiritual_weapon | Apply, OutOfReach, Targets |
| `within_zone` | flag | The aimed square has to be under the spell's zone (Call Lightning's bolts under the cloud). | call_lightning | UnderTheZone |
| `on_caster` | flag | A zone is centred on the caster's square wherever the aim was (Call Lightning, Globe of Invulnerability). | call_lightning | MakeZone, UnderTheZone |
| `unoccupied` | flag | A zone has to be put down on an empty square (Flaming Sphere). | flaming_sphere | Cast |
| `point` | flag | A zone of exactly one square. Nothing in the engine reads it: it only lets the reader accept a zone with no radius. | spiritual_weapon | nowhere (the reader's `Check` only) |

### Who it affects

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `spares_allies` | flag | The caster's own side is left out of an area (SRD's "creatures of your choice"). On a zone, the zone's pulses leave them alone. | sleep, slow, spirit_guardians | Resolve; also Encounter.Affects, SpellZone.SparesAllies |
| `spares_caster` | flag | The caster is left out of an area, allies are not (Entangle's "other than you"). | entangle | Resolve |
| `allies_only` | flag | A zone touches only the caster's own side (Pass without Trace). | pass_without_trace | not in Casting.cs; Encounter.Affects, SpellZone.AlliesOnly |
| `only_tags` | list of tags | Only creatures with one of these tags are touched; anything else is as if it saved (Hold Person's Humanoid). | charm_person, hold_person | Touches |
| `except_tags` | list of tags | Creatures with one of these tags are untouched (an elf's Trance against Sleep). | sleep | Touches |
| `only_at_or_below` | hit points, 0 | Only a creature at or below this many hit points is touched (Power Word Stun's 150). | power_word_stun | Touches |
| `only_above` | hit points, 0 | Only a creature above this many hit points is touched (Power Word Stun's fallback). | power_word_stun | Touches |
| `needs_sight` | flag | A creature that can't see is untouched; on an area it also has to see a square of it (Hypnotic Pattern). | hypnotic_pattern | Resolve, Touches |
| `max_size` | size | Only a creature of this size or smaller (Telekinesis' Huge). | telekinesis | Touches |
| `follows` | flag | Lands only on a creature the effect before it landed on (Guiding Bolt's glimmer comes with a hit). | 4, e.g. guiding_bolt, ray_of_frost | Pulse, Run |

### Damage

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `amount` | dice | Damage, healing, temporary hit points or Aid's raise: whatever this primitive counts in. | 50, e.g. eldritch_blast, fire_bolt + Dragonborn breath | Apply (through `AmountAt`); also MonsterTactics.Average |
| `per_extra_level` | dice | What upcasting adds per slot level above the spell's own. Not allowed on a cantrip. | 29, e.g. burning_hands, cure_wounds | Apply (through `AmountAt`); also Spell.Upcastable |
| `damage_type` | damage type, or `chosen` | The damage's type. `chosen` means the caster picks at the cast from `damage_choices`. On a sway with a `mark` it types the mark. | 46, e.g. eldritch_blast, fire_bolt + Dragonborn breath | Apply (damage), Sway, Swing |
| `damage_choices` | list of damage types | The types a `chosen` damage type may be (Chromatic Orb). Comes together with `damage_type: chosen`. | spirit_guardians, chromatic_orb, dragons_breath | Cast; also CombatSession.Options, CombatSession.Select |
| `attack_roll` | flag | A spell attack roll instead of a save. Never both. | 9, e.g. eldritch_blast, fire_bolt | Apply, Apply (damage); also CombatSession.Preview |
| `cantrip_scaling` | flag | A cantrip's amount gains its dice again at caster levels 5, 11 and 17. | 6, e.g. fire_bolt, sacred_flame + Dragonborn breath | Apply (through `AmountAt`) |
| `cantrip_beams` | flag | A cantrip grows by more beams (more targets), not more dice (Eldritch Blast). Not together with `cantrip_scaling`. | eldritch_blast | Targets (through `TargetsAt`) |
| `add_modifier` | flag | Adds the caster's spellcasting modifier to the amount, once (Cure Wounds). Damage or heal only. | cure_wounds, spiritual_weapon, healing_word | Apply; also MonsterTactics.Average |
| `extra_against` | list of tags | Extra dice against creatures with one of these tags (Divine Smite's fiends and undead). | divine_smite | Apply (damage) |
| `extra_amount` | dice | The extra dice for `extra_against`. The two come together. | divine_smite | Apply (damage) |
| `bonus_if` | setting tag | More dice when the fight's setting has this tag (Call Lightning in a storm). | call_lightning | Apply (damage) |
| `bonus_amount` | dice | The dice for `bonus_if`. The two come together. | call_lightning | Apply (damage) |
| `slays_at_or_below` | hit points, 0 | A creature at or below this many hit points dies instead of rolling damage; a Death Ward stops it (Power Word Kill). | power_word_kill | Apply (damage) |
| `dust` | flag | Brought to 0 by it, the creature is dust and past revival (Disintegrate). | disintegrate | Apply (damage), Apply (heal) |
| `raises_as` | statblock id | A creature it kills rises as this statblock on the caster's side at the start of the caster's next turn (Finger of Death). | finger_of_death | Apply (damage), TurnStarts |
| `raises_tag` | tag | Narrows `raises_as` to creatures with this tag (Humanoid). | finger_of_death | Apply (damage) |
| `delayed` | flag | The damage lands at the end of the target's next turn instead of now (Vitriolic Sphere's second splash). | vitriolic_sphere | Apply (damage) |
| `recurs` | flag | The damage lands again at the start of each of the target's turns while it lasts (Searing Smite). Not together with `delayed`. | searing_smite | Apply (damage) |
| `leaps` | squares, 0 | Chromatic Orb: when two damage dice match, it leaps to another picked creature within this many squares of the last, once per slot level. | chromatic_orb | Resolve |
| `extra_tiers` | list of dice by cantrip tier | Extra dice on a hit, by cantrip tier (none, then 5, 11, 17): True Strike's radiant. | true_strike | Swing |
| `reverts_shape` | flag | A failed save turns a shape-shifted creature back, and it can't shift again until it leaves the zone (Moonbeam). | moonbeam | Apply (damage), Refresh |

### Healing and hit points

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `revives` | flag | A heal that works on the dead, bringing them back with the amount (Raise Dead). Not on dust. | raise_dead | Apply (heal) |
| `raises_maximum` | flag | A sway's amount raises the hit point maximum and current hit points (Aid). | aid | Apply (sway) |
| `death_ward` | flag | The first drop to 0 is 1 instead, and an instant kill is negated (Death Ward). | death_ward | Sway |
| `eases_per_long_rest` | number, 0 | A penalty that shrinks by this much each long rest instead of ending (Raise Dead's -4). | raise_dead | Sway |
| `restores_abilities` | flag | A relieve that ends every reduction to an ability score (Greater Restoration). | greater_restoration | Apply (relieve) |

### Conditions and saves

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `condition` | condition | The condition an afflict puts on or a relieve takes off. | 27, e.g. charm_person, entangle + giant spider's web | Apply, Apply (afflict), Apply (relieve); also Encounter.Lapsing, MonsterTactics.AlreadyHas |
| `save` | ability | The saving throw. None means no save. The DC is the caster's (or the spell's `dc_ability`). | 50, e.g. sacred_flame, vicious_mockery + Dragonborn breath and giant spider's web | Apply, Apply (afflict), Apply (damage), Apply (sway), Cast; also CombatSession.Kindly, CombatSession.Preview |
| `on_save` | `half` / `negates` / `on_success`, `none` | What a successful save does. Only damage treats `half` differently: on every other primitive any value means "a failed save lands it", except `on_success`, which lands it on a success (Flesh to Stone's speed 0). | 50, e.g. sacred_flame, vicious_mockery + Dragonborn breath and giant spider's web | Apply, Apply (afflict), Apply (damage), Apply (direct), Apply (disarm), Apply (shift) …; also CombatSession.Preview |
| `same_save` | flag | Uses the save the effect before it already rolled, not a second one (Meteor Swarm's fire and bludgeoning). | 14, e.g. vicious_mockery, thunderwave | Apply |
| `save_if_unwilling` | flag | The caster's own side is not asked to save (Enlarge/Reduce). | enlarge_reduce | Apply |
| `advantage_if_fought` | flag | A hostile target saves with advantage (Charm Person). | charm_person | Apply |
| `auto_save_tags` | list of tags | Creatures with one of these tags succeed without rolling (Flesh to Stone's Construct). | flesh_to_stone | Apply |
| `auto_fail_tags` | list of tags | Creatures with one of these tags fail without rolling (Blight's Plant). | blight | Apply |
| `disadvantage_tags` | list of tags | Creatures with one of these tags save with disadvantage (Shatter's Construct). | shatter | Apply |
| `repeat_save` | flag | The target makes the same save again at the end of each of its turns; a success ends it. Afflict or sway. | 9, e.g. sleep, hold_person | Apply (afflict), Apply (sway), Hurt, SaveAgain, SaveOnce |
| `end_save` | ability | A save to end it with no save to begin with. On a condition: at the end of each turn, like `repeat_save`. On recurring damage: right after each start-of-turn burn (Searing Smite). | searing_smite, power_word_stun | Apply (afflict), Apply (damage), Apply (sway), TurnStarts |
| `repeat_unseen` | flag | The repeat save happens only when the creature ends its turn out of the caster's sight (Fear). | fear | Apply (afflict), SaveAgain |
| `save_on_damage` | flag | Taking damage also calls for the repeat save, with advantage (Hideous Laughter). | hideous_laughter | Apply (afflict), Hurt, SaveOnce |
| `worsens` | condition | Enough failed repeat saves swap the condition for this one, and the saves stop (Sleep's Unconscious). | sleep, flesh_to_stone | Apply (afflict) |
| `worsens_after` | number, 1 | How many failures `worsens` takes. The first save counts. | sleep, flesh_to_stone | Apply (afflict), SaveOnce |
| `ends_after` | number, 1 | How many successful repeat saves end it (Flesh to Stone's three). | flesh_to_stone | Apply (afflict) |
| `breaks_concentration` | flag | A failed save ends the target's concentration (Sleet Storm). | sleet_storm | Apply |
| `escape` | ability | The check a creature may spend its action on to break free (Web's Strength). | 4, e.g. entangle, web + giant spider's web | Apply (afflict), Apply (sway) |
| `escape_skill` | skill | The escape check's skill when it isn't the usual one (Maze's Investigation). | maze | Apply (afflict), Apply (sway), Escape |
| `escape_dc` | number, 0 (caster's DC) | The escape check's DC when it isn't the caster's (Maze's 20). | maze | Apply (afflict), Apply (sway), Escape |
| `shakeable` | flag | Someone within 5 feet may spend an action to end it (Sleep, Hypnotic Pattern). | sleep, hypnotic_pattern | Apply (afflict), CanBeShaken, Shake |
| `pinned` | flag | The creature can't end the condition on itself (Hideous Laughter's Prone). | hideous_laughter | Apply (afflict) |
| `immune` | list of conditions | Conditions the bearer of a sway can't have while it lasts (Gaseous Form's Prone). | gaseous_form | Sway |
| `curses` | flag | A dispel that ends only curses, all of them, whatever their level (Remove Curse). | greater_restoration, remove_curse | Apply (dispel) |

### Duration and ending

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `duration` | name, `instant` | How long it lasts: `instant`, `concentration`, `encounter`, `rest`, `long_rest`, `next_turn`, `next_turn_end`, `turn_end`. | 81, e.g. guidance, light + giant spider's web | Apply (afflict), Apply (damage), Apply (sway), MakeZone, Sway |
| `until` | `bearer` / `caster`, `bearer` | Whose turn ends a `next_turn` or `next_turn_end` duration. | 6, e.g. guiding_bolt, sunbeam | Apply (afflict), Apply (sway), Sway |
| `once` | flag | A sway spent by the first roll it touches (Guiding Bolt's glimmer, Vicious Mockery). | vicious_mockery, guiding_bolt | Sway |
| `ends_on_attack` | flag | A sway that ends when its bearer attacks or casts. | **none** | Sway |
| `ends_on_act` | flag | An afflict that ends when its bearer makes an attack roll, deals damage or casts (Invisibility). | invisibility | Apply (afflict), Unveil |
| `ends_on_damage` | `any` / `caster_side`, none | Taking damage ends a condition: any damage (Sleep), or damage from the caster's side (Charm Person). | charm_person, sleep, hypnotic_pattern | Apply (afflict), Hurt |
| `ends_at_zero` | flag | A sway that ends when its bearer drops to 0 hit points (Gaseous Form). | gaseous_form | Apply (sway), Hurt |
| `while_in_zone` | flag | An afflict from a zone holds only while its bearer is in the zone (Web). | web | Apply (afflict), Refresh |
| `gone_after_rounds` | rounds, 0 | Held this many rounds, a banished creature with one of `gone_tags` doesn't come back (Banishment). | banishment | Apply (sway), TurnStarts |
| `gone_tags` | list of tags | The tags for `gone_after_rounds`. | banishment | Apply (sway), TurnStarts |
| `permanent_after_rounds` | rounds, 0 | Held this many rounds, the condition stays after the spell ends (Flesh to Stone). The code only honours it for Petrified. | flesh_to_stone | Apply (afflict), TurnStarts |
| `on_end` | flag | Lands when the spell ends on the creature rather than when it is cast (Haste's lethargy). | haste | Ending, Run |
| `repeats` | flag | Done again on a later turn, without paying, when the caster spends the spell's `repeat` (Spiritual Weapon's swing). The spell's `repeat` and at least one `repeats` come together. | 10, e.g. hex, hunters_mark | Again; also CombatSession.Preview, CombatSession.TargetingOf |
| `repeat_only` | flag | Done only on the repeat, never on the cast (Produce Flame's hurl). A kind of `repeats`. | produce_flame, dragons_breath | Run; also CombatSession.Preview, CombatSession.TargetingOf |
| `switches` | flag | The repeating part has one target at a time; a new pick ends it on the old one (Telekinesis). | telekinesis | Again |

### Bonuses and advantage (sways)

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `sway` | number, 0 | A flat bonus or penalty (Shield's +5). On `armor_class` it is armor class. | 5, e.g. shield, haste | Apply (sway), Cast, Kindly, Sway; also CombatSession.Kindly, SpellReaction.Deflects |
| `sway_dice` | dice | A rolled bonus or penalty, rolled fresh each time (Bless's 1d4). | guidance, bless | Sway |
| `touches` | list: `attacks\|saves\|checks\|damage\|armor_class` | Which rolls the sway's number reaches. Required when there is a number. | 7, e.g. guidance, bless | Sway; also SpellReaction.Deflects |
| `skill` | skill, or `chosen` | Narrows a checks sway to one skill. `chosen` is the caster's pick at the cast (Guidance). | guidance, pass_without_trace | Cast, Sway; also MonsterTactics.PlanFor |
| `ability` | only `chosen` | An ability the caster picks at the cast narrows the lean (Hex, Enhance Ability). | hex, enhance_ability | Cast, Sway; also MonsterTactics.PlanFor |
| `ability_choices` | list of abilities | The abilities a `chosen` ability may be (Enhance Ability's five). | enhance_ability | Cast |
| `leans` | list of eight words | Advantage or disadvantage: `advantage_on_attacks`, `disadvantage_on_attacks`, `advantage_against`, `disadvantage_against`, and the same on checks and on saves. | 10, e.g. vicious_mockery, guiding_bolt | Kindly, Sway |
| `leans_on` | ability | Narrows a check or save lean, and a saves sway, to one ability (Enlarge's Strength). | 4, e.g. haste, slow | Sway |
| `mark` | dice | Extra damage every time the caster hits the bearer with an attack roll, typed by `damage_type` (Hex, Hunter's Mark). | hex, hunters_mark | Kindly, Sway |
| `unarmored_base` | number, 0 | An unarmored armor class base (Mage Armor's 13). | mage_armor | Sway |
| `resists` | `\|`-separated damage types | Damage types the bearer resists while it lasts (Stoneskin). | gaseous_form, stoneskin | Sway |
| `truesight` | flag | The bearer has Truesight (True Seeing). | true_seeing | Sway |
| `exposes` | flag | The bearer gets nothing from being unseen (Faerie Fire). | faerie_fire | Sway |
| `if_seen` | flag | The advantage against the bearer counts only for an attacker that can see it (Faerie Fire). | faerie_fire | Sway |
| `not_vs_truesight` | flag | The disadvantage against the bearer doesn't hold for an attacker with Truesight (Blur). | blur | Sway |
| `wards_spell` | spell id | The named spell's damage is turned away while it lasts (Shield against Magic Missile). | shield | Sway |
| `weapon_dice` | dice | Extra dice on weapon attacks only (Enlarge's 1d4). | enlarge_reduce | Sway |
| `weapon_dice_less` | flag | The weapon dice come off instead, never below 1 damage (Reduce). | enlarge_reduce | Sway |

### Action economy

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `no_actions` | flag | No action and no bonus action at all (Stinking Cloud's poisoned). | stinking_cloud | Sway |
| `no_reactions` | flag | No reactions (Slow). | slow | Sway |
| `no_attacks` | flag | It can't attack (Gaseous Form). | gaseous_form | Sway |
| `no_casting` | flag | It can't cast spells (Gaseous Form). | gaseous_form | Sway |
| `no_opportunity_attacks` | flag | It makes no opportunity attacks (Shocking Grasp). | shocking_grasp | Sway |
| `action_or_bonus` | flag | An action or a bonus action on its turn, not both, and one attack (Slow). | slow | Sway |
| `limited_action` | flag | One extra action each turn that only buys a weapon attack, Dash, Disengage or Hide (Haste). A grant, not a limit. | haste | Sway |

### Movement

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `speed` | feet, 0 | Feet of speed added or taken (Ray of Frost's -10). | ray_of_frost | Sway |
| `speed_change` | `double` / `half` / `zero`, none | Speed doubled, halved or made 0 (Haste, Slow, Hypnotic Pattern). | 6, e.g. haste, hypnotic_pattern | Sway |
| `fly_speed` | feet, 0 | A fly speed while it lasts; the slowest one wins (Fly, Gaseous Form). | fly, gaseous_form | Sway |
| `push` | squares, 0 | A shift pushes the creature this far straight away from the caster, stopped by walls and bodies (Thunderwave). | thunderwave | Apply (shift) |
| `teleports` | flag | A shift of the caster is magical travel: leaving a Forcecage takes a Charisma save. | misty_step, dimension_door | Apply (shift) |
| `passenger` | flag | A teleport brings one willing creature from beside the caster (Dimension Door). | dimension_door | Apply (shift), OutOfReach |
| `unseen` | flag | A teleport to any square in range, seen or not; an occupied square is 4d6 force and a failure (Dimension Door). | dimension_door | Apply (shift), OutOfReach |
| `flees` | flag | While the condition holds, the creature Dashes away from the caster at the start of each turn (Fear). | fear | Apply (afflict), TurnStarts |
| `reaction_flee` | flag | A failed save spends the creature's reaction running away from the caster (Dissonant Whispers). | dissonant_whispers | Apply (damage) |
| `command` | `approach` / `drop` / `flee` / `grovel` / `halt` | A direct's word, obeyed on the creature's next turn (Command). Only a direct has one. | command | Apply (direct) |
| `disarms` | flag | The creature drops what it holds as the condition lands (Fear). | fear | Apply (afflict) |
| `contest` | ability | A disarm is the caster's spellcasting check against the holder's check with this ability. | **none** | Apply (disarm) |

### Zones and light

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `pulses` | list: `appear\|enter\|start_turn\|end_turn\|each_square` | For an effect that reaches `zone`: the moments the zone does it. | 11, e.g. web, spirit_guardians | Again, Pulse; also Encounter.AddZone, Encounter.PulseStep, Encounter.PulseWhereItStands, Encounter.ZoneMoved … |
| `each_time` | flag | The zone acts every time, not once per turn (Wall of Fire). | wall_of_fire | not in Casting.cs; Encounter.Once, SpellZone.EachTime |
| `core_only` | flag | An effect that reaches the zone lands only on the wall itself, not the squares beside it (Wall of Fire). | wall_of_fire | Pulse |
| `rough` | flag | Difficult terrain inside the zone. | 7, e.g. entangle, web | not in Casting.cs; Encounter.IsRough, SpellZone.Rough |
| `ground` | flag | The zone is on the ground; a flyer passes over it (Grease, Spike Growth). | 4, e.g. entangle, spike_growth | not in Casting.cs; Encounter.Affects, SpellZone.Ground |
| `while_inside` | flag | A sway that reaches the zone is on whoever stands inside, while they are there (Spirit Guardians, Pass without Trace). | spirit_guardians, pass_without_trace | Targets; also SpellZone.Auras |
| `drifts` | squares, 0 | The zone moves this far away from the caster at the start of the caster's turn (Cloudkill). | cloudkill | Drift, MakeZone |
| `rams` | flag | A shift that moves the zone rolls it square by square and stops at the first creature, which it rams (Flaming Sphere). | flaming_sphere | Again |
| `radius_per_extra_level` | squares, 0 | The zone's radius grows per slot level above the spell's own (Fog Cloud). | fog_cloud | not in Casting.cs; SpellZone.Radius |
| `ring_size` | squares, 0 | For a wall: how many squares across its ring is; 0 is a straight wall only. | 4, e.g. wall_of_fire, wall_of_force | not in Casting.cs; SpellZone.Enclose, SpellZone.Inside, SpellZone.WallSquares, SpellZone.Within |
| `beside` | squares, 0 | For a wall: how far it reaches on the side the caster picks (Wall of Fire's 10 feet). | wall_of_fire | not in Casting.cs; SpellZone.BesideSquares |
| `encloses` | `bars` / `solid` | The zone's outline becomes walls on the board (Forcecage). | wall_of_force, forcecage | Caged, MakeZone; also SpellZone.Enclose |
| `cover` | number, 0 | Cover to a creature behind the zone (+5 is three-quarters) (Blade Barrier). | blade_barrier | not in Casting.cs; Encounter.Cover, SpellZone.Cover |
| `obscures` | `light` / `heavy`, none | What the zone does to sight (Fog Cloud, Darkness). | 7, e.g. web, wall_of_fire | not directly; SpellZone.Obscures → Encounter.Sight |
| `magical_darkness` | flag | Magical darkness: Truesight sees through it, Darkvision doesn't. Needs `obscures: heavy`. | darkness | Brighten (through the zone); also SpellZone.Magical, Encounter sight |
| `dispels_darkness` | flag | Ends every zone of magical darkness the effect's area touches (Sunburst). | sunburst | Resolve |
| `dim_radius` | squares, 0 | Dim light beyond the bright (Light). Nothing in the engine reads it: light is told, not played. | light, produce_flame, flaming_sphere | nowhere |
| `blocks_spells_up_to` | slot level, 0 | Spells of this level or lower cast from outside can't affect anything inside, one higher per slot level above (Globe of Invulnerability). | globe_of_invulnerability | Shielded (through the zone); also SpellZone.BlocksSpellsUpTo |
| `ends_force` | flag | A dispel at a square that ends a creation of magical force there (Disintegrate). | disintegrate | Resolve |

### Summoning, shapes and weapons

| field | type, default | what it does | spells | read in |
|---|---|---|---|---|
| `item` | item id | What a conjure makes (Goodberry). Only a conjure has one. | goodberry | Casting.Conjured |
| `count` | number, 0 | How many a conjure makes. | goodberry | Apply (conjure) |
| `size_step` | number, 0 | Size categories up or down (Enlarge +1, Reduce -1). | enlarge_reduce | Sway |
| `decoys` | number, 0 | Illusory duplicates that may take a hit instead (Mirror Image). | mirror_image | Sway |
| `banishes` | flag | A sway that takes the target off the board until the spell ends (Banishment, Maze). | banishment, maze | Apply (sway) |
| `weapons` | list of weapon ids | A sway that rewrites these weapons: they swing with the caster's spellcasting ability (Shillelagh). | shillelagh | Rewrite |
| `rewrite_die` | dice | The die a rewritten weapon rolls. | **none** | Rewrite |
| `rewrite_die_tiers` | list of dice by cantrip tier | The die by cantrip tier instead (Shillelagh's d8, d10, d12, 2d6). | shillelagh | Rewrite |
| `rewrite_damage_type` | damage type | A damage type the rewritten weapon may deal instead (Shillelagh's force), or True Strike's radiant when the target is weaker to it. | shillelagh, true_strike | Rewrite, Swing |

## 2. Usage counts

**157 JSON fields** on an effect (they fill 158 C# properties on `SpellEffect`: `skill` fills both `Skill` and `ChosenSkill`).

**Used by exactly one spell or effect: 96.** Each is a candidate special case.

`points` (meteor_swarm), `up_to` (slow), `near_first` (chain_lightning), `near_zone` (spiritual_weapon), `within_zone` (call_lightning), `on_caster` (call_lightning), `unoccupied` (flaming_sphere), `point` (spiritual_weapon), `spares_caster` (entangle), `allies_only` (pass_without_trace), `except_tags` (sleep), `only_at_or_below` (power_word_stun), `only_above` (power_word_stun), `needs_sight` (hypnotic_pattern), `max_size` (telekinesis), `cantrip_beams` (eldritch_blast), `extra_against` (divine_smite), `extra_amount` (divine_smite), `bonus_if` (call_lightning), `bonus_amount` (call_lightning), `slays_at_or_below` (power_word_kill), `dust` (disintegrate), `raises_as` (finger_of_death), `raises_tag` (finger_of_death), `delayed` (vitriolic_sphere), `recurs` (searing_smite), `leaps` (chromatic_orb), `extra_tiers` (true_strike), `reverts_shape` (moonbeam), `revives` (raise_dead), `raises_maximum` (aid), `death_ward` (death_ward), `eases_per_long_rest` (raise_dead), `restores_abilities` (greater_restoration), `save_if_unwilling` (enlarge_reduce), `advantage_if_fought` (charm_person), `auto_save_tags` (flesh_to_stone), `auto_fail_tags` (blight), `disadvantage_tags` (shatter), `repeat_unseen` (fear), `save_on_damage` (hideous_laughter), `ends_after` (flesh_to_stone), `breaks_concentration` (sleet_storm), `escape_skill` (maze), `escape_dc` (maze), `pinned` (hideous_laughter), `immune` (gaseous_form), `ends_on_act` (invisibility), `ends_at_zero` (gaseous_form), `while_in_zone` (web), `gone_after_rounds` (banishment), `gone_tags` (banishment), `permanent_after_rounds` (flesh_to_stone), `on_end` (haste), `switches` (telekinesis), `ability_choices` (enhance_ability), `unarmored_base` (mage_armor), `truesight` (true_seeing), `exposes` (faerie_fire), `if_seen` (faerie_fire), `not_vs_truesight` (blur), `wards_spell` (shield), `weapon_dice` (enlarge_reduce), `weapon_dice_less` (enlarge_reduce), `no_actions` (stinking_cloud), `no_reactions` (slow), `no_attacks` (gaseous_form), `no_casting` (gaseous_form), `no_opportunity_attacks` (shocking_grasp), `action_or_bonus` (slow), `limited_action` (haste), `speed` (ray_of_frost), `push` (thunderwave), `passenger` (dimension_door), `unseen` (dimension_door), `flees` (fear), `reaction_flee` (dissonant_whispers), `command` (command), `disarms` (fear), `each_time` (wall_of_fire), `core_only` (wall_of_fire), `drifts` (cloudkill), `rams` (flaming_sphere), `radius_per_extra_level` (fog_cloud), `beside` (wall_of_fire), `cover` (blade_barrier), `magical_darkness` (darkness), `dispels_darkness` (sunburst), `blocks_spells_up_to` (globe_of_invulnerability), `ends_force` (disintegrate), `item` (goodberry), `count` (goodberry), `size_step` (enlarge_reduce), `decoys` (mirror_image), `weapons` (shillelagh), `rewrite_die_tiers` (shillelagh)

**Used by none: 3.** `ends_on_attack`, `contest`, `rewrite_die`. The reader parses them and the engine honours them, but no data file sets them.

**Read by nothing in the engine:** `dim_radius` (parsed, never read) and `point` (only the reader's own check reads it). `note` is read only by the sim.

## 3. The overlap audit

Each candidate was checked against the code, not only the names. The verdict column says plainly when two
fields only look alike. **Risk** is the chance a merge changes behaviour: *low* means the fields are read in one
place and never set together; *medium* means several readers, or a timing difference to keep; *high* means
the merge would change what a spell does.

### 3a. Real overlaps: one idea written more than once

| # | fields | why they are one idea (checked) | merged version | spells that would change | risk |
|---|---|---|---|---|---|
| 1 | `spares_allies`, `spares_caster`, `allies_only` | All three answer "which creatures in the area count". `Resolve` filters the first two for an area; `Encounter.Affects` filters `spares_allies` and `allies_only` for a zone. No effect sets two of them. (The `around` reach also always spares the caster, hard-coded in `Targets`.) | C#: one enum, `Affects { All, NotCaster, NotCastersSide, OnlyCastersSide }`. The reader maps the three flags onto it; JSON can stay. (Compare `"affects": "foes"`.) | sleep, slow, spirit_guardians, entangle, pass_without_trace | low |
| 2 | `repeat_save`, `end_save` | Both become the same thing: `Apply` sets `Placement.RepeatSave = effect.RepeatSave ? effect.Save : effect.EndSave`. The only difference is which ability: the effect's own `save`, or a named one when there was no save to begin with. | C#: one `Ability? RepeatSave`. `repeat_save: true` fills it from `save`; `end_save: "con"` fills it directly. JSON can stay. | 9 with `repeat_save` (sleep, hold_person, slow, …), searing_smite, power_word_stun | medium: on recurring damage (Searing Smite) `end_save` is rolled at the *start* of the turn after the burn, not the end, and that timing has to survive. `save_on_damage`, `worsens` and `ends_after` are only allowed with `repeat_save` today; `end_save` would have to qualify too. |
| 3 | `delayed`, `recurs`, `on_end`, `repeats`, `repeat_only` | All say **when** the effect lands: now, at the end of the target's next turn, at the start of each of its turns, when the spell ends, now and on each repeat, or only on a repeat. The reader already forbids `delayed` with `recurs`, and `repeat_only` without `repeats`; in the data no effect combines any others. | C#: one enum, `Lands { Now, Later, EachTurn, OnEnd, NowAndOnRepeat, OnlyOnRepeat }`. JSON can stay (produce_flame and dragons_breath set both `repeats` and `repeat_only`, which maps to `OnlyOnRepeat`). | 10 with `repeats`, produce_flame, dragons_breath, vitriolic_sphere, searing_smite, haste | medium: five readers (`Run`, `Again`, `Pulse`, `Apply`, `CombatSession.TargetingOf`/`Preview`), and the spell-level `repeat` must still agree. |
| 4 | `cantrip_scaling`, `cantrip_beams` | Two booleans for "how a cantrip grows at 5, 11 and 17": by dice or by beams. The reader refuses both together. | C#: `CantripGrowth { None, Dice, Beams }`. JSON can stay. | 11 with `cantrip_scaling`, eldritch_blast | low |
| 5 | `weapon_dice`, `weapon_dice_less` | A number plus a flag that are one value: "+1d4" or "-1d4". `Boon.WeaponDice`'s own comment already says a negative count means "less", but `Strike` reads the flag. | C#: a signed `WeaponDice`. JSON can stay (the reader negates when the flag is set). | enlarge_reduce | low, if a negative dice count rolls correctly, which is **unclear** from `DiceRoll`. |
| 6 | `ends_on_attack`, `ends_on_act` | Both end a thing when its bearer acts. `ends_on_attack` goes on the `Boon` and ends on an attack roll or a cast (`Boons.Attacked`, `Boons.Cast`). `ends_on_act` goes on the placement and ends on an attack roll, damage dealt, or a cast (`Unveil`). **No data sets `ends_on_attack`**; Invisibility uses `ends_on_act`, and Hide ends through `Encounter.Reveal`, a third path. `Boon.EndsOnAttack`'s comment says Hide and Invisibility use it; neither does. | Drop `ends_on_attack`, or make it the sway half of `ends_on_act` with the same three triggers. | none today | low |
| 7 | `ends_on_damage`, `ends_at_zero`, `save_on_damage` | All are "what taking damage does to it", and all are read in one place, `Hurt`. They never appear together. | C#: `OnDamage { Nothing, Ends, EndsIfCastersSide, EndsAtZero, SavesAgain }`. JSON can stay. | charm_person, sleep, hypnotic_pattern, gaseous_form, hideous_laughter | medium: `ends_at_zero` sits on a sway and the others on an afflict, and the reader's per-primitive checks would move with them. |
| 8 | `while_in_zone`, `while_inside` | Both mean "it holds only while the bearer is in the spell's zone". `while_inside` is a sway aura: `Refresh` puts it on whoever steps in and takes it off whoever steps out. `while_in_zone` is a condition a pulse put on, which `Refresh` drops when the bearer leaves. Same ending rule, two starting rules. | C#: one flag. On a sway it is an aura (no pulse needed); on an afflict it is "dropped on leaving". JSON can stay. | web, spirit_guardians, pass_without_trace | medium: `Targets` special-cases `while_inside` off the board, and the reader exempts it from needing `pulses`. |
| 9 | `only_tags`, `except_tags`, `auto_save_tags`, `auto_fail_tags`, `disadvantage_tags` | Five lists with one shape: "creatures with one of these tags get X". The first two are read in `Touches`, the other three in `Apply` before the save. `extra_against` (+`extra_amount`), `gone_tags` and `raises_tag` have the same shape but are tied to another field each. | C#: a list of `TagRule(tags, outcome)` with outcome `Only`, `Untouched`, `AutoSave`, `AutoFail`, `SaveAtDisadvantage`. JSON can stay (the reader builds the list). | charm_person, hold_person, sleep, flesh_to_stone, blight, shatter | medium: `only_tags` is the inverse of the others (any tag passes, not any tag stops), so the rule order matters. The other three tag fields are better left with their partners. |
| 10 | `no_actions`, `no_reactions`, `no_attacks`, `no_casting`, `no_opportunity_attacks` | Five booleans that each forbid one kind of thing; all are copied straight onto the `Boon` by `Sway` and asked of the actor as `Boons.NoX`. | C#: a flags enum `Forbids { Actions, Reactions, Attacks, Casting, OpportunityAttacks }` on both `SpellEffect` and `Boon`. JSON can stay. | stinking_cloud, slow, gaseous_form, shocking_grasp | low to medium: every `Boons.NoX` caller changes. **Not** in this set: `action_or_bonus` (a choice, not a ban) and `limited_action` (Haste's extra action, a grant). They only look alike. |
| 11 | `obscures`, `magical_darkness` | The reader refuses `magical_darkness` unless `obscures` is `heavy`, so it is a third value of `obscures`, not a separate setting. | C#: `Obscurement { None, Light, Heavy, MagicalDarkness }`. JSON can stay. | darkness | low |
| 12 | `until` and `duration` | `until` only means something on `next_turn` and `next_turn_end` (the reader refuses it elsewhere); it says whose turn counts. | C#: two more `Duration` values, `CasterNextTurn` and `CasterNextTurnEnd`. JSON can stay. | guiding_bolt, sunbeam, ray_of_frost, telekinesis, +2 | low, but `Duration` is also `Boon`'s and items', so the new values reach them. |
| 13 | `damage_type: "chosen"` + `damage_choices`; `skill`/`ability: "chosen"` + `ability_choices` | The `"chosen"` word is redundant with a non-empty choices list, and the reader enforces that the two come together. | C#: the list alone means chosen. JSON can stay. | spirit_guardians, chromatic_orb, dragons_breath, hex, enhance_ability, guidance | low |
| 14 | `only_at_or_below`, `only_above` | One hit-point gate, both read in `Touches`, used by one spell (Power Word Stun: stunned at or below 150, speed 0 above). | C#: `HitPointGate(int line, bool above)`. JSON can stay. | power_word_stun | low (and little gain) |
| 15 | `point`, `dim_radius` | Not overlaps but dead weight: `point` is read only by the reader's own check (to let a zone have no radius), and `dim_radius` is read by nothing. | Let a zone say `radius: 0` on purpose; drop `dim_radius` or give it a reader when light is played. | spiritual_weapon; light, produce_flame, flaming_sphere | low |

### 3b. Grouping candidates: several fields that always travel together

These are not duplicates. Each is one small record split into loose fields. Grouping them is a C#-only change
(the reader builds the record from today's keys).

| fields | the record | spells |
|---|---|---|
| `escape`, `escape_skill`, `escape_dc` | `Escape(Ability, Skill, Dc)` | entangle, web, black_tentacles, maze, giant spider's web |
| `worsens`, `worsens_after`, `ends_after` (with `repeat_save`) | a save track: `RepeatSave(Ability, EndsAfter, Worsens, WorsensAfter, OnDamage, OnlyUnseen)`, taking in `save_on_damage` and `repeat_unseen` too | sleep, flesh_to_stone, hideous_laughter, fear, and the other repeat-save spells |
| `extra_against` + `extra_amount`; `bonus_if` + `bonus_amount` | one `ExtraDice(when, dice)`, the "when" being a tag on the target or a tag on the setting | divine_smite, call_lightning |
| `per_extra_level`, `extra_targets_per_level`, `radius_per_extra_level` | `Upcast(Amount, Targets, Radius)` | 29 + 14 + fog_cloud |
| `weapons`, `rewrite_die`, `rewrite_die_tiers`, `rewrite_damage_type` | already one record on the Boon side (`WeaponRewrite`); the effect could carry that record | shillelagh (true_strike uses `rewrite_damage_type` alone) |
| `raises_as` + `raises_tag`; `gone_after_rounds` + `gone_tags`; `item` + `count` | pairs the reader requires together | finger_of_death, banishment, goodberry |

### 3c. Fields that only look alike

Checked and **not** the same idea:

- **`slays_at_or_below` vs `only_at_or_below`.** One is a threshold that changes the outcome (dies instead of
  rolling damage, and a Death Ward stops it); the other is a filter on who is touched at all.
- **`gone_after_rounds` vs `permanent_after_rounds`.** Both are "held this many rounds, then it is final", but
  the first counts on the bearer's turn and removes the creature for good; the second counts on the caster's
  turn and keeps a condition. `permanent_after_rounds` only works for Petrified: `TurnStarts` checks for it by
  name. They could share an `AfterRounds(rounds, outcome)` record, but the timing difference is real.
- **`same_save` vs `follows`.** Both tie an effect to the one before it: `same_save` reuses its roll,
  `follows` lands only where it landed. Different questions.
- **`near_first`, `leaps`, `near_zone`.** All "within N squares of X", but X is the first target, the last
  target (and only on matched dice), or the zone.
- **`up_to` vs `targets`.** A cap on an area versus the number of picks.
- **`flees`, `reaction_flee`, `command: flee`.** All three run through `RunFrom`, but at different moments:
  every turn start while a condition holds, once on the target's reaction, or one commanded turn.
- **`speed` vs `speed_change`.** Adding feet and multiplying the speed are different operations. The overlap
  is on the `Boon` side (Step 2b, lead 2).
- **`once` vs `ends_on_attack`.** `once` is spent by the first roll the sway touches (including rolls made
  *at* the bearer); `ends_on_attack` by the bearer attacking or casting.
- **`disarms` (on an afflict) vs the `disarm` primitive.** Close: Fear's "drops what it holds" could be a
  second effect, a `disarm` that `follows` the afflict. It is a field doing a primitive's job, but only one
  spell uses it and the saving is small.

### 3d. Fields that one primitive's code reads and every other primitive ignores

Most fields belong to one primitive: `Apply`'s damage case alone reads 18 of them. The reader's `Check` refuses
many on the wrong primitive, but **about 25 primitive-specific fields are not checked**, so one put on the
wrong primitive loads and silently does nothing. Among them: `dust`, `raises_as`, `reverts_shape`,
`near_first`, `within_zone`, `reaction_flee` (damage only); `passenger`, `unseen` (a caster shift only);
`ends_force` (dispel); `ends_at_zero`, `gone_after_rounds`, `gone_tags` (sway); `ends_on_act`,
`while_in_zone`, `permanent_after_rounds` (afflict); `extra_tiers` (strike); `each_time`, `ground` (zone);
`weapon_dice_less` (sway). This is the strongest argument for the on-hold idea of one handler file per
primitive, each owning its own settings and its own checks.

### 3e. Counts

- **157 JSON fields** on an effect (158 `SpellEffect` properties).
- **96 used by exactly one spell or effect**, **3 used by none** (`contest`, `ends_on_attack`, `rewrite_die`),
  **2 read by nothing in the engine** (`dim_radius`, and `point` outside the reader).
- **15 real overlaps** (3a), which would remove roughly 25 fields if all were done; **6 grouping candidates**
  (3b); **9 look-alikes** that should stay apart (3c).

## 4. What I'm unsure of

- **`on_save`.** The enum comment says `half` means "half the damage, and any condition still lands", but the
  afflict case of `Apply` treats any `on_save` as "a made save stops it", so a condition with `half` would not
  land. No data does that today (only damage uses `half`), so which is intended is **unclear**. Also
  `keeps_damage` is an `OnSave` value that no code and no data uses.
- **Negative dice.** Whether a `DiceRoll` with a negative count rolls as "take this much off" is **unclear**
  from `DiceRoll`; merge 5 depends on it.
- **Upcasting shown on the card.** `Spell.Upcastable` looks only at `per_extra_level` and
  `extra_targets_per_level`. Fog Cloud (`radius_per_extra_level`) and Globe of Invulnerability
  (`blocks_spells_up_to`) do scale with the slot level in the engine (`SpellZone.Radius`,
  `SpellZone.BlocksSpellsUpTo`), but their spell card never offers a higher level (`SpellCard.HighestLevel`
  stays at the base). This looks like a gap rather than a decision; not changed here.
- **`Boon.EndsOnAttack`'s comment** says Hide and Invisibility use it. Neither does (see 3a, row 6). The
  comment is stale, or something was meant to use it.
- **`SpellEffect.ReactionFlee`'s comment** names Murmur of Dread; the data uses it on `dissonant_whispers`,
  which replaced it. Cosmetic.

## 5. Overlaps across the codebase

Step 2b: the same audit, past the spell code. Each lead from the Cowork pass was checked against the code.

### Lead 1. `Boon` and `SpellEffect` describe the same things twice: **real, and the biggest**

`Incantation.Sway` (`core/Magic/Casting.cs`) is a field-by-field copy: it builds a `Boon` from **38**
`SpellEffect` settings, most under the same name (`NoReactions`, `NoActions`, `NoAttacks`, `NoCasting`,
`LimitedAction`, `ActionOrBonus`, `WeaponDice`, `WeaponDiceLess`, `Decoys`, `FlySpeed`, `Truesight`,
`Resists`, `WardsSpell`, `IfSeen`, `NotVsTruesight`, `UnarmoredBase`, `DeathWard`, `Mark`, `Once`,
`EndsOnAttack`, `Speed`, `NoOpportunityAttacks`, `SizeStep`, `EasesPerLongRest`), some renamed
(`Immune` → `ImmuneTo`, `Exposes` → `Exposed`), and some reshaped on the way (`Leans` → eight booleans,
`SpeedChange` → three booleans, `Touches` → four booleans and an armor class number, `Weapons` + dice →
`WeaponRewrite`). So yes: **a sway effect could carry a Boon definition.** About 40 of `SpellEffect`'s 158
properties exist only to be copied onto a `Boon`.

What the shared version would be: a `BoonSpec` (what a boon *is*, with no owner and nothing rolled) that
`Boon` is made from. `SpellEffect` would hold one `BoonSpec` instead of ~40 properties, and `Sway()` would shrink
to the part that really happens at the cast:

- a skill or ability the caster picks (`skill`/`ability: chosen`),
- the owner (from `until: caster`, or a `mark`),
- Aid's rolled raise,
- Shillelagh's die by the caster's cantrip tier.

What would change: `SpellEffect`, `SpellReader` (it would read the sway settings into the spec, from the same
JSON keys), `Incantation.Sway`, and the reader's sway checks. The settings that ride on a sway but aren't a
boon (`banishes`, `escape`, `gone_after_rounds`, `ends_at_zero`, `repeat_save`) stay on the effect.

**Two more Boon vocabularies.** Three readers make Boons, each with its own words for the same things:

| | a flat number | dice | which rolls | advantage | resistances |
|---|---|---|---|---|---|
| spells (`SpellReader`) | `sway` | `sway_dice` | `touches` | `leans`, `leans_on` | `resists` (`\|`-separated) |
| class stances (`ClassReader`/`Feature.BoonFor`) | `flat` | `amount` | `touches` | `stance_advantage`, `advantage_against` | `resists` (a list) |
| items (`ItemReader`) | `flat` | `dice` | `touches` | none | none |

`touches` is already shared (all three use `Sways`). A shared `BoonSpec` reader would give one vocabulary.
**A hazard found on the way:** `Equipment.Restamped` rebuilds an item's Boon by hand and passes only the
constructor's first sixteen arguments (of eighteen), so it would drop `AdvantageAgainst`, `DisadvantageAgainst` and every
`init` property (resistances, speed, saves' advantage…). Harmless today, because item boons only set
`flat`/`dice`/`touches`/`skill`/`save`, but it is the copy that breaks first when items grow.

**`Placement` is the second copy.** `Casting.cs`'s private `Placement` class (what a spell left on a creature,
for its turns and its hurts) mirrors about 20 more `SpellEffect` settings: `Escape`, `EscapeSkill`,
`EscapeDc`, `GoneAfterRounds`, `GoneTags`, `EndsAtZero`, `EndsOnAct`, `WhileInZone`,
`PermanentAfterRounds`, `RaisesAs`, `EndsOnDamage`, `SaveOnDamage`, `Shakeable`, `Worsens`,
`WorsensAfter`, `EndsAfter`, `EndSave`/`RepeatSave`, `Command`, `Flees`, `RepeatUnseen`. Section 3's rows 2,
6, 7 and 8 and the "save track" record in 3b all land here. Between them, `Boon` and `Placement` absorb
nearly every non-primitive setting on `SpellEffect`.

### Lead 2. One idea, two shapes, inside `Boon`: **real**

- **`SpeedDoubled`, `SpeedHalved`, `SpeedZero` (three booleans) vs `SpeedChange` (an enum).** Only `Sway`
  sets them, from `SpeedChange`. A Boon can hold `SpeedChange` directly. The *aggregate* on `Boons` should stay
  three questions, because `Actor` cancels a double against a half (Haste and Slow together, `Actor.cs`).
  Risk low.
- **Boon's eight `AdvantageOn*`/`DisadvantageOn*`/`*Against` booleans vs the `Leans` flags enum.** The same
  eight. `Sway` unpacks `Leans` into them; `Feature.BoonFor` sets three from `stance_advantage`;
  `Equipment.Restamped` copies four and drops two (above). A `Leans Leans` on `Boon` would replace all
  eight, and `Boons.AnyAdvantageOnChecks` and friends become one-line flag tests. Risk low to medium (every
  reader of the booleans changes).
- **`Leans` vs `Sways`.** Only partly the same. `Leans` is {advantage, disadvantage} × {attacks, against,
  checks, saves}; `Sways` is {attacks, saves, checks, damage, armor class}. They share three targets, but
  "against" has no number (a flat penalty to attacks against you is armor class) and damage and armor class
  have no advantage. They could be one "which rolls" set plus a direction, but that set would need an
  `Against` that only means something for advantage. **Keep them apart.**
- **A fourth advantage vocabulary:** `Actor.GrantAdvantage` takes string keys (`"save:dex"`,
  `"save_vs:charmed"`, `"skill:athletics"`, `"check:str"`, `"initiative"`) from a feature's `advantage_on`.
  These are permanent, not boons, and `save_vs:<condition>` has no Boon or `Leans` equivalent; `Casting` and
  `Strike` read it by building the string. Worth folding into whatever replaces the Boon booleans.
- **`WeaponDice` + `WeaponDiceLess`** on `Boon` too; the property's own comment says a negative count already
  means "less". See section 3a, row 5.

### Lead 3. Duplicate enums: **`Pulse`/`Pulses` real but deliberate; the rest are different questions**

- **`Pulse` and `Pulses`** (`core/Combat/Zones.cs`) have the same six values; `Pulses` is the `[Flags]` set a
  zone carries, `Pulse` the single moment an event reports, and `ZonePulses.Has` bridges them. A single
  `Pulses` with one bit set could do both jobs. It would save one enum and the bridge; low value, low risk.
  Leave unless the zone code is being opened anyway.
- **`Rest` (Short/Long), `Recharge` (Short/ShortOne/Long), `Duration.Rest`/`Duration.LongRest`.** Three
  different questions on the same short-or-long axis: which rest just happened (an event, `SpellResource`),
  what a limited feature gets back on each (a policy, `Feature`), and when a boon ends (`Boon`). Not
  duplicates. `Recharge` could be written as a `Rest` plus "one use on a short rest", but it would read worse.
  **Not real.**

### Lead 4. Class-feature JSON repeats ideas the spell/boon side already has: **mostly real**

| `ClassReader` key | where it goes | the spell/boon equivalent | verdict |
|---|---|---|---|
| `resists` | `Feature.Resists` → `Boon.Resists` (a stance) | spell `resists` → `Boon.Resists` | same idea, same target. Note the class side has a second way to resist: `Trait.Resistance` with `damage_type` + `defense` sets the actor's defense for good. |
| `immune` | `Actor.MakeImmune` (for good) | spell `immune` → `Boon.ImmuneTo` (while it lasts) | same idea, different lifetime |
| `advantage_against` | `Boon` constructor's `advantageAgainst` | `leans: advantage_against` | same |
| `stance_advantage` (`str_attacks`, `str_checks`, `str_saves`) | three Boon booleans narrowed to Strength | `leans` + `leans_on: str` | same idea, second spelling |
| `advantage_on` | `Actor.GrantAdvantage` string keys | none (see lead 2) | a separate system |
| `once_per_turn` | `Hero` rider bookkeeping | nothing on an effect (a zone's once-per-turn is the zone's own rule) | not the same |
| `only_bloodied`, `unless_incapacitated` | gates on when a feature applies | none in the data; `Casting` hard-codes the same "unless incapacitated" gate for Evasion | related to problem 3 in the task (feature ids in core): Evasion could be data if features had a hook |

A shared `BoonSpec` reader (lead 1) would give stances the spell vocabulary for the first four.

### Lead 5. Targeting shapes in three places: **a legitimate view-model mapping, not real**

`Reach` (12 values) is what the engine does. `Targeting` (5, `CombatSession.TargetingOf`) is what the UI has
to collect from the player, computed from `Reach` and the primitive (a caster shift needs a square, a line
needs a direction). `RangeKind` (3, `SpellCard.ReadRange`) is how the card words the range, computed from
`Spell.Range` and `Reach`. Both are pure functions of the spell; no data is stored twice. Leave them.

### Lead 6. Speed is read in five places: **three mean the same, two don't**

`Form`, `Monster` and `Species` all read `speed` as walking feet, default 30, and put it on the actor
(`Actor.Speed`, or the `Shape` for a form). Same meaning, three one-line readers. `SpellReader`'s `speed` is a
**change** in feet (Ray of Frost's -10), which `Sway` puts on a Boon. `GameSettings.EnemySpeed` is how fast the
table animates an enemy's move: the same word, unrelated. Nothing to merge beyond, at most, a shared
`entry.Speed()` helper for the three statblock readers.

### Lead 7. `SaveGame`: **one derived field, on purpose**

Only `SavedHero.PendingImprovements` duplicates derived state, and its comment says why: it is a check on the
`Improvements` list. `Expertise` is a subset of `Skills`, also documented. Nothing else is derivable from
another field.

### Also found

- **`core/Characters/Boon.cs` holds four types** (`Duration`, `Boon`, `Boons`, `WeaponRewrite`) and a second
  `namespace` block. It wasn't in Step 1's list; it is a one-type-per-file candidate.
- **Feature ids hard-coded in core** (problem 3 in the task) were confirmed in `Apply`: `potent_cantrip`,
  `empowered_evocation`, `evasion` in the damage case and `supreme_healing`, `disciple_of_life`,
  `blessed_healer` in the heal case. Left alone, as the task says.
