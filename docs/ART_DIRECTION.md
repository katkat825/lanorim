# Art direction — the tabletop

*Internal design doc. Naming: player-facing is always **Lanorim**; internal is either **Maps & Math Rocks** or Lanorim.*

Harvested from the original art direction (2026-08-02) and **updated for lanorim's confirmed direction.**
What changed since the original: the table is now **table-only** — no 3D room, no DM hands; the GM screen is
a **bent quad with a static parallax image** on the player side. Dice are **standard polyhedral (d20 + mods)**,
not the old three-die "best-two" pool. The asset base is **Quaternius** (KayKit retired). **The visual
signature — one palette + the painted-miniature shader — is unchanged and carries over intact; it is the
single most valuable thing to harvest from the old build.**

---

## 1. The thesis: don't depict a tabletop game, *be* one

The player is looking at **a table.** A map on it, painted miniatures standing on the map, a dice tray with
real dice, a camera that is a person leaning over the table. Nothing is a compromise, because the fiction
*is* a tabletop game — and that's the only art direction where every limitation becomes a feature:

| Limitation | In a normal game | Here |
|---|---|---|
| No walk cycles | Looks broken | Minis get picked up and set down. Correct. |
| No facial animation | Looks cheap | Painted minis have no expressions. Correct. |
| Grid-snapped movement | Feels restrictive | That's how minis move. Correct. |
| Death is a tip-over | Looks lazy | Exactly what happens to a mini. Correct, and delightful. |
| Sparse environments | Looks unfinished | It's a map on a table. Correct. |
| Flat lighting | Looks dated | It's a lamp over a table. Correct. |

The cheapest possible answer is also the most thematically correct one. That's the whole bet.

## 2. What this deletes from production

Entire disciplines you never have to touch: locomotion animation (minis hop/slide/tip), facial rigs and lip
sync, IK / foot-planting / ragdolls, camera collision and traversal, environment art at scale (you build
tiles, not levels), complex lighting (one warm key light), and all climb/swim/jump/vault systems (minis are
placed). This is the difference between a project that needs an animator and one that doesn't.

## 3. References worth studying
- **Demeo** — the closest existing thing: a tabletop with minis and dice, and proof the look reads as
  *premium*, not cheap.
- **Tabletop Simulator** — for the physicality of dice and pieces, and how much the table itself sells it.
- **Painted miniatures** — reference for the shader, not the geometry: heavy silhouette, strong value
  contrast, exaggerated proportions that read clearly at arm's length.

## 4. The camera
Fixed three-quarter overhead, orbitable in **90° snaps**, zoom from "whole map" to "leaning over one mini."
The 90° snap means every tile only needs to look right from four angles. Add a slight handheld drift and a
shallow depth of field at the near/far table edges — that one effect sells "a real object in a real room"
more than any polygon budget.

## 5. Minis are objects that get moved
Motion vocabulary in full: **Move** (arc up, travel, set down, small squash — ~0.3s), **Attack** (lunge and
back, no swing), **Take a condition** (a wobble, a visible chip/scuff), **Die** (tip over and lie on the
map — leave the body there). A room that fills with tipped minis is a better record of a hard fight than any
kill counter; don't replace it with a "real" death animation. **Conditions read on the mini** where you can
(a wobble, a marker), so the player reads their state from the figure, not only a status bar.

## 6. The dice — spend everything here
Rolling is the core verb; the player does it thousands of times. If the dice have weight, sound, and
unpredictability, the game feels expensive no matter how simple everything else is.
- **Real rigid-body physics** in a physical tray. Not a canned animation.
- **Audio is half the effect** — rattle, impact, tumble, settle; vary samples heavily; die material changes
  the sound. (The moved Freesound pool + `game/audio` sound system cover this.)
- **Determinism is fine** — roll the number first, animate a die that lands on it. Every physics-dice game
  does this; nobody notices.
