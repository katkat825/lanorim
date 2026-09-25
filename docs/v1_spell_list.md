# v1 spell list — the SRD-based rebuild

*Internal design doc. Naming: the player-facing name is always **Lanorim**; internal use is either **Maps & Math Rocks** or Lanorim.*

Written 2026-09-21 from the spell scope workbook. All spell names and text come from **SRD 5.2.1
(CC-BY-4.0)** and are legal to ship.

> **Build status (2026-09-24, end of Tier 1):** all 131 are built. **Dissonant Whispers** and **Dragon's Breath** are not in SRD 5.2.1 and ship as working spells under original names (Murmur of Dread, Wyrmbreath Boon). **20 ship as renamed approximations** (8 MUST, 12 SHOULD) — down from 65 this morning; the other 109 are the SRD spell under its SRD name. The table at the end lists them.

## The plan in one paragraph

The full ~339 SRD spells all ship as **reference cards** (cheap — just data). A curated **131-spell subset
fully FUNCTIONS in v1** (**62 MUST + 69 SHOULD**), built by composing a small library of effect
**primitives** (targeting, typed damage, healing/HP, timed effects + concentration, conditions,
areas/multi-target, persistent zones, movement, visibility/light, reactions/countermagic, barriers,
illusion/info). 106 of the 131 come "for free" once those ~12 foundation primitives exist; only the
handful flagged **⚠ simplify** below need a bounded, authored approximation instead of a general system.
Pure-utility spells outside this set are handled as narrative / campaign content rather than coded.

**Resource model reminder:** these are the spells a caster can *know/equip*. **Cantrips are at-will; leveled spells spend either spell slots or spell points** — the player picks the mode at character creation (2026-09-23); upcasting spends a higher slot or more points; long rest refills. See `decisions_checklist.md` §1.

**[C]** = requires concentration. **⚠** = its effect can't match the SRD, so it ships as a bounded approximation **under a NEW NAME** (see the naming rule below), not a general subsystem.

**Area shapes (2026-09-23):** v1 supports **radius/sphere, line, and cone** templates, and **reaction spells cast as real reactions** (see `decisions_checklist.md` §6). So Lightning Bolt (line), Cone of Cold / Burning Hands (cone), and Shield / Counterspell (reaction) are **faithful, not ⚠ approximations** — the ⚠ flags below are only the genuinely-hard spells. *(2026-09-24: Sunbeam is a 60-foot **line** in the SRD, not a cone — built as a line, but still an approximation because v1 has no blinded condition.)*

**HARD NAMING RULE (`decisions_checklist.md` §1):** a 5e spell name only ever sits on the 5e effect. Any
spell whose *own* mechanics differ from the SRD (dice, save, shape, duration, targets, condition) is renamed
and never ships under the SRD name — rules-lawyer players will not forgive a familiar name that behaves wrong.
Universal substitutions that hit every spell the same (the slots-or-points choice, milestone leveling, binary concentration) are
disclosed once globally and don't count. So every ⚠ spell below needs a new name before it ships; the SRD name
survives only as a faithful reference card if we choose to show one.



---

## MUST — 62 spells


**Cantrips**

- Eldritch Blast — *Damage*
- Fire Bolt — *Damage*
- [C] Guidance — *Buff / debuff*
- Light — *Utility / narrative*
- Mage Hand — *Utility / narrative*
- Minor Illusion — *Utility / narrative*
- Prestidigitation — *Utility / narrative*
- Sacred Flame — *Damage*
- Vicious Mockery — *Damage*

**Level 1**

- [C] Bless — *Buff / debuff*
- Burning Hands — *Damage*
- Charm Person — *Control, Buff / debuff*
- Cure Wounds — *Healing / revival*
- [C] Detect Magic — *Information, Utility / narrative*
- Disguise Self — *Utility / narrative*
- [C] Entangle — *Control*
- Find Familiar — *Information, Summoning, Utility / narrative* — ⚠ Simplify: fixed familiar archetypes; scouting/interaction command menu; no freeform animal simulation.
- Guiding Bolt — *Damage*
- [C] Hex — *Damage, Utility / narrative*
- [C] Hunter’s Mark — *Damage*
- Mage Armor — *Defense*
- Magic Missile — *Damage*
- Shield — *Defense, Buff / debuff*
- [C] Sleep — *Damage, Defense, Control*
- Thunderwave — *Damage, Control*

