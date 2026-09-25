# v1 build order — sequence + who does what

*Internal design doc. Naming: player-facing is always **Lanorim**; internal is either **Maps & Math Rocks** or Lanorim.*

The sequencing companion to `v1_build_checklist.md` (the *scope* — everything that must exist) and
`decisions_checklist.md` (the *what*). This is the *order*: build v1 as a chain of playable slices, and
every task is tagged by **who** should do it. Written 2026-09-21.

## Who does what — the principle

The split follows one line: **can the work be verified without eyes?**

- **Claude / an AI coding assistant** does the deterministic, testable, non-visual work: rules math, data
  models, the spell-primitive engine, combat logic, save/load, loaders, SRD data entry, UI *logic*, tests.
  Code whose correctness you check by running it, not by looking at it.
- **You** do the visual, spatial, and taste work — plus the creative and business work: composing scenes in
  Godot, building maps, tuning materials/lighting/camera, Blender kitbashing, judging "does this look
  right," writing your campaigns, playtesting, and the Steam/legal side. *(This is exactly the line the old
  build tripped over — an AI doing 3D layout it couldn't see. Keep that work with you.)*
- **Specialized tools** do their part: **Godot** (scenes, editor, running the game), **Blender**
  (models/materials/export), **Steamworks** (store/Workshop), **git** (version control), a **lawyer** (final
  legal wording).

Most tasks are a handoff — **Claude writes the logic → you wire and test it in Godot.** Tags show the
primary owner; **→** marks a handoff.

**Legend:** **(Claude)** · **(You)** · **(Godot)** · **(Blender)** · **(Steam)** · **(You+lawyer)** · **→** handoff.

## Aim here first — the first playable slice

Before widening, target one thin vertical slice that proves the whole stack:
**make a character → stand on a map you built → fight and beat a goblin → level up.**
Phases 0–3 build to exactly that. Get it working end to end before adding breadth. Then your **first *public* ship is the demo (Phase D)** — done early, well before full v1.

---

## Phase 0 — Setup & harvest
- Initialize the Godot/C# project + repo layout — **(You in Godot)**, scaffolding **(Claude)**
- git init → first commit → public GitHub remote *(public for portfolio; LICENSE reserves rights)* — **(You)**
- Decide harvest-vs-fresh per old-game system — **(You)**, with **(Claude)** reviewing the old code
- Adapt reusable code — dice physics, content loader, save architecture, localization, YarnSpinner wiring — **(Claude)** → **(You in Godot)**
- Content data schema (classes/species/spells/monsters/items as data) — **(Claude)**

## Phase 1 — One character on screen
- SRD data entry: abilities, 18 skills, proficiency, a starter class + species, a handful of spells/items — **(Claude)**
- Character data model + a hardcoded test hero — **(Claude)**
- d20 resolution: rolls, adv/dis, checks, saves, attack/damage, crits, HP, death save — **(Claude)**
- Dice tray + 3D dice roll — harvested physics **(Claude)** → tray scene **(You in Godot)**
- Character sheet UI — logic **(Claude)** → layout with the Kenney UI kit + fonts **(You in Godot)**
- One hero mini on a static grid map — grid logic **(Claude)** → hand-placed test map **(You in Godot)**

## Phase 2 — The map builder (early, on purpose)
- Map file format + placement/save logic — **(Claude)**
- The editor: grid + asset palette + click/drag placement + spawn markers — **(You in Godot)**, drag-drop/UI logic **(Claude)**
- *Payoff: you build every later map yourself — no Godot hand-coding, no waiting on me for maps.*

## Phase 3 — First combat → **slice complete**
- Turn loop, initiative, 2/1/1 action economy — **(Claude)**
- Grid movement + range checks + radius AoE + opportunity attacks — **(Claude)**
- One goblin + basic approach/attack AI — logic **(Claude)** → place its mini **(You)**
- Combat interaction UX (how you issue an action) — design **(You)** → implement **(Claude)**
- Conditions subset + damage types / simple resistance — **(Claude)**
- ✅ **Milestone: character → your map → goblin fight → milestone level-up.**

