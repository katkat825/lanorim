# v1 build checklist — what actually gets built

*Internal design doc. Naming: player-facing is always **Lanorim**; internal is either **Maps & Math Rocks** or Lanorim.*

The companion to `decisions_checklist.md`. That doc settles **what the game is**; this one lists **what you
build to ship it, end to end** — every system and feature, grouped, so the whole scope is visible and you
can tell when v1 is *done*. Written 2026-09-21.

**Legend:** `[ ]` to build · `[~]` in progress · `[x]` done · `[HARVEST]` reuse/adapt old game code ·
`[DEFER]` explicitly post-v1.

## Progress snapshot — 2026-09-24
*Marks reflect code + unit tests present in the repo (a file/test survey — not a play-through, and not a fresh `dotnet test` run this session; the harvest report records the last green run). `[x]` = built and unit-tested at the code layer; `[~]` = partial, or built but not yet wired to UI / play-verified.*

- **Built + unit-tested (rules & data engine):** the content **schema** + campaign **package format/loader**, **save/load**, **localization**; **d20 resolution** + **abilities/proficiency**, **attack/damage/crits**, **HP + death save**, **action economy + turn loop**, **grid movement**; the **spell-primitive engine** (13 primitives; the 62 MUST spells compose); the **two-mode spell resource** — slots + points, chosen at creation, built 2026-09-24; **inventory**; the **branching dialogue runtime** (Yarn + the beats "shared spine"). ~350 unit tests across core + content.
- **Built at the code layer, needs eyes (the physical table):** dice physics + your modeled tray, board grid, minis, audio, camera, the painted-mini shader path — tuning and the dice-fairness confirmation are Godot-side.
- **Partial:** conditions, damage types/resistance, rest, milestone leveling, concentration, the full 131-spell + monster/class/species data; enemy AI; the diegetic GM narrator + narrative flow.
- **Not started — the frontier is UI:** **no screen UI exists yet** (start-game/menu, character sheet, inventory/merchant, dialog popup, spell cards, HUD, settings, creation flow, the Kenney theme/fonts/icons). Also: the **map builder + Workshop**, **random-encounter tables**, the just-decided **line/cone shapes**, **reaction/interrupt spells** and **companion alias**, all **content** (campaigns/tutorials), **demo**, **release**.

---

## 0. The finish line — what "v1 done" means

v1 is shippable when a player can, start to finish:

1. Launch the game, read the campaign book (menu), pick a **class + species**, and make a character.
2. Load a campaign and be led by the **GM narrator** through a **branching story** (dialogue, skill
   checks that branch out and return).
3. Fight **turn-based combats** on a **grid map** with the simplified v1 rules, using the 2/1/1 action
   economy, spells, and items.
4. **Level up** (milestone), manage **inventory + a merchant**, and reach the campaign's ending.
5. Do all of that with **autosave/reload**, the **table presentation** (dice, GM screen, companion), a
   coherent **UI**, and **accessibility** on.
6. Ship with the **v1 campaign set** — 3 tutorial campaigns, a handful of one-shots, a couple of
   level-block campaigns, and one **epic level 1–20** campaign — plus the **map builder + Workshop** so
   others can build and share their own.

Anything not on that list is **out of v1** and lives in `deferred.md`: XP leveling, feats, multiclass,
cover/flanking/LoS, legendary/lair actions, concentration saves, music/ambient SFX, exotic monsters
(dragons/oozes/owlbears aside), container inventory, MTX. When the six lines above are true, **building is
done.**

**Earlier than that — the demo.** A free **Steam demo** (1 tutorial + 1 one-shot) ships *before* full v1: it's the portfolio piece and the wishlist/Next-Fest engine. See `v1_build_order.md` → Phase D.

---

## 1. Foundation

- `[x]` **Godot project + C# setup** — project, folder layout, palette-unify/painted-miniature shader path.
- `[x]` **Content data model** — the schema for classes, species, spells, monsters, items, conditions as
  *reference data* (the cheap layer). Everything else reads from this.
- `[x]` **Content / campaign package format + loader** — data-driven package the game loads (paradigm-agnostic; reusable from the old game).
- `[x]` **Save/load** — autosave on events + manual saves (save all if feasible, else last 10), reload-on-death.
- `[x]` **Localization / string keys** — so all player text is keyed.

## 2. Rules engine

