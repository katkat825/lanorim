# Decisions checklist — the SRD-based rebuild

**Written 2026-09-21, updated same day** after the docs moved here and the big SRD-alignment pass.
The running list of what still needs deciding. Work through it, cross things off, add more as you go.

**The framing that scopes this:** the **SRD 5.2.1 is the default** (`../THIRD_PARTY.md`, SRD section). Anything
you don't explicitly change *is* the SRD, so most items are "keep SRD, or make it a delta?" Every delta
is something you then build, balance, and document — choose them on purpose. **This is your delta
tracker.** And per §6 below, you're fine *simplifying* SRD rules where they're expensive.

Legend: **[OPEN]** to decide · **[DECIDED]** (where) · **[DEFER]** post-v1.

---

## Other
- **[OPEN] combat leveling** - need a way to account for leveling in the combat engine! just because I plan a campaign for them to follow the possible side quests in line with the main quests doesn't mean it'll happen. need to think that through.

## 1. Rules — deltas from the SRD

- **[DECIDED] Ability scores** — 5e SRD point-buy (`character_sheet_decisions.md`). (SRD is 27-point, 8–15
  before racial bumps, cap 20 — that's the default unless you say otherwise.)
- **[DECIDED] Proficiency bonus** — SRD scaling (+2→+6). It's trivial to implement (a one-line formula),
  so no reason for the old flat "+3." Skills add ability mod + proficiency (if trained), SRD-style.
- **[DECIDED] Skill list** — 5e SRD, all 18 (`character_sheet_decisions.md`).
- **[DECIDED] Skill-check DCs** — 5e SRD DC ladder, or a campaign custom number (`updated_decisions.md`).
- **[DECIDED] nat-1 / nat-20 consequence pool** on checks — a keeper delta (`updated_decisions.md`).
- **[DECIDED] Action economy** — **2 actions + 1 bonus action + 1 reaction** (intentional solo delta,
  `character_sheet_decisions.md`). *Sub-decision left:* whether to keep SRD's
  bonus-action-spell restriction (cast a bonus-action spell → your action spell must be a cantrip). I'd
  drop it for simplicity.
    drop for simplicity
- **[DECIDED] Death save** — single d20 ≥ 10, no mods, intentional solo delta.
- **[DECIDED] Attack & damage, initiative** — d20 + mods vs. AC; damage = weapon die + mod;
  initiative d20 + Dex. Flag if you want a delta.
- **[DECIDED] Ability Score Improvements** — SRD grants an ASI/feat at set levels; feats are deferred, so at
  those levels: a straight +2 to spend. 
- **[DECIDED] Saving throws** — six SRD saves + class proficiencies.
- **[DECIDED] Critical hits in combat** — double the damage dice plus the standard pool of consequences.
- **[DECIDED] Armor Class** — derived from equipped armor + Dex (SRD, shields) 
- **[DECIDED] Movement & grid scale** — SRD
- **[DECIDED] Currency** — Confirmed gold-only.
- **[DEFERRED] Damage types & resistances** — full SRD, tagged-but-simple, or flatten? (see §6) deferred or flatten
- **[DECIDED] Conditions** — v1 subset: prone, poisoned, stunned, frightened, restrained, grappled — core effects only; exhaustion's 6-level ladder deferred (decided in §6).
- **[DECIDED] Rest** — pin exact recovery to SRD (long rest = half hit dice + all slots; short rest spends hit dice) unless you simplify. except long rest = full hp recovery
- **[DECIDED, one part open] Spellcasting** — SRD spell lists + effects + concentration **[DECIDED]**;
  **flat known/equipped model [DECIDED]** — the spells on your sheet are what you can cast: no 5e slot
  tables, no daily preparation, no "Channeling" (that word is retired). *Which spells FUNCTION in v1* is
  settled (`v1_spell_list.md`). **Cantrips at-will [DECIDED].** **Leveled spells = point pool / mana [DECIDED]** — one pool, each
  spell costs points by its level, upcasting = spend more points, pool refills on rest; no 5e slot grid.
  (Ritual/upcasting depth: §6.)
- **[DEFER] Feats · Multiclassing · Encumbrance.**

## 2. Content scope for v1

- **[DECIDED] Races/ancestries** — **7 v1 species: Human, Elf, Dragonborn, Tiefling, Dwarf, Halfling,
  Orc** (`v1_species_roster.md`); Gnome/Goliath deferred. Light to implement; minis reusable; v1 may
  ignore species for the mini. The cost knee is the visual pipeline, not the mechanics.
- **[DECIDED, approach] Backgrounds** — SRD; light (skills/flavor).
- **[DECIDED, approach] Monsters** — full SRD pool + custom per campaign. Statblocks are
  cheap; *special abilities are the cost* (§6).
- **[DECIDED] Level range & leveling** — milestone leveling, up to level 20; XP deferred (allowing both,
  campaign-chosen, is cheap and a fine fast-follow).
- **[DECIDED] Classes & subclasses** — **7 v1 classes: Barbarian, Fighter, Rogue, Mage, Cleric,
  Paladin, Druid**, one SRD subclass each (`v1_class_roster.md`). **Mage is merged** (Wizard + Warlock +
  Sorcerer themes in one class). **Fighter shares Barbarian's companion; Paladin (the cheap 7th, a
  Cleric + Fighter hybrid) shares Cleric's** — no new dialogue lanes. Bard/Ranger/Sorcerer/Monk deferred
  or route-covered (Ranger was the next cheap candidate, intentionally held). Extra Attack isn't ported
  literally (hero has 2 actions); Wild Shape = 3–5 curated forms.