## Phase 4 — Spells
- Spell-effect **primitive library** (the #1 cost) — **(Claude)**
- Spell resource (slots or points, chosen at creation), at-will cantrips, upcasting, binary concentration — **(Claude)**
- Wire ~15 spells first, then scale to the 131 (123 since 2026-09-25) — **(Claude)**
- Spell cards UI — logic **(Claude)** → cards in Godot (EB Garamond) **(You in Godot)**

## Phase 5 — Classes, species, progression
- 7 classes' core features + one subclass each — **(Claude)**
- Druid Wild Shape: 3–5 curated form cards — data/logic **(Claude)**, the form models **(You in Blender/Godot)**
- 7 species traits — **(Claude)**
- Milestone leveling to 20; backgrounds; starting gear/loot — **(Claude)**
- Character-creation flow — logic **(Claude)** → UI **(You in Godot)**

## Phase 6 — Inventory & merchant
- Inventory (40 slots, stacking, equip, class/level gating), buy/sell/discard — **(Claude)**
- Inventory + merchant UI — logic **(Claude)** → **(You in Godot)**

## Phase 7 — Narrative, GM & campaign system
- Dialogue runtime (YarnSpinner) + popup UI — **(Claude)** → **(You in Godot)**
- Generic companion speaker (`companion` alias) — resolves to the player's actual companion so one in-narrative line can be "said by any companion"; companion-specific lines override; follows the `dm` reserved-speaker precedent — **(Claude)** → **(You in Godot)** *(base-game only; not in Workshop)*
- Diegetic GM narrator + narrative branch-out/return for checks & combat — **(Claude)**
- Random-encounter tables + hidden GM rolls — **(Claude)**
- Campaign package format + loader — **(Claude)**
- GM screen prop (bent quad + Admurin backing image) — **(You in Blender/Godot)**

## Phase 8 — Content (your creative work)
- Finalize full SRD data entry (all v1 spells/monsters/items) — **(Claude)**
- **3 tutorial campaigns** (beginner / intermediate / advanced), replayable — **(You)**
- **A handful of one-shots** — **(You)**; some contractable to friends
- **A couple of level-block campaigns** — **(You)** / contracted
- **One epic level 1–20 campaign** — the flagship — **(You)**
- All built with the map builder. *Contracted campaigns:* a short contract that assigns or licenses the
  content to you keeps the repo's all-rights-reserved coherent; **attribute every author regardless of
  licence** (house policy).

## Phase D — The demo (your first *public* ship — do this early)

The demo doesn't wait for full v1. As soon as the **core loop works** (through ~Phase 5: character
creation, combat, spells, leveling) and you have **one polished tutorial + one polished one-shot**, package
and ship the demo — it's your portfolio piece and your wishlist engine, months ahead of the full game.

- Pick the demo content: **1 tutorial + 1 one-shot** — your best ~30 minutes — **(You)**
- Demo build target that ships only that content — **(Claude)** → **(You in Godot)**
- Steam **demo App ID** (a separate free app linked to the game); integrated "Download Demo" button or its own page — **(You in Steam)**
- Disable achievements; save to shared Steam Cloud; end-of-demo "wishlist / get the full game" screen — logic **(Claude)** → **(You in Godot/Steam)**
- Demo content survey on the demo App ID — **(You in Steam)**
- *Optional big win:* enter a **Steam Next Fest** with it for a wishlist spike — **(You in Steam)**

## Phase 9 — The table & presentation polish
- Table scene: dice tray, GM screen, companion, help button — **(You in Godot)**
- Palette-unify / painted-miniature shader across all assets — shader code **(Claude)** → apply **(You in Godot)**
- Blender builds: dragons/owlbears from the parts kit; the ooze material; gelatinous cube — **(You in Blender)**
- Companion minis (Quaternius) placed + idle presence — **(You in Godot)**, hint logic **(Claude)**
- Camera + lighting — **(You in Godot)**
- Audio wiring (dice / mini-move / UI sounds) — **(Claude)** → **(You in Godot)**
- UI theme from Kenney kits; icons from game-icons.net *(credit authors — CC-BY)* — **(You in Godot)**, theme logic **(Claude)**
- Accessibility pass, high depth — **(Claude)** + **(You)**

## Phase 10 — Workshop & release
- Steam Workshop integration (upload / subscribe / load user campaigns) — **(Claude)** → **(You in Steamworks/Godot)**
- Content policy for authors (no WotC Product Identity; asset-redistribution rules) — draft **(Claude)** → **(You)**
- Steam page + AI-content disclosure — **(You in Steam)**
- Legal review: exact SRD CC-BY attribution wording + placement — **(You+lawyer)**
- Build / export / packaging (PC/Steam first) — **(You in Godot)**

---

## On the handoffs

Claude-written code isn't *done* until you've run and tested it in Godot. That human-in-the-loop step is
what keeps the visual/spatial reality checked — the safeguard the old build skipped. Keep the loop tight:
small chunks, you verify each in the editor before the next. When a task is visual (a scene, a material, a
layout), you own it and I support with code; when it's logic (a rule, a formula, a data file), I own it and
you verify by running it.