**Level 2**

- [C] Hold Person — *Control*
- [C] Invisibility — *Defense, Control*
- Lesser Restoration — *Control*
- Mirror Image — *Defense, Control, Utility / narrative*
- Misty Step — *Movement*
- Scorching Ray — *Damage*
- Shatter — *Damage*
- [C] Spiritual Weapon — *Damage, Summoning*
- [C] Web — *Control*

**Level 3**

- Counterspell — *Control*
- Dispel Magic — *Control, Buff / debuff*
- Fireball — *Damage*
- [C] Fly — *Movement*
- [C] Haste — *Control, Buff / debuff*
- [C] Hypnotic Pattern — *Damage, Control*
- Lightning Bolt — *Damage*
- [C] Slow — *Control, Buff / debuff*
- [C] Spirit Guardians — *Damage*

**Level 4**

- [C] Banishment — *Control*
- Dimension Door — *Damage, Movement*
- [C] Greater Invisibility — *Defense, Control*
- [C] Wall of Fire — *Damage, Control*

**Level 5**

- Cone of Cold — *Damage*
- Greater Restoration — *Defense, Control, Utility / narrative*
- [C] Hold Monster — *Control*
- [C] Wall of Force — *Control* — ⚠ Simplify: snap to grid edges and fixed shapes; no freehand geometry.

**Level 6**

- Chain Lightning — *Damage*
- Disintegrate — *Damage*
- Heal — *Healing / revival, Control*
- [C] Sunbeam — *Damage, Control*

**Level 7**

- Finger of Death — *Damage, Control*
- Teleport — *Movement* — ⚠ Simplify: choose discovered places; mishap table only for unvisited/unsafe destinations if retained.

**Level 8**

- [C] Dominate Monster — *Damage, Control, Utility / narrative* — ⚠ Simplify: short tactical command set; authored dialogue reactions, not general AI obedience.
- Sunburst — *Damage, Control*

**Level 9**

- Meteor Swarm — *Damage*
- Power Word Kill — *Damage*
- Wish — *Information, Utility / narrative* — ⚠ Simplify: exact duplicate-spell option plus a small authored menu; no free-text reality alteration.

---

## SHOULD — 69 spells


**Cantrips**

- Produce Flame — *Damage*
- Ray of Frost — *Damage*
- Shillelagh — *Utility / narrative*
- Shocking Grasp — *Damage*
- Spare the Dying — *Utility / narrative*
- Thaumaturgy — *Buff / debuff, Utility / narrative*
- True Strike — *Damage*

**Level 1**

- Chromatic Orb — *Damage, Utility / narrative*
- Command — *Control*
- Dissonant Whispers — *Damage*
- Divine Smite — *Damage*
- [C] Faerie Fire — *Control, Buff / debuff*
- Feather Fall — *Damage*
- [C] Fog Cloud — *Utility / narrative*
- Goodberry — *Utility / narrative*
- Healing Word — *Healing / revival*
- Hellish Rebuke — *Damage*
- [C] Hideous Laughter — *Damage, Control*
- Identify — *Information, Utility / narrative*
- Searing Smite — *Damage, Control*
- [C] Silent Image — *Utility / narrative*
- Speak with Animals — *Utility / narrative*

**Level 2**

- Aid — *Defense, Buff / debuff*
- Blindness/Deafness — *Control*
- [C] Blur — *Defense, Buff / debuff*
- [C] Darkness — *Control*
- Darkvision — *Utility / narrative*
- [C] Dragon’s Breath — *Damage*
- [C] Enhance Ability — *Buff / debuff, Utility / narrative*
- [C] Enlarge/Reduce — *Buff / debuff, Utility / narrative*
- [C] Flaming Sphere — *Damage*
- [C] Heat Metal — *Damage*
- [C] Moonbeam — *Damage, Control*
- [C] Pass without Trace — *Buff / debuff*
- [C] Spike Growth — *Damage, Control*
- [C] Suggestion — *Control*

**Level 3**