- **[DECIDED] Spells** — full ~339 SRD list ships as reference cards (all CC-BY, legal); a **131-spell
  subset (62 MUST + 69 SHOULD) fully FUNCTIONS in v1**, composed from effect primitives
  (`v1_spell_list.md`). Only useable spells exist as cards; ~8 high-cost spells ship as bounded
  approximations (flagged in the list).
- **[DECIDED] Starting gear & loot tables** — per class.
- **[DECIDED] Characters per campaign / save slots** — carry the old "5 per campaign"

## 3. The video-game glue (SRD is silent)

- **[DECIDED — corrected] The "GM."** The diegetic DM is **kept**: the figure behind the GM screen is the
  narrator who tells you the story, and campaigns can have the "DM" make hidden rolls — e.g. roll for
  whether a random encounter happens and which one. (I earlier mis-said this was
  removed — what was removed is the *no-menus/hands/room* presentation, not the DM-as-narrator.) *Builds
  needed:* random-encounter tables (weighted table + trigger) and the hidden-roll surface.
- **[OPEN] Combat interaction model — how faithful to 5e tactics?** Opportunity attacks, cover, LoS,
  flanking, AoE templates, targeting/range, and *how the player issues an action*. Biggest scope driver;
  also a cost hotspot (§6). suggest a simplified version for v1
- **[OPEN] Enemy AI** — scripted per-monster vs. generic "approach + attack" with tags. How smart, v1? standard enemy ai?
- **[DECIDED] Companion function** — the companion is a live **off-map** table thing (its own token,
  distinct from the on-map minis), and like everything else it's a **Quaternius** creature: Wolf for
  Barbarian + Fighter, the Imp you own for Mage, Fox/beast for Druid; Cleric + Paladin's "saint's
  fragment" is an abstract prop; **Rogue's raven has no Quaternius match — open sourcing task** (a
  Quaternius-style bird, a re-theme, or a commission). Same hints/flavor as before; the written voice
  carries identity. **5 companion voices across 7 classes** (Fighter shares Barbarian, Paladin shares
  Cleric). Details in `v1_class_roster.md`.
