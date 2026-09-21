# v1 species roster — the SRD-based rebuild

*Internal design doc. Naming: the player-facing name is always **Lanorim**; internal use is either **Maps & Math Rocks** or Lanorim.*

Written 2026-09-21 from the class/species scope workbook, rewritten into current **5e-SRD terminology**
(the workbook's *Vigor / Nerve / Snag/Trouble / die-step* wording has been dropped; this uses HP, the
nat-1/nat-20 consequence pool, and standard d20 + ability mod + proficiency).

SRD 5.2.1 uses the term **species**. Note: Half-Elf, Half-Orc, and Aasimar are **not** in this SRD and
aren't assumed available. The SRD option is **Orc**, not Half-Orc.

## What v1 ships — 7 species

**Human, Elf, Dragonborn, Tiefling, Dwarf, Halfling, Orc.** All build on one data-driven character-
presentation pipeline (skin/hand material, scale, selection art), so once the pipeline exists, adding a
species is comparatively cheap. **v1 may ignore species for the mini** if the visual work isn't ready —
the mechanics ship regardless.

| Species | Size / Speed | SRD traits | v1 handling |
|---|---|---|---|
| **Human** | Sm/Med, 30 | Resourceful; Skillful; Versatile | Ship. Replace the *Versatile* Origin-feat grant with **one extra trained skill or one curated growth choice** (feats are deferred). *Resourceful* → one reroll/day or a small camp benefit. |
| **Elf** | Med, 30 (Wood 35) | Darkvision; Elven Lineage; Fey Ancestry; Keen Senses; Trance | Ship all three lineages (Drow, High, Wood) as data choices **if their granted spells are in the v1 catalog**. *Trance* → camp flavor / small rest benefit, not a separate time-sim. |
| **Dragonborn** | Med, 30 | Draconic Ancestry; Breath Weapon; Damage Resistance; Darkvision; Draconic Flight (L5) | Ship. **One breath template + a damage-type parameter**; ancestry is a cosmetic choice. *Draconic Flight* → a short tactical flight / ignore-terrain state on authored maps, not general 3D flight. |
| **Tiefling** | Sm/Med, 30 | Darkvision; Fiendish Legacy; Otherworldly Presence | Ship. Legacy spells reuse the existing spell catalog + resistance handling — **no separate species magic engine**. Launch with Infernal if the other legacies' spells aren't yet supported; add as data. |
| **Dwarf** | Med, 30 | Darkvision 120; Dwarven Resilience; Dwarven Toughness; Stonecunning | Ship (cheap once resistance + HP + perception hooks exist). *Stonecunning* → advantage / step-up on **authored** stone secrets and hazards, not a general tremorsense sim. |
| **Halfling** | Sm, 30 | Brave; Halfling Nimbleness; Luck; Naturally Stealthy | Ship, but **redesign *Luck*** so it doesn't erase the nat-1 consequence loop: once per rest, reroll one die showing a 1 **or** downgrade a consequence to a cosmetic one. |
| **Orc** | Med, 30 | Adrenaline Rush; Darkvision 120; Relentless Endurance | Ship. *Relentless Endurance* maps directly to the death/stay-up mechanic: at 0 HP, spend the use to stay up at a fixed small HP. Validates a broadly reusable "death-intercept" system. |

## Deferred species

| Species | Why deferred | If added later |
|---|---|---|
| **Gnome** (Forest / Rock) | Rock Gnome clockwork-device crafting would be a new subsystem for modest demand. | Forest Gnome is cheap after innate spells; Rock Gnome devices = a fixed 3-option item, **not** a crafting sandbox. |
| **Goliath** | Temporary Large form + six ancestry actions; distinct body/hand scale for modest demand. | Large Form = reach/STR/movement modifiers **without** changing the collision footprint unless size-state support already exists. |

## Notes

- The **cost knee is the visual pipeline, not the mechanics** — build one data-driven presentation
  pipeline; then adding a species is mostly cosmetic + trait data.
- Species that grant spells (Elf lineages, Tiefling legacies, Gnome) are only "free" once those specific
  spells are in the v1 spell catalog — check `v1_spell_list.md` before promising a lineage.
