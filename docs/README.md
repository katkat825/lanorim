# Lanorim — design docs

The design record for Lanorim (internal: Maps & Math Rocks). Read this before touching the code — these
docs are the source of truth for what the game is and what gets built.

**Two entry points:**

- **`decisions_checklist.md`** — the master decision tracker: SRD rule deltas, v1 content scope, the
  video-game glue, art, and product. *What the game is.*
- **`v1_build_checklist.md`** — the end-to-end build scope with a "definition of done." *What gets built,
  and when it's finished.*
- **`v1_build_order.md`** — the sequence to build v1 in playable slices, with every task tagged **who does
  it** (you / Claude / a tool). *The order, and the division of labor.*

**The rest:**

- `v1_class_roster.md` — the 7 v1 classes (one subclass each) and their adaptations.
- `v1_species_roster.md` — the 7 v1 species.
- `v1_spell_list.md` — the 131 functioning v1 spells + the full-list reference-card plan.
- `v1_minis_map.md` — every mini (hero, companion, monster) and where its model comes from.
- `character_sheet_decisions.md` — the character sheet layout; abilities/skills/spells basis.
- `inventory_decisions.md` — the lean v1 inventory (40 slots, buy/sell, equip, gating).
- `updated_decisions.md` — UI/table layout, dialog, rest, campaign flow.
- `combat_ux.md` — how a fight is played at the table: the HUD, a turn, reactions and the Ask prompt, enemy turns, dice, keys. *(2026-09-24)*
- `how_to_play_combat.md` — the same, player-facing: what the tutorials and the help card say. *(2026-09-24)*
- `ART_DIRECTION.md` — the visual bible: the be-a-tabletop thesis, the camera, minis-as-objects, the dice, and the **one-palette + painted-miniature-shader** signature that unifies the assets.
- `deferred.md` — the parked pile: everything explicitly out of v1.

Asset & rules provenance lives in **`../THIRD_PARTY.md`** (repo root).