- [C] Call Lightning — *Damage*
- [C] Fear — *Control*
- [C] Gaseous Form — *Defense, Control, Movement*
- [C] Major Image — *Control, Utility / narrative*
- Remove Curse — *Utility / narrative*
- [C] Sleet Storm — *Control*
- Speak with Dead — *Information, Utility / narrative*
- [C] Stinking Cloud — *Control*

**Level 4**

- [C] Black Tentacles — *Damage, Control*
- Blight — *Damage*
- Death Ward — *Defense*
- Ice Storm — *Damage*
- [C] Polymorph — *Defense* — ⚠ Simplify: curated form cards with pre-authored stat blocks; no arbitrary SRD creature browser.
- [C] Stoneskin — *Defense, Buff / debuff*
- Vitriolic Sphere — *Damage, Control*

**Level 5**

- [C] Cloudkill — *Damage, Control*
- Flame Strike — *Damage*
- Raise Dead — *Healing / revival, Buff / debuff, Utility / narrative*
- [C] Telekinesis — *Control, Utility / narrative* — ⚠ Simplify: combat shove/restrain plus tagged movable objects; no general physics sandbox.

**Level 6**

- [C] Blade Barrier — *Damage, Control*
- [C] Flesh to Stone — *Control*
- [C] Globe of Invulnerability — *Defense, Control*
- True Seeing — *Utility / narrative*

**Level 7**

- [C] Forcecage — *Control*
- Plane Shift — *Movement, Information*
- [C] Reverse Gravity — *Utility / narrative*

**Level 8**

- [C] Antimagic Field — *Control, Movement*
- [C] Earthquake — *Control* — ⚠ Simplify: encounter-wide hazard pulses and authored destructible tags; no structural simulation.
- [C] Maze — *Utility / narrative*
- Power Word Stun — *Control*

**Level 9**

- Foresight — *Buff / debuff*
- [C] Shapechange — *Defense, Utility / narrative* — ⚠ Simplify: curated high-level forms and one swap per turn; exclude arbitrary monster catalog.
- [C] True Polymorph — *Control, Utility / narrative* — ⚠ Simplify: curated combat forms; permanent narrative transformations only at authored hooks.

---

## Renamed approximations — as built (regenerated 2026-09-24, end of Tier 1)

*Generated by `tools/spell_review.py` from `content/srd/spells/*.json` (`"approximated": true`) and `game/locale/game.csv`. 20 of the 131 built spells (8 MUST, 12 SHOULD) ship under a new name; why each deviates is in its `note` fields and in `_design_docs/REVIEW_spell_names.md`. Two more (Dissonant Whispers, Dragon's Breath) are not SRD spells and ship under original names.*

| Tier | Lvl | SRD spell (id) | Ships as |
|---|---|---|---|
| MUST | 1 | `find_familiar` | Bonded Familiar |
| MUST | 3 | `fly` | Skystride |
| MUST | 3 | `slow` | Leaden Limbs |
| MUST | 4 | `banishment` | Exile |
| MUST | 5 | `wall_of_force` | Force Bulwark |
| MUST | 7 | `teleport` | Far Travel |
| MUST | 8 | `dominate_monster` | Master's Command |
| MUST | 9 | `wish` | Heart's Desire |
| SHOULD | 1 | `feather_fall` | Drifting Descent |
| SHOULD | 2 | `heat_metal` | Forge Fever |
| SHOULD | 2 | `suggestion` | Honeyed Word |
| SHOULD | 3 | `gaseous_form` | Mist Walk |
| SHOULD | 4 | `polymorph` | Borrowed Form |
| SHOULD | 7 | `plane_shift` | Worldwalk |
| SHOULD | 7 | `reverse_gravity` | Skyward Fall |
| SHOULD | 8 | `antimagic_field` | Null Sphere |
| SHOULD | 8 | `earthquake` | Rending Quake |
| SHOULD | 8 | `maze` | Labyrinth Exile |
| SHOULD | 9 | `shapechange` | Myriad Forms |
| SHOULD | 9 | `true_polymorph` | Perfect Remaking |

| Not in SRD | Lvl | v1 list name (id) | Ships as |
|---|---|---|---|
| SHOULD | 1 | `dissonant_whispers` | Murmur of Dread |
| SHOULD | 2 | `dragons_breath` | Wyrmbreath Boon |
