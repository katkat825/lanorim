# v1 build checklist — what actually gets built

*Internal design doc. Naming: player-facing is always **Lanorim**; internal is either **Maps & Math Rocks** or Lanorim.*

The companion to `decisions_checklist.md`. That doc settles **what the game is**; this one lists **what you
build to ship it, end to end** — every system and feature, grouped, so the whole scope is visible and you
can tell when v1 is *done*. Written 2026-09-21.

**Legend:** `[ ]` to build · `[~]` in progress · `[x]` done · `[HARVEST]` reuse/adapt old game code ·
`[DEFER]` explicitly post-v1.

## Progress snapshot — 2026-09-24, after the unattended run

*`dotnet test` green: **826** (core 258 + content 568). The whole game plays: launch → the campaign book →
creation → the table, where a campaign runs its story, checks, shop, fights on the board through the combat
HUD with the hero's dice on the tray, level-ups, rests, saves, death → reload, and its end —
`check-play.ps1` plays the sample test campaign that way headless. All 131 spells built (20 still
approximations). Every screen is wired and **needs Kathleen's eyes**. The run's log, questions and review
lists: `_design_docs/RUN_LOG_2026-09-24.md`. The paragraph below is the morning's snapshot, kept for the record.*

## Progress snapshot — 2026-09-24 morning (refreshed after the overnight Claude Code session)
*Marks reflect code + unit tests present in the working tree (a file/test survey — not a play-through, and not a fresh `dotnet test` run during this doc pass). Much of this is **uncommitted** as of this refresh — review with `git status` before committing. `[x]` = built and unit-tested at the code layer; `[~]` = partial, or built but not yet wired to UI / play-verified.*

- **Built + unit-tested (rules & data engine):** the content **schema** + campaign **package format/loader**, **save/load** (now including the full hero — class features, slots/points, a worn Wild Shape), **localization**; **d20 resolution** + **abilities/proficiency**, **attack/damage/crits**, **HP + death save**, **rest**, **concentration** (binary), **action economy + turn loop** (incl. Extra Attack / Action Surge / Cunning Action), **grid movement** + **radius, line, cone and cube templates**, the **reaction/interrupt window** (Shield, Counterspell, opportunity attacks), **fleeing**; the **spell-primitive engine** + **zones**; the **two-mode spell resource** (slots + points); **129 of the 131** v1 spells; **Wild Shape** (4 form cards); **inventory + merchant logic**; **loot tables**; **random-encounter tables + hidden GM rolls**; the **branching dialogue runtime** + **narrative flow** (`<<check>>`, `<<save>>`, `<<fight>>`, `<<roll>>`, `<<encounter>>`, `<<loot>>`, `<<give>>`, `<<gold>>`, `<<level>>`); the **`companion` speaker alias**; **demo-edition logic**. About **584 test methods** across core (206) + content (378), counted by `[Fact]`/`[Theory]`.
- **Logic built, UI not:** character-sheet view (`content/Sheet/SheetView.cs`), spell cards (`content/Spells/SpellCard.cs`), character creation (`content/Creation`), merchant. These are the models the Godot screens bind to.
- **Built at the code layer, needs eyes (the physical table):** dice physics + your modeled tray, board grid, minis, audio, camera, the painted-mini shader path — tuning and the dice-fairness confirmation are Godot-side. GM-screen models are imported but not yet placed in `table.tscn`.
- **Partial:** conditions, damage types/resistance, milestone leveling (ASI +2 is auto-spent; player choice still to do), class/species/monster data breadth, enemy AI, the diegetic GM narrator.
- **Not started — the frontier is UI:** **no screen UI exists yet** (menu, character sheet, inventory/merchant, dialog popup, spell cards, HUD, settings, creation flow). Fonts and a few Kenney panels are copied into `game/` but nothing uses them yet. Also: the **map builder UI + Workshop**, the **combat interaction UX**, all **content** (campaigns/tutorials), **release**.
- **Decisions waiting on Kathleen** are listed in `_design_docs/BUILD_FOR_CLAUDE_CODE.md` → *Build notes*.

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
- `[x]` **Action economy + turn structure** — 2 actions + 1 bonus + 1 reaction; class extras. *(Extra Attack = +1 action at 5 for Fighter/Barbarian/Paladin, Action Surge = +1 per rest, Cunning Action on the bonus action; guardrail of 4 actions a turn. Dash/Disengage/Hide exist as actions.)*
- `[x]` **Conditions** — v1 subset (prone, poisoned, stunned, frightened, restrained, grappled), core effects. *(2026-09-24: grown to what faithful spells need — blinded, charmed, deafened, incapacitated, invisible, paralyzed, petrified, and unconscious by a spell — with SRD 5.2.1 core effects, condition sources (charmed by whom) and immunities. Exhaustion stays deferred.)*
- `[~]` **Damage types + simple resistance** — tag types; ×0.5 / ×2 / ×0.
- `[x]` **Rest** — short (spend hit dice) / long (full HP + spell slots or points refilled).
- `[x]` **Leveling** — milestone, to level 20; ASI level = +2 to spend. *(Campaign `<<level N>>` raises the hero; features and HP arrive at their level. 2026-09-24: **the player chooses the ASI** — +2 to one or +1 to two, capped at 20 — as a pending improvement the level-up screen spends (`Hero.Improve`); creation above level 4 has an Improvements step; saves keep the choices. The level-up screen itself is §10/Tier 3.)*