- `[x]` **d20 resolution** — roll + mods vs DC/AC, advantage/disadvantage.
- `[x]` **Ability scores, modifiers, proficiency bonus** (+2→+6).
- `[~]` **Skill checks** — 18 skills, DC ladder or campaign custom, **nat-1/nat-20 consequence pool**.
- `[~]` **Saving throws** — six saves + class proficiencies.
- `[x]` **Attack + damage + crits** — weapon die + mod; crit = double + consequence pool.
- `[x]` **HP / damage / healing / death save** — single d20 ≥ 10 death save.
- `[x]` **Action economy + turn structure** — 2 actions + 1 bonus + 1 reaction; class extras. *(base built + tested; the Extra-Attack/Action-Surge action grants from the 2026-09-23 correction still to add in class data.)*
- `[~]` **Conditions** — v1 subset (prone, poisoned, stunned, frightened, restrained, grappled), core effects.
- `[~]` **Damage types + simple resistance** — tag types; ×0.5 / ×2 / ×0.
- `[~]` **Rest** — short (spend hit dice) / long (full HP + mana).
- `[~]` **Leveling** — milestone, to level 20; ASI level = +2 to spend.

## 3. Spellcasting

- `[x]` **Spell-effect primitive library** — damage, heal, apply-condition, buff/debuff, move, area,
  utility-narrative — the composition engine (the #1 cost; see `v1_spell_list.md`).
- `[x]` **Spell resource — two modes, chosen at creation** — **(A) spell slots** (full/half tables) and **(B) spell points** (fixed cost per level + 6th+-once-per-rest cap); cantrips at-will in both; refill on rest; one `ISpellResource` abstraction over the shared cast-at-level engine. *(2026-09-23: was mana-only. Built + unit-tested 2026-09-24 — slots + points + 6th+ cap; the choose-at-creation UI is §10.)*
- `[~]` **Concentration** — binary (held until ended or downed).
- `[~]` **131 functioning spells** composed from primitives; bounded approximations — count re-pinned after line/cone + reactions land.
- `[ ]` **Spell cards UI** — text cards; full ~339 SRD list as reference cards.

## 4. Classes, species, progression

- `[~]` **7 classes + core features** — Barbarian, Fighter, Rogue, Mage (merged), Cleric, Paladin, Druid (`v1_class_roster.md`); one subclass each.
- `[ ]` **Druid Wild Shape** — 3–5 curated form cards (highest-cost feature).
- `[~]` **7 species + traits** — Human, Elf, Dragonborn, Tiefling, Dwarf, Halfling, Orc (`v1_species_roster.md`).
- `[ ]` **Backgrounds** — SRD, light (skills/flavor).
- `[~]` **Starting gear + loot tables** — per class.

## 5. Character sheet & inventory

- `[ ]` **Character sheet UI** — identity, basics, abilities, skills, actions/bonus/reactions, weapons, spells, special (`character_sheet_decisions.md`).
- `[x]` **Inventory system** — flat 40 slots, stacking, equip, buy/sell, discard-to-make-room, class/level gating (`inventory_decisions.md`).
- `[ ]` **Merchant** — buy/sell, unlimited gold default, buy-prevention when full.

## 6. Combat

- `[x]` **Turn-based loop** — initiative, turn order, action economy.
- `[~]` **Grid movement + range checks**; **radius, line & cone AoE templates**. *(radius/burst built + tested; line/cone still pending — see cc task.)*
- `[~]` **Reaction / interrupt system** — a creature spends its 1 reaction to interrupt: opportunity attacks *plus* reaction spells (Shield resolves before the hit lands, Counterspell on an enemy cast). *(2026-09-23: promoted from "opportunity attacks only". The opportunity-attack reaction is built + tested; the interrupt window + Shield/Counterspell are the pending part.)*
- `[ ]` **Combat interaction UX** — how the player issues an action (menu/targeting feel). *Design during this build — the one big undesigned piece.*
- `[~]` **Enemy AI** — basic approach + attack with tags.
- `[~]` **Monster statblocks + ability primitives** — multiattack, save-or-condition, recharge, resistance; spellcaster monsters reuse the spell system; skip legendary/lair.
- `[ ]` **Fleeing combat** — whichever is more expected + easier.

## 7. The table & presentation

- `[~]` **3D table scene** — grid map (3/4 screen), lifting dice tray, GM screen, companion, help button.
- `[x]` **Dice physics + roll + sound** — reuse the old dice system + the moved sound pool.
- `[~]` **GM screen** — first-party GM-screen models made (`game/models/gm_screen/`); wire **blank** for now, per-campaign backgrounds later.
- `[ ]` **Companion mini** — Quaternius creature, idle presence + hints (off-map token).
- `[~]` **Mini rendering on grid** — heroes + monsters, palette-unified.
- `[~]` **Camera + lighting** for the table.

## 8. Narrative & GM

- `[~]` **Diegetic GM narrator** — the figure behind the screen delivers the story.
- `[x]` **Branching dialogue runtime** — YarnSpinner (already chosen); popup + Continue button. *(Runtime + the beats "shared spine" built + tested; the popup UI is §10.)*
- `[ ]` **Generic companion speaker (`companion` alias)** — a reserved speaker the presentation resolves to the player's actual companion, so a base-game campaign can write one in-narrative line "said by any companion"; a companion-specific line overrides it. Follows the existing `dm` reserved-speaker precedent. *(Base-game only; not needed in Workshop.)*
- `[~]` **Narrative flow** — branch-out/return for skill checks and combat.
- `[ ]` **Random-encounter tables + hidden GM rolls** — weighted table + trigger + hidden-roll surface.

## 9. Campaign format & Workshop

- `[~]` **Campaign format** — scenes, encounters, maps, NPCs, dialogue, tables as data.
- `[ ]` **MAP BUILDER (wanted early).** In-engine visual editor: shows the grid + a palette of the
  Quaternius/asset library; **click/drag to place** floor tiles, walls, props, and monster/spawn markers on
  squares; save as a campaign map file. Purpose: you *and* Workshop authors build maps **without touching
  Godot or hand-authoring map data**. Keep it simple — paint tiles, place props, set spawns, no scripting.
  Build a usable version **early** (it's also your own authoring tool) even before Workshop upload exists.
- `[ ]` **Steam Workshop integration** — upload / subscribe / load user campaigns.
- `[ ]` **Content policy** — authors may not upload WotC Product Identity or copyrighted adventures; asset-redistribution rules (Admurin/Quaternius: reference-by-id, don't repackage raw).

## 10. Game-wide UI / UX

- `[ ]` **Main menu = campaign book** (table of contents).
- `[ ]` **Character-creation flow** — whatever's easiest; guided.
- `[ ]` **Settings.**
- `[ ]` **Tutorial / onboarding** — ask beginner/intermediate/advanced → matching replayable tutorial campaign.
- `[ ]` **UI theme** — Kenney UI kit (panels/buttons/borders); **icons** game-icons.net (CC-BY, credit authors); **fonts** (EB Garamond cards / Alegreya SC UI / storytelling font).
- `[~]` **Accessibility** — in v1, at the highest depth feasible.

## 11. Audio

- `[x]` **Dice + mini-move + table SFX** — the moved Freesound pool.
- `[ ]` **UI SFX** — Kenney interface sounds (trial fit).
- `[DEFER]` **Music + ambient SFX.**

## 12. Content to author (the actual v1 game)

- `[~]` **SRD data entry** — the 7 classes, 7 species, 131 spells, monster set, items as structured data (AI-assistable, but real volume).
- `[ ]` **3 tutorial campaigns** — beginner / intermediate / advanced, replayable.
- `[ ]` **A handful of one-shots** — self-contained short adventures.
- `[ ]` **A couple of level-block campaigns** — mid-length arcs over a level range.
- `[ ]` **One epic level 1–20 campaign** — the flagship.
- `[ ]` **Contracted campaigns** — some one-shots/blocks authored by friends under a short contract (assigns/licenses the content to you so it ships clean) + author attribution. *House policy: credit every creator regardless of licence.*
- `[ ]` **Demo content** — 1 tutorial + 1 one-shot packaged as the free demo (ships early; build order Phase D).

## 13. Release

- `[ ]` **Steam page + AI-content disclosure** (coding-AI exempt; provenance in ../THIRD_PARTY.md).
- `[ ]` **Legal review** — exact SRD CC-BY attribution wording + placement; store copy implies no affiliation.
- `[ ]` **Demo build + Steam demo App ID** — separate free app; integrated button or own page; disable achievements, shared-cloud saves, end-screen CTA, content survey. *Ships early, before full v1.*
- `[ ]` **Build / packaging** — PC/Steam first.

---

## Harvest from the old game (before writing fresh)

Worth pulling from `solo_ttrpg_game` rather than rebuilding: the **content/campaign package format + loader**,
**save architecture**, **localization/keys**, **dice physics + roll**, **loot tables**, and the **YarnSpinner
dialogue** wiring. The UI (the diegetic room/hands) is *not* harvested — that direction was abandoned. First
build task is deciding, per item above, harvest-vs-fresh.
