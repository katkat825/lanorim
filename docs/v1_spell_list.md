# v1 spell list — the SRD-based rebuild

*Internal design doc. Naming: the player-facing name is always **Lanorim**; internal use is either **Maps & Math Rocks** or Lanorim.*

Written 2026-09-21 from the spell scope workbook. All spell names and text come from **SRD 5.2.1
(CC-BY-4.0)** and are legal to ship.

> **Build status (2026-09-25, after the SRD check):** **123 functioning spells** (62 MUST + 61 SHOULD). Kathleen deferred 7 SHOULD spells (Feather Fall, Heat Metal, Plane Shift, Reverse Gravity, Antimagic Field, Earthquake, True Polymorph; see `deferred.md`) and cut Suggestion. Their data is kept in `content/deferred/spells/`, which the loader doesn't ship. **Dissonant Whispers** (SRD p.124) and **Dragon's Breath** (SRD p.126) *are* in SRD 5.2.1. The 09-24 note said they weren't; that was wrong. Both were rebuilt from the SRD text and ship under their SRD names. **113** are the SRD spell under its SRD name. **8** keep their SRD names as approximations Kathleen approved (the allow-list under the HARD RULE in `decisions_checklist.md` §1). **2** ship renamed: Wall of Force as *Cube of Force*, Teleport as *Waypoint*. Every functioning spell was checked field by field against the SRD text (`_design_docs/SRD_CHECK_2026-09-25.md`). The table at the end lists the approximations.

## The plan in one paragraph

The full ~339 SRD spells all ship as **reference cards** (cheap — just data). A curated **123-spell subset
fully FUNCTIONS in v1** (**62 MUST + 61 SHOULD**; it was 131 until 2026-09-25, when 7 SHOULD spells were
deferred and Suggestion cut), built by composing a small library of effect
**primitives** (targeting, typed damage, healing/HP, timed effects + concentration, conditions,
areas/multi-target, persistent zones, movement, visibility/light, reactions/countermagic, barriers,
illusion/info). 106 of the 131 come "for free" once those ~12 foundation primitives exist; only the
handful flagged **⚠ simplify** below need a bounded, authored approximation instead of a general system.
Pure-utility spells outside this set are handled as narrative / campaign content rather than coded.

**Resource model reminder:** these are the spells a caster can *know/equip*. **Cantrips are at-will; leveled spells spend either spell slots or spell points** — the player picks the mode at character creation (2026-09-23); upcasting spends a higher slot or more points; long rest refills. See `decisions_checklist.md` §1.

**[C]** = requires concentration. **⚠** = its effect can't match the SRD, so it ships as a bounded approximation **under a NEW NAME** (see the naming rule below), not a general subsystem.

**Area shapes (2026-09-23):** v1 supports **radius/sphere, line, and cone** templates, and **reaction spells cast as real reactions** (see `decisions_checklist.md` §6). So Lightning Bolt (line), Cone of Cold / Burning Hands (cone), and Shield / Counterspell (reaction) are **faithful, not ⚠ approximations** — the ⚠ flags below are only the genuinely-hard spells. *(2026-09-24: Sunbeam is a 60-foot **line** in the SRD, not a cone — built as a line. 2026-09-25: the Blinded condition exists now, and Sunbeam is faithful under its SRD name, SRD p.166.)*

**HARD NAMING RULE (`decisions_checklist.md` §1):** a 5e spell name only ever sits on the 5e effect. Any
spell whose *own* mechanics differ from the SRD (dice, save, shape, duration, targets, condition) is renamed
and never ships under the SRD name — rules-lawyer players will not forgive a familiar name that behaves wrong.
Universal substitutions that hit every spell the same (the slots-or-points choice, milestone leveling, binary concentration) are
disclosed once globally and don't count. So every ⚠ spell below needs a new name before it ships; the SRD name
survives only as a faithful reference card if we choose to show one. **The one exception** is the short
allow-list Kathleen approved on 2026-09-25 (Find Familiar, Dominate Monster, Wish, Polymorph, Shapechange, Fly,
Gaseous Form, Slow). Those keep their SRD names as bounded versions, each one named with its reason under the
HARD RULE in `decisions_checklist.md` §1 and in the naming test.



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
- [C] Wall of Force — *Control* — ⚠ Simplify: snap to grid edges and fixed shapes; no freehand geometry. *Ships as **Cube of Force** (2026-09-25).*