## 3. Spellcasting

- `[x]` **Spell-effect primitive library** — damage, heal, apply-condition, buff/debuff, move, area,
  utility-narrative — the composition engine (the #1 cost; see `v1_spell_list.md`).
- `[x]` **Spell resource — two modes, chosen at creation** — **(A) spell slots** (full/half tables) and **(B) spell points** (fixed cost per level + 6th+-once-per-rest cap); cantrips at-will in both; refill on rest; one `ISpellResource` abstraction over the shared cast-at-level engine. *(2026-09-23: was mana-only. Built + unit-tested 2026-09-24 — slots + points + 6th+ cap; the choose-at-creation UI is §10.)*
- `[x]` **Concentration** — binary (held until ended or downed); dropping it lifts its conditions and zones.
- `[x]` **131 functioning spells** composed from primitives. *All 131 built (2026-09-24). Dissonant Whispers and Dragon's Breath are not in SRD 5.2.1 and ship as working spells under original names (Murmur of Dread, Wyrmbreath Boon). **20 approximations remain and ship renamed** (8 MUST, 12 SHOULD — down from 65), nearly all against decided constraints; 109 are the SRD spell under its SRD name. Both halves of the naming rule are tests. Review: `_design_docs/REVIEW_spell_names.md`.*
- `[~]` **Spell cards UI** — text cards; full ~339 SRD list as reference cards. *(Card logic built — `SpellCard.cs`; the Godot card is not.)*

## 4. Classes, species, progression

- `[~]` **7 classes + core features** — Barbarian, Fighter, Rogue, Mage (merged), Cleric, Paladin, Druid (`v1_class_roster.md`); one subclass each. *(2026-09-24: every SRD 5.2.1 feature to level 20, subclass included, is in the data (74 added); ones the engine plays have their trait, the rest are `narrate` with a note saying what's missing — weapon mastery above all. The Fighter's ASIs at 6 and 14 and the Rogue's at 10 are class data. Paladin still plays the 2014 Divine Smite rider and Druid keeps 2014's Land's Stride — flagged, not changed.)*
- `[~]` **Druid Wild Shape** — 3–5 curated form cards (highest-cost feature). *(Logic + 4 cards built: cat, riding horse, black bear, spider — one per role: scout, travel, combat, utility. Form models are yours.)*
- `[~]` **7 species + traits** — Human, Elf, Dragonborn, Tiefling, Dwarf, Halfling, Orc (`v1_species_roster.md`).
- `[~]` **Backgrounds** — SRD, light (skills/flavor). *(9 in data. ⚠ Verify against SRD 5.2.1 before release — see build notes.)*
- `[x]` **Starting gear + loot tables** — per class. *(Loot tables built + tested: weighted, nested, hidden rolls, never draws what the hero can't use, overflow into the discard flow.)*

## 5. Character sheet & inventory

- `[~]` **Character sheet UI** — *(logic built — `SheetView.cs`, plus Alignment; a short sheet card is wired from the pause menu 2026-09-24 — the full layout of `character_sheet_decisions.md` is not)* — identity, basics, abilities, skills, actions/bonus/reactions, weapons, spells, special (`character_sheet_decisions.md`).
- `[x]` **Inventory system** — flat 40 slots, stacking, equip, buy/sell, discard-to-make-room, class/level gating (`inventory_decisions.md`).
- `[x]` **Merchant** — buy/sell, unlimited gold default, buy-prevention when full. *(Logic built + tested; the pack/merchant card is wired 2026-09-24 — `<<shop id>>` in a story opens it. Needs Kathleen's eyes.)*

## 6. Combat

- `[x]` **Turn-based loop** — initiative, turn order, action economy.
- `[x]` **Grid movement + range checks**; **radius, line & cone AoE templates**. *(Plus cube and a placed square; four facings. Spells now check range and line of sight.)*
- `[x]` **Reaction / interrupt system** — a creature spends its 1 reaction to interrupt: opportunity attacks *plus* reaction spells (Shield resolves before the hit lands, Counterspell on an enemy cast). *(2026-09-23: promoted from "opportunity attacks only". Built + tested 2026-09-24: Hit / Cast / LeaveReach / Damaged windows; Shield and Counterspell faithful. Open: how the player is asked — see combat UX.)*
- `[~]` **Combat interaction UX** — how the player issues an action (menu/targeting feel). *(Designed 2026-09-24 — `docs/combat_ux.md`, player-facing `how_to_play_combat.md` — and built on `CombatSession`: HUD, hotkeys, greyed reasons, previews, reach and templates, the Ask card, paced enemy turns. Needs Kathleen's eyes and hands.)*
- `[~]` **Enemy AI** — basic approach + attack with tags.
- `[~]` **Monster statblocks + ability primitives** — multiattack, save-or-condition, recharge, resistance; spellcaster monsters reuse the spell system; skip legendary/lair.
- `[x]` **Fleeing combat** — whichever is more expected + easier. *(Built: step off an open edge of the map; provokes opportunity attacks unless you Disengage; a closed map can't be fled.)*

## 7. The table & presentation

- `[~]` **3D table scene** — grid map (3/4 screen), lifting dice tray, GM screen, companion, help button.
- `[x]` **Dice physics + roll + sound** — reuse the old dice system + the moved sound pool.
- `[x]` **GM screen** — first-party GM-screen models made (`game/models/gm_screen/`); wire **blank** for now, per-campaign backgrounds later. *(2026-09-24: `GmScreen` stands beyond the board's far edge whatever the map's size; a manifest's `gm_screen` picks cave / dead-forest / plains / snowy-mountains; the GM's hidden rolls rattle behind it. Its place and size need eyes.)*
- `[ ]` **Companion mini** — Quaternius creature, idle presence + hints (off-map token).
- `[~]` **Mini rendering on grid** — heroes + monsters, palette-unified. *(2026-09-24: a monster stands as its statblock's mini — goblin, bandit, guard, zombie, wolf, rat, spider so far (`MiniModels`); props stand on their squares.)*
- `[~]` **Camera + lighting** for the table.

## 8. Narrative & GM

- `[~]` **Diegetic GM narrator** — the figure behind the screen delivers the story. *(The dialogue card is wired; the figure is not.)*
- `[x]` **Branching dialogue runtime** — YarnSpinner (already chosen); popup + Continue button. *(Runtime + the beats "shared spine" built + tested; the popup is wired on the table 2026-09-24.)*
- `[x]` **Generic companion speaker (`companion` alias)** — a reserved speaker the presentation resolves to the player's actual companion, so a base-game campaign can write one in-narrative line "said by any companion"; a companion-specific line overrides it. Follows the existing `dm` reserved-speaker precedent. *(Base-game only; not needed in Workshop. Built + tested 2026-09-24.)*
- `[x]` **Narrative flow** — branch-out/return for skill checks and combat. *(Closed set of Yarn commands, settled by `Referee`; 12 tests.)*
- `[x]` **Random-encounter tables + hidden GM rolls** — weighted table + trigger + hidden-roll surface. *(`core/Tables` — `EncounterTable`, `GmScreen`.)*

## 9. Campaign format & Workshop

- `[~]` **Campaign format** — scenes, encounters, maps, NPCs, dialogue, tables as data. *(Encounter and loot tables, merchants, the GM screen and map props load from a pack; a whole campaign plays — `CampaignRun`, `campaigns/sample_millbrook`.)*
- `[~]` **MAP BUILDER (wanted early).** *(2026-09-24: the palette from data and the tool modes — `PropCatalogue`, `MapEditor` — on `MapDraft`; the Godot editor screen is not built.)* In-engine visual editor: shows the grid + a palette of the
  Quaternius/asset library; **click/drag to place** floor tiles, walls, props, and monster/spawn markers on
  squares; save as a campaign map file. Purpose: you *and* Workshop authors build maps **without touching
  Godot or hand-authoring map data**. Keep it simple — paint tiles, place props, set spawns, no scripting.
  Build a usable version **early** (it's also your own authoring tool) even before Workshop upload exists.
- `[ ]` **Steam Workshop integration** — upload / subscribe / load user campaigns.
- `[~]` **Content policy** — *(draft for you: `_design_docs/CONTENT_POLICY_DRAFT.md`)* — authors may not upload WotC Product Identity or copyrighted adventures; asset-redistribution rules (Admurin/Quaternius: reference-by-id, don't repackage raw).

## 10. Game-wide UI / UX

- `[x]` **Main menu = campaign book** (table of contents). *(2026-09-24 — `CampaignBook` + `game/Screens/Launch.cs`, the main scene: pages, five character slots each, Continue, test campaigns labelled at the back. Needs eyes.)*
- `[x]` **Character-creation flow** — whatever's easiest; guided. *(Logic + the screen, 2026-09-24 — `CreationScreen`: one page a step, Next greyed with the reason. Needs eyes.)*
- `[~]` **Settings.** *(Game half built and wired — `GameSettings` + `SettingsPanel`: enemy-turn speed, skip physical dice, camera follow, reaction policies, saved as JSON. The Access half (`game/Access/Adjustments`) has no page yet.)*
- `[~]` **Tutorial / onboarding** — ask beginner/intermediate/advanced → matching replayable tutorial campaign. *(Picker built — `TutorialPicker`: a campaign tagged `tutorial_beginner` etc. fills each level; the test campaigns sit under it. The tutorials themselves are content, not written.)*
- `[x]` **UI theme** — *(2026-09-24: `game/ui/lanorim_theme.tres`, the project default — Alegreya SC UI, EB Garamond cards, both narration candidates as variations. Icons not yet.)* — Kenney UI kit (panels/buttons/borders); **icons** game-icons.net (CC-BY, credit authors); **fonts** (EB Garamond cards / Alegreya SC UI / storytelling font).
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
- `[~]` **Demo build + Steam demo App ID** *(the edition logic is built — `Edition.cs`/`DemoCut`, `demo.json` picks the content; Steam side not)* — separate free app; integrated button or own page; disable achievements, shared-cloud saves, end-screen CTA, content survey. *Ships early, before full v1.*
- `[ ]` **Build / packaging** — PC/Steam first.

---

## Harvest from the old game (before writing fresh)

Worth pulling from `solo_ttrpg_game` rather than rebuilding: the **content/campaign package format + loader**,
**save architecture**, **localization/keys**, **dice physics + roll**, **loot tables**, and the **YarnSpinner
dialogue** wiring. The UI (the diegetic room/hands) is *not* harvested — that direction was abandoned. First
build task is deciding, per item above, harvest-vs-fresh.