- **[DECIDED] Save model** — reload-on-death. autosave on events plus manual saves, save all if feasible, otherwise last 10. 
- **[OPEN] Character-creation flow** — guided vs. freeform; order of picks. [DECIDED] whatever's easiest
- **[OPEN] Encounter & map authoring format** — how a campaign places monsters and lays out a fight.
- **[DECIDED] Dialog presentation** — popup with Continue button.
- **[DECIDED-ish] Tutorial / onboarding.** - tutorial campaigns. ask player how familiar they are with ttrpg (beginner, intermediate, advanced) and select related tutorial campaign. tutorial campaigns are replayable
- **[OPEN] Fleeing combat** — an option? [DECIDED-ish] whatever is more expected and is easier

## 4. Presentation & art

- **[DECIDED] Art cohesion** — **Quaternius** low-poly 3D is the game's look (everything — dungeon, hero, monsters, companions, 3D UI); 2D art only ever as framed content
  on a prop (GM-screen panel bent with the folds, or small accents), never environment/UI; all 2D from a
  single coherent source.
- **[DECIDED] 3D pack cohesion — two bases: Quaternius + KayKit.** Characters, dungeon and
  other environments (village, wilderness, sci-fi), monsters, and animals/companions all come from
  Quaternius — one style family, one licence (QAL), one relationship for any future commissions. Within
  Quaternius, keep to one consistent sub-style (don't mix its realistic and "cute" lines). **KayKit is a second base source (CC0 — even more permissive than
  the QAL): kitbashed and mixed with Quaternius, unified by the one-palette bake + painted-miniature shader;
  its Skeletons also broaden the enemy roster.** **Monsters are the one thin spot:** the only style-matching, TTRPG-recognizable
  Quaternius monsters are the **Bestiary** (7 — werewolf, ogre, goblin, skeleton, imp, demon, death
  knight); the other Quaternius monster packs are cutesy and off-style (yeti, cactus, panda), so they're
  rejected. v1 enemies = the Bestiary + **reused character-pack humanoids** (goblin, zombie,
  bandit/ninja/pirate/soldier + cultist reskins) + animal-pack beasts, stretched across many statblocks by
  mini-reuse. **Dragons and exotic monsters (vermin, oozes, elementals, trolls) are a known gap →
  commission** (or a style-matched paid pack) later.
- **[DECIDED] Table layout** — grid map 3/4, lifting dice tray, GM screen, companion, help button.
- **[DECIDED] Audio** — carry "text before voice, no voiced words".
- **[DEFERRED] Audio** — music, atmospheric sound effects.
- **[DECIDED] Accessibility** — explicitly in v1 and at the highest depth we feasibly can.

## 5. Product