- **Never skip the roll** — offer a speed setting, not a skip.
- **Dice are d20 + the standard polyhedral set** (d4–d20). *(This replaces the old three-die pool / "Impact
  die"; the money shot is now a weighty d20 tumbling and settling, readable off the tray before any UI.)*
- **Materials are cosmetic, always** — wood, resin, marble, obsidian, brass, steel, gemstone change
  appearance/sound/heft and **nothing else**. No die grants a bonus. (Die-material textures already gathered
  — see `THIRD_PARTY.md`.)
- **Typography:** every 6 underlined; one font across all dice; consistent glyph size/margins so a d8 and a
  d12 read as one set. Readability first.

## 7. The map and the table
Modular tiles on the grid — floor, wall, door, stair, pit, rubble — composed, not modeled per level.
Unexplored area can be **blank table**, revealed as the player advances: cheap fog-of-war and exactly how a
DM reveals a dungeon. **The map builder is how these get made** (yours and Workshop authors'). The table
around the map — felt, a rulebook, pencils, a lamp, a mug ring — is the best cheap investment in identity,
and it's where menus/saves/loading live, as objects rather than UI panels.

## 8. The visual signature — one palette + one shader (beating the CC0 problem)

Thousands of devs use these exact packs; raw, the game looks like a hundred jam entries. Three fixes, in
order of leverage — **the first two already exist in the old build and are the top visual harvest:**

1. **Impose one palette.** Recolour every asset to a tight **~14-colour palette**, stated once in
   `tools/palette.ps1`; `tools/bake-palette.ps1` remaps a whole pack's atlas to the nearest swatch at once
   (Quaternius packs share flat-swatch atlases, so it's one image, not a month of texture work). Its
   `-Shading` dial sets how much of the pack's own light survives; 0 = flat swatches and the shader does the
   lighting. **Highest-leverage art task in the project.** → *Harvest `tools/palette.ps1` + `bake-palette.ps1`.*
2. **One shader — the "painted miniature."** Flat banded lighting (3 bands = base/shade/highlight), a **dark**
   liner at the silhouette (not a bright plastic rim), world-space brushstroke noise in the albedo, primer
   grey in the crevices, and a tight varnish gloss. It throws away each pack's own lighting and relights
   everything identically, so four packs become one look — and it's a look almost no one has, because no one
   else's game is about miniatures. This is your signature, and it's **one file**. → *Harvest
   `game/shaders/painted_miniature.gdshader` ~as-is (model-agnostic; works on Quaternius as it did on KayKit).*
3. **Commission only what carries the game** — the hero minis, the bosses, the dice. ~10–15 custom models;
   everything else stays free. (Optional; deferred.)

**Every number in the shader is an in-editor dial and must be tuned by eye** — bands, band_blend,
shade_floor, liner colour/width/strength, primer, brush strength/scale/stretch, varnish/tightness. Do **not**
author them blind. This is the natural home for the screenshot loop: apply the shader, screenshot the table,
adjust the dials against feedback, repeat.

## 9. Free-asset sources & provenance
Base is **Quaternius** (characters, dungeon, environments, monsters, animals); **Kenney** for UI + borders;
**game-icons.net** for icons (CC-BY, credit authors); fonts under OFL; plus the supplements (KayKit Forest/
Halloween decor, KittyCatGames paper, Admurin GM-screen art). Full list + licences: `../THIRD_PARTY.md`.
Record every download there; keep raw files gitignored under `assets/`; commit only processed maps.
**Texture-resolution targets** (more is not better — a die is ~90 px on screen): **512** dice, **1K** tray/
felt/walls, **2K** table top.

## 10. Production order (visual)
Each step is playable before the next: **1)** dice + tray (physics, audio, a weighty d20 reading clearly) —
build first; if throwing dice isn't fun on its own, nothing downstream saves it. **2)** grid + one tile set
(movement, snapping, 90° camera). **3)** one mini, moving (hop, attack, tip over). **4)** enemies + the
combat loop. **5)** conditions visible on the mini. **6)** table dressing — last, and it's when the game
suddenly feels real.

## 11. Settled
- 2D or 3D? → **3D minis.**
- Companion on the table? → **yes** — now an **off-map** live token (its own visual lane; may be animated).
- How literal is the table? → **table-only.** GM screen = a bent quad with a static parallax image; **no 3D
  room, no DM hands** (that was the abandoned direction).
