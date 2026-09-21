# v1 spell list — the SRD-based rebuild

*Internal design doc. Naming: the player-facing name is always **Lanorim**; internal use is either **Maps & Math Rocks** or Lanorim.*

Written 2026-09-21 from the spell scope workbook. All spell names and text come from **SRD 5.2.1
(CC-BY-4.0)** and are legal to ship.

## The plan in one paragraph

The full ~339 SRD spells all ship as **reference cards** (cheap — just data). A curated **131-spell subset
fully FUNCTIONS in v1** (**62 MUST + 69 SHOULD**), built by composing a small library of effect
**primitives** (targeting, typed damage, healing/HP, timed effects + concentration, conditions,
areas/multi-target, persistent zones, movement, visibility/light, reactions/countermagic, barriers,
illusion/info). 106 of the 131 come "for free" once those ~12 foundation primitives exist; only the
handful flagged **⚠ simplify** below need a bounded, authored approximation instead of a general system.
Pure-utility spells outside this set are handled as narrative / campaign content rather than coded.

**Resource model reminder:** these are the spells a caster can *know/equip*. **Cantrips are at-will; leveled spells use a point pool (mana)** — cost by spell level, upcasting
spends more, refills on rest. See `decisions_checklist.md` §1.

**[C]** = requires concentration. **⚠** = ships as a bounded approximation, not a general subsystem.



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
