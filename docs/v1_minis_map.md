# v1 minis map — heroes, companions, monsters

*Internal design doc. Naming: the player-facing name is always **Lanorim**; internal use is either **Maps & Math Rocks** or Lanorim.*

The single reference for every mini in the game — what it represents, where the model comes from, and its
licence. Written 2026-09-21.

## The one principle

**A mini is a silhouette, not a statblock.** One body plan — recoloured, rescaled, reskinned — stands in
for many SRD monsters. So the game needs ~a dozen archetype minis, not hundreds. On-map minis are
**static** (no animation), like tabletop pieces; only the off-map companion may be animated.

Base look = **Quaternius** (single-creator on-map style). A few non-Quaternius fills are allowed **for
monsters only** (transient, on-map, reskinned) and get a light palette pass to sit with the set. The
companion is deliberately its own visual lane and can differ freely.

---

## 1. Player-hero minis — 7 classes

Source: **Quaternius Ultimate Animated Character Pack** (owned). The fantasy subset maps onto the classes;
weapons come from the same pack's modular weapon set.

| Class | Hero mini | Note |
|---|---|---|
| Barbarian | Viking | — |
| Fighter | Knight | — |
| Paladin | Knight (Golden) | armoured/holy variant |
| Rogue | Ninja or Pirate | — |
| Mage | Wizard | — |
| Cleric | Knight (Golden) or a robed reskin | no dedicated priest model — reskin |
| Druid | Witch or Elf reskin | no dedicated druid model — reskin |

Cleric and Druid are the only two without an exact model; both are covered by a reskin/recolour.

## 2. Companion minis — 5 voices across 7 classes

The companion is a **live off-map token**, distinct from the on-map minis, so it can look different and may
be animated. Voices: Barbarian + Fighter share one, Cleric + Paladin share one, Rogue/Mage/Druid each their
own.

| Companion | Mini | Source | Status |
|---|---|---|---|
| Barbarian + Fighter — bonded wolf | Wolf | Quaternius Ultimate Animated Animals | **owned** |
| Druid — borrowed-shape spirit | Fox / beast | Quaternius Ultimate Animated Animals | **owned** |
| Mage — bound imp | Imp | Quaternius Bestiary (standard tier) | **owned** |
| Cleric + Paladin — saint's fragment | abstract prop (relic/gem/wisp) | Quaternius props / a simple effect | flexible |
| Rogue — gossiping raven | no bird in any owned pack | — | **open** — re-theme, or a future purchase |

**Future option (not now):** for characterful companions, **Downrain DC** (downraindc3d.itch.io) sells
individual low-poly *animated* fantasy creatures (Bat, Dragon, Demon, etc.), a few dollars each. Because
companions are a separate visual lane, a distinct Downrain style is fine — and a companion is the one place
animation is worth paying for. Check each asset's licence at purchase and log it. **For now we go with what
we have** (Quaternius animals + imp).

## 3. Monster / enemy minis — archetype → SRD → source

Each archetype mini fronts many SRD statblocks by recolour/rescale/reskin.

| Archetype | Fronts (SRD examples) | Source | Status |
|---|---|---|---|
| Medium humanoid | bandit, guard, cultist, orc, hobgoblin, enemy mage/priest, noble | Character Pack (Knight/Soldier/Ninja/Pirate/Viking/Wizard/Witch) + hero models | **owned** |
| Small humanoid | goblin, kobold | Character Pack (Goblin) | **owned** |
| Skeleton | skeleton + undead minions | Bestiary | **owned** |
| Zombie / ghoul | zombie, ghoul, ghast, wight | Character Pack (Zombie) | **owned** |
| Big brute | ogre, troll, hill/stone giant (scale), ettin, minotaur (+horns) | Bestiary (Ogre) | **owned** |
| Small fiend | imp, quasit + mid-tier demon/death-knight | Bestiary | **owned** |
| Quadruped beast | wolf, dire wolf, bear, boar, big cat | Ultimate Animated Animals | **owned** |
| Vermin | giant rat, snake, giant frog | Easy Animated Enemy Pack | **owned** |
| Giant spider | giant spider, phase spider (reskin) | Easy Animated Enemy Pack | **owned** |
| Dragon | every chromatic/metallic dragon (recolour), wyvern/drake | Dragon Bodyparts Bundle (build) — free drop-in fallback: bocdagla "Low Poly Dragon" (CC-BY) | **owned (kit)** |
| Owlbear / drakes / hybrid beasts | owlbear, drake, chimera, odd beasts | Dragon Bodyparts Bundle (kitbash) | **owned (kit)** |
| Ooze / slime | gray ooze, ochre jelly, black pudding, gelatinous cube | Stylized Nature kit — recoloured pebble/rock (scaled) or mushroom cap + a translucent glossy "slime" material; gelatinous cube = a translucent cube | **owned (reskin)** |
| Elemental / golem / gargoyle | animated armour, elementals, gargoyle | reuse (empty knight) / defer | defer |

**Don't need at all:** the iconic aberrations (beholder, mind flayer, displacer beast, umber hulk…) are
WotC **Product Identity** carved out of the SRD — not usable, so no silhouette required.

## The Dragon Bodyparts Bundle is a beast-&-draconic *builder*

The nimbuspawtales kit is untextured modular low-poly parts (6 heads incl. beaky, legs, necks, bodies,
tails, wings, a skeleton). Because minis are static, using it is just **assemble + apply flat-colour
materials in the Quaternius palette + export GLB** — no rig/animation work. From it you can build:

- **Dragons** — the whole chromatic/metallic roster by combination + recolour
- **Owlbear** — beaky head + bulky body, no wings
- **Quadruped beasts / drakes / basilisks / gryphon-hybrids** — vary heads/wings/legs

Its boundary: it's a four-legged / winged / beaky *body-plan* builder. It can't make an ooze, a spider, or a
humanoid — those come from the other sources above. Untextured is a feature here: you colour it to match
Quaternius rather than fighting someone else's textures.

## Status — v1 monster minis: COMPLETE

Every monster archetype is sourced from **owned** assets (heroes, companions, humanoids, undead, brutes,
fiends, beasts, vermin/spider, dragons from the kit, and the ooze as a reskinned rock). **No monster
purchase is required for v1.**

Minor / optional leftovers:

- **Rogue's raven** (companion, not a monster) — no owned bird; re-theme, or a small future purchase (e.g. Downrain DC).
- **Full Bestiary (7)** — an optional cosmetic upgrade over the standard Imp+Puglin tier; not needed.
- **Ooze** is a reskin (rock + translucent "slime" material), not a download.

## Licences at a glance

- **Quaternius packs** (Character, Animals, Easy Enemy, Bestiary, environments, items) — Quaternius Asset
  License: commercial OK, no credit required, no reselling the raw assets.
- **Dragon Bodyparts Bundle** (nimbuspawtales) — free for commercial & non-commercial use, may ship in
  games; no reselling/repackaging the raw assets; credit appreciated, not required.
- **bocdagla "Low Poly Dragon"** (if used) — CC-BY: commercial OK, **credit required**.
- **Downrain DC** (future companions) — verify per asset at purchase.

Full provenance/recovery detail lives in `../THIRD_PARTY.md`.