**Level 6**

- Chain Lightning — *Damage*
- Disintegrate — *Damage*
- Heal — *Healing / revival, Control*
- [C] Sunbeam — *Damage, Control*

**Level 7**

- Finger of Death — *Damage, Control*
- Teleport — *Movement* — ⚠ Simplify: choose discovered places; mishap table only for unvisited/unsafe destinations if retained. *Ships as **Waypoint** (2026-09-25).*

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
- ~~Feather Fall~~ — *Damage* — **deferred 2026-09-25** (`deferred.md`)
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
- ~~[C] Heat Metal~~ — *Damage* — **deferred 2026-09-25** (`deferred.md`)
- [C] Moonbeam — *Damage, Control*
- [C] Pass without Trace — *Buff / debuff*
- [C] Spike Growth — *Damage, Control*
- ~~[C] Suggestion~~ — *Control* — **cut 2026-09-25** (Kathleen: skip completely; not deferred)

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
- ~~Plane Shift~~ — *Movement, Information* — **deferred 2026-09-25** (`deferred.md`)
- ~~[C] Reverse Gravity~~ — *Utility / narrative* — **deferred 2026-09-25** (`deferred.md`)

**Level 8**

- ~~[C] Antimagic Field~~ — *Control, Movement* — **deferred 2026-09-25** (`deferred.md`)
- ~~[C] Earthquake~~ — *Control* — ⚠ Simplify: encounter-wide hazard pulses and authored destructible tags; no structural simulation. **Deferred 2026-09-25** (`deferred.md`)
- [C] Maze — *Utility / narrative*
- Power Word Stun — *Control*

**Level 9**

- Foresight — *Buff / debuff*
- [C] Shapechange — *Defense, Utility / narrative* — ⚠ Simplify: curated high-level forms and one swap per turn; exclude arbitrary monster catalog.
- ~~[C] True Polymorph~~ — *Control, Utility / narrative* — ⚠ Simplify: curated combat forms; permanent narrative transformations only at authored hooks. **Deferred 2026-09-25** (`deferred.md`)

---

## Approximations — as built (regenerated 2026-09-25, after the SRD check)

*From `content/srd/spells/*.json` (`"approximated": true`) and `game/locale/game.csv`. 10 of the 123 functioning spells are approximations. 8 keep their SRD names by Kathleen's approval of 2026-09-25 (the allow-list under the HARD RULE, `decisions_checklist.md` §1). Their descriptions say "(v1 ships a bounded version of this spell.)". The other 2 ship under new names. Why each one deviates is in its `note` fields and in `_design_docs/REVIEW_spell_names.md`. Banishment and Maze were rebuilt faithfully, and Dissonant Whispers and Dragon's Breath (both in SRD 5.2.1) were rebuilt from the SRD text, so none of the four is listed. The 7 deferred spells and Suggestion are no longer in the functioning set.*

| Tier | Lvl | SRD spell (id) | Ships as | Name |
|---|---|---|---|---|
| MUST | 1 | `find_familiar` | Find Familiar | SRD name, approved approximation |
| MUST | 3 | `fly` | Fly | SRD name, approved approximation |
| MUST | 3 | `slow` | Slow | SRD name, approved approximation |
| MUST | 5 | `wall_of_force` | Cube of Force | renamed |
| MUST | 7 | `teleport` | Waypoint | renamed |
| MUST | 8 | `dominate_monster` | Dominate Monster | SRD name, approved approximation |
| MUST | 9 | `wish` | Wish | SRD name, approved approximation |
| SHOULD | 3 | `gaseous_form` | Gaseous Form | SRD name, approved approximation |
| SHOULD | 4 | `polymorph` | Polymorph | SRD name, approved approximation |
| SHOULD | 9 | `shapechange` | Shapechange | SRD name, approved approximation |