- **[DECIDED] Monetization** — paid game, with MTX allowed-for as a possible future addition, not planned.
- **[DECIDED] Legal / ruleset basis** — SRD Option B + attribution (`../THIRD_PARTY.md`).
- **[DECIDED] Workshop in v1**
- **[DECIDED] Content rating / mature-content stance** (base game) is not mature-content. workshop mature content allowed
- **[DECIDED-ish] Platform(s)** — PC/Steam first. how feasible is it to have this on other platforms?
- **[DECIDED] Naming** — player-facing name is **always Lanorim**; internal/dev use is either **Maps &
  Math Rocks** or Lanorim. (Workbook/xlsx filenames keep "maps_math_rocks" — fine, it's the internal name.)

## 6. SRD cost hotspots — expensive to build; simplify freely

You said you're fine simplifying SRD rules, so here are the parts that cost the most time/tokens, worst
first, each with the cheap way out. **The recurring trap: "have all of X" is cheap as *reference data*
and expensive as *working mechanics*.** Decide, per hotspot, how faithful v1 is.

- **[DECIDED — approach] Spell EFFECTS (the #1 cost).** ~300+ SRD spells, each a unique mechanical effect (damage +
  type, saves, conditions, areas, durations, upcasting). Cards are cheap; *making each one work* is huge.
  **Cheap path:** build a small library of spell-effect *primitives* (damage, heal, apply-condition,
  buff/debuff, move, area, utility-narrative) and compose spells from them; ship a curated **v1 subset
  that fully functions**, list the rest as reference cards, and treat pure-utility spells as
  narrative/campaign-handled rather than coded. **Specced:** the primitive foundation + 131-spell v1 subset are in `v1_spell_list.md`.
- **[DECIDED] Class features & subclasses.** Each class is a progression of features (rage, sneak attack,
  spellcasting, ki, wild shape, channel divinity…), and subclasses multiply it. **Cheap path:** v1 ships
  a **small set of classes**, implement their core features, **skip subclasses for v1** (or one each),
  and cut/downgrade the most complex features (Wild Shape is a whole subsystem — v1 = 3–5 curated forms). **Specced:** 7 classes, one SRD subclass each, in `v1_class_roster.md`.
- **[DEFERRED] Per-monster special abilities.** Base statblocks are uniform and cheap; the cost is unique
  abilities (breath weapons, multiattack, recharge, regeneration, spellcasting monsters, legendary/lair
  actions).
- **[DECIDED]** a few ability primitives (multiattack, save-or-condition, recharge, resistance);
- **[DECIDED] skip legendary/lair actions in v1**; spellcaster monsters reuse the spell system.
- **[DECIDED] Grid tactics — cover, line-of-sight, flanking, AoE templates.** v1 does simple range checks and radius AoE; 
- **[DEFERRED] cover/flanking/LoS** (or add later).
- **[DEFERRED] Reactions & interrupts.** interrupt reactions (counterspell,
  shield, hellish rebuke) are fiddly timing. 
- **[DECIDED]:** v1 = opportunity attack only; other
  reactions are author-scripted or deferred.
- **[DEFERRED] Concentration damage-saves.** SRD makes you roll a CON save to keep concentration when hit.
- **[DECIDED]** v1 concentration is binary (held until you end it or are downed); skip the saves.
- **[DECIDED] Spell preparation rules.** one flat "known/equipped spells" model for everyone (your sheet already leans this).
- **[DEFERRED] Damage types × resistances/vulnerabilities/immunities.** A matrix on every attack and monster.
- **[DECIDED]** tag damage types but treat resistance as a simple ×0.5 / ×2 / ×0, or defer resistances.
- **[DEFERRED] Full conditions + interactions.** ~15 conditions, each with several rule effects that interact.
- **[DECIDED]** a v1 subset (prone, poisoned, stunned, frightened, restrained, grappled) with core
  effects only; defer exhaustion's 6-level ladder.
- **[DECIDED] Data-entry volume.** Minimal set of data. Even lifting verbatim, entering hundreds of spells/monsters/features as
  structured data is real time (AI-assistable, but large). Scoping the v1 subsets above is what keeps
  this sane.

## 7. Already decided (pointers)

`updated_decisions.md` (UI/table, dialog, rest, campaign flow, dice) · `character_sheet_decisions.md`
(sheet + abilities/skills/spells = SRD, action economy) · `inventory_decisions.md` (lean v1 inventory:
flat 40 slots, stacking, buy/sell, discard flow, equip, class/level gating) · `deferred.md` (the parked
pile) · `../THIRD_PARTY.md` (SRD Option B + asset provenance) · **`v1_class_roster.md`** (7
classes) · **`v1_species_roster.md`** (7 species) · **`v1_spell_list.md`** (131 functioning + full-list
cards).

## 8. Open questions & running additions

Dialog popup vs. bottom-bar · Continue button · fleeing combat · horse/cart inventory separate vs.
additive (currently deferred; v1 is flat 40 slots) · global inventory / lost-and-found.

*Add anything here as it comes up. Half-formed is fine.*
