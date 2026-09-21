# Third-party assets

Where each asset came from and what licence it's under.

The raw downloads aren't in git. `assets/` is ignored because 1.2 GB of compressed archives would sit in history forever and git can't delta-compress them. If those downloads are ever lost, this file is how they get replaced, so it has to stay current.

Standing rule: check the licence on the specific asset page at download time and record it here. "The site is CC0" isn't good enough, because sites host guest submissions and terms change.

## Ruleset — D&D SRD 5.2.1 (CC-BY-4.0)

The game's rules are built on Wizards of the Coast's **System Reference Document 5.2.1**, used under
**Creative Commons Attribution 4.0 (CC-BY-4.0)**. It's a content/licence dependency rather than a
downloaded asset, but it lives here because — like every entry below — it carries an attribution
obligation that has to survive to the credits screen. Full rules of the road (Option B): build on the SRD, use no WotC trademarks or Product Identity, and
carry the attribution below. (The fuller rationale lives in the internal design notes.)

| What | Source | Licence | Obligation |
|---|---|---|---|
| SRD 5.2.1 — rules, generic classes/subclasses, spells, monsters (text usable verbatim) | Wizards of the Coast, via the SRD 5.2.1 legal PDF on D&D Beyond | CC-BY-4.0, irrevocable, commercial use permitted | One attribution notice: credit WotC, name SRD 5.2.1, link it, state CC-BY-4.0, imply no endorsement. **Copy the exact wording from the SRD PDF verbatim — do not paraphrase.** |

Do **not** use: trademarks ("D&D", the ampersand logo, product-line names), Product-Identity monsters
carved out of the SRD (Beholder, Mind Flayer, Displacer Beast, etc.), non-SRD rulebook prose, or WotC's
published adventures. Every campaign we ship is our own. Exact attribution wording and placement get
confirmed at the one-hour IP/legal review before the game sells.

## 3D assets — Quaternius (single-creator base)

Every 3D asset in lanorim comes from **Quaternius** (quaternius.com): characters, dungeon and other
environments (village, wilderness, sci-fi), monsters, and animals/companions. One creator, one style
family — this *is* the game's look. **KayKit is retired from lanorim.**

**Licence — Quaternius Asset License (QAL) v1.0** (quaternius.com/license.html; the version in effect at
download time governs). Commercial use permitted, **no attribution required**, perpetual and irrevocable
for what you download. The one restriction that matters: **you may not resell or redistribute the raw
assets as assets** — only baked into the finished game. This shapes Workshop: campaign authors reference
assets that ship with the base game by id and never upload raw Quaternius meshes. (We still credit
Quaternius, as a single line, on goodwill.)

**Provenance model going forward:** one licence covers all of Quaternius, so we do **not** track each pack
in the per-file detail the audio below gets. Instead — this one licence block covers everything Quaternius,
plus a light list of *which packs* were downloaded (name · URL · date) so a lost `assets/` can be rebuilt.
**Anything NOT from Quaternius still gets a full individual entry** (the SRD above; audio, textures, and any
parallax backgrounds below) — those carry per-item licences that vary.

Packs in use (all from quaternius.com; downloaded 2026-09-21 into `lanorim/assets/`):

| Pack | Role | Notes |
|---|---|---|
| Ultimate Animated Character Pack | Characters / heroes | The player-mini source. |
| Ultimate Fantasy RTS | Characters + fantasy buildings | Extra unit/character variety. |
| Bestiary – Dungeon Monsters Kit [Standard] | Monsters | Standard/free tier = Imp + Puglin only; full 7-monster set is a paid upgrade. |
| Ultimate Animated Animals | Companions + beast minis | Wolf, fox, etc. — companion source *and* quadruped-beast enemies. |
| Easy Animated Enemy Pack | Monster minis (vermin) | Spider, rat, snake, frog — the vermin/spider archetypes. |
| Updated Modular Dungeon | On-map dungeon environment | Primary battle-map kit. |
| Ultimate Modular Ruins Pack | Environment (ruins) | On-map / setting. |
| Ultimate Modular Sci-Fi | Environment (sci-fi) | Future/other-setting campaigns. |
| Medieval Village MegaKit [Standard] | Environment (village) | Big village kit. |
| Medieval Village Pack (Dec 2020) | Environment (village) | Older/smaller village pack — overlaps the MegaKit; keep one. |
| Farm Buildings | Environment (rural) | Setting decor. |
| Pirate Kit | Environment (pirate) | Setting. |
| Ships | Props / environment | Setting. |
| Ultimate House Interior Pack | Environment (interiors) | Indoor scenes. |
| Stylized Nature MegaKit [Standard] | Environment (nature) | Outdoor foliage/terrain. |
| Textured Stylized Trees | Environment (trees) | Foliage. |
| Fantasy Props MegaKit [Standard] | Props | Table/scene dressing. |
| Ultimate RPG Items Pack | Items | Inventory/loot icons & meshes. |
| Medieval Weapons Pack | Items (weapons) | Weapon meshes. |
| Survival Pack | Items | Misc props. |

**Status (2026-09-21):** the animal pack (Ultimate Animated Animals — wolf/fox companions + beasts) and
Easy Animated Enemy Pack (spider/vermin) are now on disk. The **ooze/slime** is solved in-house — a rounded
pebble/rock from the Stylized Nature kit (scaled up) or a mushroom cap, recoloured and given a **translucent,
glossy "slime" material** (the wet look is what sells it, not the colour); a gelatinous cube is just a
translucent cube. No purchase needed. The one remaining nice-to-have is the **full Bestiary** (7 monsters, a
paid upgrade from the Imp+Puglin standard tier). Dragons and exotic beasts (owlbear, drakes) are built from
the Dragon Bodyparts Bundle below; enemy humanoids/undead reuse the character-pack + Bestiary minis. Full archetype→source map: `docs/v1_minis_map.md`.

### Non-Quaternius supplements

Quaternius is the base, but a few non-Quaternius assets fill what Quaternius doesn't make (UI, icons, fonts,
SFX) or supplement it (paper props, map decor). Each keeps its own licence. (Transferred from the new-assets staging list.)

**3D props & map decor**

| Asset | Source | Licence | For |
|---|---|---|---|
| Dragon Bodyparts Bundle v1.0.5 (nimbuspawtales) | [itch](https://nimbuspawtales.itch.io/blender-dragon-bodyparts-bundle) | Free commercial & non-commercial; may ship in games; **no reselling/repackaging raw assets**; credit optional. DL 2026-09-21. | Kitbash kit — dragons, owlbears, drakes, beasts. |
| KittyCatGames — Books & Papers (`Meshes.zip`) | [itch](https://kittycatgames.itch.io/books-papers) | Custom — 100% free personal/commercial, **no attribution required**. `.fbx` → convert to `.glb`. | Paper props — character sheet, campaign book. |
| KayKit — Forest Nature Pack | [itch](https://kaylousberg.itch.io/kaykit-forest) | CC0 | Map decor — nature props. |
| KayKit — Halloween Bits | [itch](https://kaylousberg.itch.io/halloween-bits) | CC0 | Map decor — spooky/seasonal props. |

**UI, icons & sounds** (Quaternius makes none of these)

| Asset | Source | Licence | For |
|---|---|---|---|
| Kenney — Fantasy UI Borders | [kenney.nl](https://kenney.nl/assets/fantasy-ui-borders) | CC0 | Map / grid / UI borders. |
| Kenney — UI Pack: RPG Expansion | [kenney.nl](https://kenney.nl/assets/ui-pack-rpg-expansion) | CC0 | UI panels / buttons / frames. |
| Kenney — UI Pack: Adventure | [kenney.nl](https://kenney.nl/assets/ui-pack-adventure) | CC0 | UI panels / buttons / frames. |
| game-icons.net (SVG bundle) | [game-icons.net](https://game-icons.net/) | **CC BY 3.0 — attribution REQUIRED** (credit the icon authors; keep the bundle's author list) | Condition / action / item icons. |
| Kenney — Interface Sounds | [kenney.nl](https://kenney.nl/assets/interface-sounds) | CC0 | UI SFX (clicks, confirms) — trial fit. |

**Fonts** — all SIL Open Font License (OFL): free commercial use, embeddable in the game; just don't sell the font files on their own.

| Font | Role |
|---|---|
| EB Garamond | Spell / other cards |
| Alegreya SC | UI / buttons / labels |
| Atkinson Hyperlegible Mono *or* Cormorant Upright | Storytelling / narration (pick one) |

**2D GM-screen art:** Admurin parallax backgrounds — detailed in the *3D models* section below (custom
licence, Workshop caveat).

**Planned (not yet purchased):** individual **Downrain DC** ([itch](https://downraindc3d.itch.io/)) animated
creatures for **companion** minis — verify each licence at purchase and add its row. Going with what we have
for now.

## Audio

Freesound licences are per clip, not per site. ambientCG, Poly Haven and ShareTextures publish everything under CC0; Freesound doesn't. Each upload carries whatever its author chose, usually one of:

| Licence | Means |
|---|---|
| CC0 | Free to use, modify, ship commercially. No attribution needed. |
| CC-BY | Usable, but the author must be credited. |
| CC-BY-NC | Non-commercial only. Unusable if this ever sells. |

A clip whose licence I can't prove has to come out later, after it's already all over the mix.

| Clip | Source | Licence | Use |
|---|---|---|---|
| `558204__jakubpjp__gamemisc_dice-roll-on-wood_jaku5.wav` | [freesound.org/s/558204](https://freesound.org/s/558204/) by jakubp.jp, 2021 | CC0, verified on the page 2026-08-04 | 5 impacts. Single d10 on wood, Sony PCM M10 internal mics, 96 kHz 24-bit stereo, 1.34 s. |
| `485946__aunrea__10-sided-die-rolled-on-wood-table.wav` | [freesound.org/s/485946](https://freesound.org/people/aunrea/sounds/485946/) by aunrea, 2019 | CC0, verified on the page 2026-08-04 | 25 impacts. Tascam DR-100MKIII with an AT875R shotgun, 24/48 mono. |
| `545489__wardoctor17__d20-rolls.wav` | [freesound.org/s/545489](https://freesound.org/people/wardoctor17/sounds/545489/) by wardoctor17, 2020 | CC0, verified on the page 2026-08-04 | 25 impacts. d20 on a wooden table. |
| `441841__seanmporio__d20-rolling.wav` | [freesound.org/s/441841](https://freesound.org/people/SeanMPorio/sounds/441841/) by SeanMPorio, 2020 | CC0, verified on the page 2026-08-04 | 22 impacts, 3 quarantined. Single d20 on wood. The uploader notes the die hits an Altoids can on the fifth roll. |
| `432917__djlprojects__paper-rustle-and-plop-on-wooden-table.wav` | [freesound.org/s/432917](https://freesound.org/people/djlprojects/sounds/432917/) by djlprojects, 2018 | Attribution 4.0, verified on the page 2026-09-10 | paper rustling followed by notebook impacting table.  |
| `477435__rvgerxini__copper-and-metal-pieces-dropping-on-hardwood-surface.mp3` | [freesound.org/s/477435](https://freesound.org/people/Rvgerxini/sounds/477435/) by 
Rvgerxini, 2019 | CC0, verified on the page 2026-09-10 | metal impacting wood - use for metal dice.  |
| `649210__johanvanvuren__salt-or-pepper-on-counter-or-table-impact.wav` | [freesound.org/s/649210](https://freesound.org/people/Johanvanvuren/sounds/649210/) by 
Johanvanvuren, 2022 | CC0, verified on the page 2026-09-10 | salt or pepper shakers on table - use for minis?  |
| `505963__jedg__small-single-plastic-impacts.wav` | [freesound.org/s/505963](https://freesound.org/people/jedg/sounds/505963/) by 
jedg, 2020 | CC0, verified on the page 2026-09-10 | plastic tiles on wood table - maybe dungeon tiles being laid out?  |
| `258249__youandbiscuitme__wooden-object-set-on-table-6.wav` | [freesound.org/s/258249](https://freesound.org/people/youandbiscuitme/sounds/258249/) by 
youandbiscuitme, 2014 | Attribution 3.0, verified on the page 2026-09-10 | wood bowl on wood table - dice tray being set on table  |

### Slicing

Every dice recording you can download is a whole roll, not an impact: one die tumbling for a second or more, containing five to sixty bounces. Played as one-shots they drift: the recording's bounces land at fixed times while the physics dice land whenever they land, and the two come apart within about half a second. What's needed is one short sample per actual collision, with volume and pitch driven by impact force.

`tools/slice_impacts.py` does that. Point it at `assets/`, it finds the transients and writes a bag of impacts for `AudioStreamRandomizer`:

```
python tools/slice_impacts.py                    # every wav in assets/
python tools/slice_impacts.py assets/foo.wav     # just one
python tools/slice_impacts.py --out game/audio/dice_stone
```

Current pool is 77 samples, 1.1 MB, in `game/audio/samples/impacts/wood/`. Filenames keep the Freesound id as a prefix (`545489_07.wav`) so provenance survives in the file itself.

Each sample comes out mono, because `AudioStreamPlayer3D` can't spatialise stereo and will silently do nothing if you feed it any. 48 kHz 16-bit, since Godot resamples anyway. Trimmed to the transient with 3 ms pre-roll, 1 ms fade-in and 10 ms fade-out; the fade-in is there because a cut landing mid-waveform clicks. Normalised to −3 dBFS so loudness comes from impact force at runtime. Onsets are rejected unless preceded by real quiet, otherwise you get samples starting halfway through the previous bounce.

It parses RIFF directly rather than using Python's `wave` module, which refuses 32-bit float. `545489` is exactly that, and a fair share of Freesound uploads are.

### The Altoids can

`441841`'s description warns about the metallic clang on the fifth roll, so I measured the samples for spectral tonality and decay length rather than trusting my ears. All eight of the most metallic samples came from that clip. Three were 3–4 standard deviations out, with `441841_18` ringing for 284 ms against a pool average of 77 ms. Those three are excluded from the pool — 22 of 25 from that clip ship. They are not kept on disk: `slice_impacts.py` is deterministic, so re-running it against the source clip in `assets/` reproduces all 25 and you can listen to the rejects then. The other five had long tails but normal tonality, which is just a die rolling to a stop, so they stayed.

The measurement is only a proxy, so I should sit down and listen to those three properly at some point.

## Textures

Downloaded 2026-08-04. All three sources publish under CC0 1.0: commercial use fine, modification fine, no attribution required. Everything from ShareTextures was checked individually.

### ambientCG — https://ambientcg.com

2K JPG sets (albedo, normal, roughness, displacement, AO).

| File | Use |
|---|---|
| `Fabric081A`, blue fine weave | Tray lining, tablecloth |
| `Leather033A`, mild wear dark | DM screen, rulebook cover, tray rim |
| `Marble012`, white marble | Die material |
| `Marble016`, black marble | Die material |
| `Metal048A`, shiny gold | Die material |
| `Metal057B`, copper | Die material |
| `Onyx011`, blue/tan swirls | Die material |
| `Onyx013`, dark swirly | Die material, obsidian |
| `Onyx015`, white faint swirls | Die material |
| `Wood067`, 1K | Tray frame. In the project at `game/textures/tray_wood/` |
| `Fabric034`, 1K felt | Tray floor. `game/textures/tray_felt/`, tinted green and teal |
| `Onyx011`, 1K | Dice. `game/textures/die_onyx/`, triplanar |
| `Plastic018B`, 2K | Map surface — the wet-erase battle map. Recoloured grey → mottled sepia/parchment into `game/textures/map_parchment/` (color, normalgl, roughness at 1K), roughness biased matte. See the note below. |

### Poly Haven — https://polyhaven.com

4K `.blend` scenes, much larger than needed. Source material only.

| File | Use |
|---|---|
| `crepe_satin` | Tablecloth |
| `quatrefoil_jacquard_fabric` | Tablecloth, richer |
| `terlenka` | Fabric |
| `dark_wood` | Table, tray walls |
| `rosewood_veneer1` | Table, the good-table look |
| `wooden_panels` | Room walls |
| `wood_table`, `wood_table_001`, `wood_table_worn` | The table |
| `leather_red_03` | DM screen, chair |
| `rock_01` | Die material, stone |

### ShareTextures — https://sharetextures.com

1K JPG sets. One item so far.

| File | Use |
|---|---|
| `amethyst_texture_1` | Dice. Parked, see below |

### Coverage

The collectible die materials are nearly all there: wood (`dark_wood`, `rosewood_veneer1`), marble (`Marble012`, `Marble016`), obsidian (`Onyx013`), brass (`Metal048A`), copper (`Metal057B`), stone (`rock_01`), gemstone (`Onyx011`, `Onyx015`). Resin is the one real gap. Steel would be `Metal048A` recoloured. Felt, wood and leather for table dressing are covered several times over.

## 3D models — processing note

lanorim's 3D provenance and the full pack list live in **"3D assets — Quaternius"** near the top of this
file. The old KayKit-based pipeline that used to sit here has been **retired** — KayKit is no longer used in
lanorim (see the art-cohesion decision, `docs/decisions_checklist.md` §4). Only the still-relevant
bits are kept below.

**Bringing a pack model into the game:** pull just the models this game needs out of the zip, run them
through the one-palette recolour + painted-miniature shader so mixed sources read as one game, and commit the
processed model. Two gotchas that still bite:

- Importing a `.glb` makes Godot extract the texture it had embedded and drop a raw copy beside the model —
  so bake the *folder* to the palette, not the file, and bake again after any re-import.
- Take **NormalGL**, never NormalDX (Godot is OpenGL-convention); the wrong one inverts lighting on one axis.

### Admurin — Parallax Backgrounds (2D GM-screen art, NOT models) — still in use

2D parallax background art for the **player-facing side of the GM screen** (one per campaign) — which fits
the rule that 2D appears only as framed content on a prop. **Custom licence, NOT CC0** — human-made ("No
generative AI," verified 2026-09-20), commercial use fine, with strings.

| Pack | Source | For |
|---|---|---|
| `Parallax_Backgrounds_Dock` | [admurin.itch.io/parallax-backgrounds-dock](https://admurin.itch.io/parallax-backgrounds-dock) | GM-screen background |
| `Parallax_Backgrounds_DeadForest` | [admurin.itch.io/parallax-backgrounds-dead-forest](https://admurin.itch.io/parallax-backgrounds-dead-forest) | GM-screen background |
| `Parallax_Backgrounds_SnowyMountains` | [admurin.itch.io/parallax-backgrounds-snowy-mountains](https://admurin.itch.io/parallax-backgrounds-snowy-mountains) | GM-screen background |
| `Parallax_Backgrounds_Plains` | [admurin.itch.io/parallax-backgrounds-plains](https://admurin.itch.io/parallax-backgrounds-plains) | GM-screen background |
| `Parallax_Backgrounds_Cave` | [admurin.itch.io/parallax-backgrounds-caves](https://admurin.itch.io/parallax-backgrounds-caves) | GM-screen background |

**Admurin's terms:** commercial use in a game is fine; **do NOT resell/redistribute the assets as standalone
game assets** (they must be integrated), no NFTs, no AI-training use, credit optional. ⚠ **Workshop concern:**
if a campaign *bundles* one of these files and is shared via Steam Workshop, that may count as redistributing
the asset, which the licence forbids. Safest: ship the backgrounds with the **base game** and have campaigns
reference one by id. Settle before enabling Workshop.

### Retired from lanorim

Superseded by Quaternius and removed: the **KayKit** character/environment packs — Adventurers, Dungeon,
Skeletons, Fantasy Weapons, Furniture, RPG Tools (Quaternius covers heroes, dungeon, monsters, weapons,
furniture). **Kept as supplements** (see *Non-Quaternius supplements* above): KayKit Forest Nature + Halloween
Bits (map decor) and KittyCatGames Books & Papers (paper props) — Quaternius doesn't cover those niches as
well.

## Code

One runtime dependency that is not an asset, recorded here for the same reason the assets are: if
the game ever sells, the provenance has to be airtight, and "it came off NuGet" is not provenance.

| Package | Version | Licence | Why |
|---|---|---|---|
| `YarnSpinner.Compiler` (and `YarnSpinner`, which it brings) | 3.2.2, [github.com/YarnSpinnerTool/YarnSpinner](https://github.com/YarnSpinnerTool/YarnSpinner) | MIT, verified on the repository 2026-09-15 | The branching-dialogue runtime, Phase W. `ARCHITECTURE.md` section 6 is a standing decision not to write one, and this is it. |

**Why the compiler and not just the runtime.** Ink would have meant shipping `inklecate` and asking
every campaign author to run it; Yarn's compiler is a library, so `Content.Dialogue.DialogueBook`
compiles a campaign's `.yarn` files at LOAD TIME. A Workshop author writes dialogue in a text editor
and the game reads it, which is the same promise the rest of the campaign format already makes.

MIT means it ships with the game and wants its copyright notice carried, which the package does in
its own metadata. It brings Antlr4's runtime with it, BSD-licensed, same terms.

## Resolution

A die is 50 mm on a 640 mm tray. If the tray fills about 1200 px of a 1080p screen, a die occupies roughly 90 px. A 4K texture on that is around 45× more texels than pixels, costing VRAM, load time and repo space for detail nobody can see.

| Surface | On screen | Texture |
|---|---|---|
| Dice | ~90 px | 512 is generous |
| Tray, felt | ~1200 px | 1K |
| Table top | fills the frame | 2K |
| Room walls | soft focus anyway | 1K |

The ambientCG 2K sets only need downscaling for dice. The Poly Haven 4K `.blend` files are source material: extract the maps, downscale, discard the rest. A 200 MB `.blend` has no business near `game/`.

## Workflow

```
assets/            raw downloads, gitignored
game/textures/     processed maps at shipping resolution, committed
THIRD_PARTY.md     this file
```

Download to `assets/`, take only the maps you need (albedo, normal, roughness, sometimes AO), downscale, save into `game/textures/<material>/`, commit those, add a row here. Godot re-compresses on import, so what's committed is the source of truth for the look rather than the final bytes.

Every future asset gets its row at download time rather than later, for the reason above.

## Notes from doing this

### Unpacking an ambientCG zip

`Wood067` was the worked example: 5.7 MB in, 3.3 MB out. Keep `_Color`, `_NormalGL`, `_Roughness`, and `_Displacement` if you want parallax. Discard the `.blend`, `.usdc` and `.mtlx` authoring formats, the bare `.png` web thumbnail, and `_NormalDX`.

Take NormalGL, never NormalDX. They differ only in the sign of the green channel; DX is for DirectX-convention engines and Godot uses OpenGL's. Pick wrong and the lighting inverts along one axis, surfaces look subtly off, and you lose an afternoon to it.

The bundled `StandardMaterial3D` is nearly usable and does correctly point at NormalGL, but needs two fixes. Its `ext_resource` paths are relative and its UIDs are ambientCG placeholders no Godot project has generated, so rewrite the paths as `res://…` and drop the `uid=` attributes. And it ships `heightmap_scale = 1.0`; full-strength parallax on a tray frame at a shallow angle swims as the camera moves, so it's down to `0.03` here. The normal map does nearly all the work.

`uv1_scale` is 4 so the grain tiles across the frame instead of stretching one board over the whole thing.

### Tint in the material, not the image

`Fabric034` is the only felt on ambientCG and it's white. Its albedo averages `rgb(161,159,165)` with a colour spread of 6 across 255, near-perfectly neutral, so it multiplies to any colour cleanly. The colour lives in `albedo_color`:

| Material | `albedo_color` | Result |
|---|---|---|
| `felt_green.tres` | `(0.16, 0.50, 0.30)` | ~`rgb(26,80,49)`, poker-table green |
| `felt_teal.tres` | `(0.13, 0.50, 0.50)` | ~`rgb(21,80,82)`, teal mat |

Both land within a few points of real gaming felt. One texture serves any number of mats, each a 600-byte `.tres` sharing the same 3.9 MB of maps, and changing my mind is one value in the inspector with the result live on screen.

Heightmap is off for felt because cloth is flat and the tray is seen from above. `metallic_specular` is 0.15 since felt is matte, and `uv1_scale` is 3 so the weave reads at the right physical size on a 640 mm tray.

### Generated dice need triplanar

`DieParts.BuildMesh` generates the solids from `DieSolid` and never emits texture coordinates, so a normal material samples one corner pixel and the die comes out a flat colour. Nothing errors, which makes it easy to blame on the texture.

```
uv1_triplanar = true
uv1_world_triplanar = false
uv1_scale = Vector3(12, 12, 12)
```

Triplanar projects down all three axes and blends by surface normal, so it needs no UVs. It suits stone: the veining flows around the die as one continuous block instead of being cut into a patch per face.

`uv1_world_triplanar = false` matters. Local-space projection fixes the pattern to the die so it tumbles with it. World-space would make the veining swim across the surface as the die rolls, and you'd immediately see the geometry was generated.

Cost is three texture samples per pixel instead of one, which is irrelevant at 90 px.

It's all in `uv1_scale`. At 50 mm, `uv1_scale = 12` puts about half a repeat across the die, giving the large sweeping veins that make gemstone dice look good. Raise it for busier stone.

Watch the numerals. `Onyx011` averages `rgb(97,102,95)`, and `DieBody.Ink` defaults to near-black `(0.12, 0.10, 0.09)`, which is close to invisible on it. Overridden to a warm off-white in `die.tscn`. Pale gold `(0.85, 0.72, 0.38)` looks nicer on stone but needs a readability check first.

### Other sites don't follow ambientCG's conventions

The ShareTextures amethyst needed two conversions.

Its normal map is DirectX and nothing says so. Rather than guess, it's measurable: take the vertical gradient of the displacement map and correlate against the normal's green channel. Positive is GL, negative is DX.

```
normal.jpg     r = -0.881   DX
normalgl.jpg   r = +0.884   GL, correct for Godot
```

Fix is inverting the green channel. Worth measuring rather than eyeballing, since the failure mode is that everything just lights slightly wrong forever.

It also ships `specular` rather than `roughness`, which is the older specular/glossiness workflow. The specular map averaged 26/255, spiking to 140 on crystal facets. Converted by inverting and remapping into a polished-gemstone band:

```
rough = 0.10 + (1 - normalised_specular) * 0.35     ->  mean 0.38, range 0.10..0.45
```

Facets come out glossy, the body stays satin, nothing goes mirror-like. Using the specular map raw would have made the die uniformly dull. It ships an AO map too, wired at `ao_light_affect = 0.4`.

Every source names things differently and some name them wrongly. `diffuse` is albedo, `specular` needs converting, and you have to measure a normal map's convention instead of trusting the filename.

### Amethyst is parked

It reads blurry on the dice and the onyx is back in `die.tscn`. The processed maps stay in the repo since swapping is one line.

Turned out not to be the texture. Measured against the onyx it's sharper on every axis: contrast 35.9 vs 25.4, fine detail 11.39 vs 8.61, strong edges 33.8 vs 21.4.

The cause is `uv1_scale`, and it's a mistake about what kind of stone this is. Onyx is broad banding, so magnifying it gives sweeping veins that read well. Amethyst is fine crystal structure, and magnified the same amount that becomes large soft blobs.

When I come back to it: raise `uv1_scale` a long way, try 25 to 40. Turn off `ao_enabled`, since at high magnification a 1K AO map adds mushy dark patches. Then raise `normal_scale` back toward 1.0 if the facets still look soft. Maybe half an hour of work, and not urgent.

### Recolouring the map to parchment

`Plastic018B`'s colour map is a mottled, scuffed grey — perfectly neutral (saturation 0.0, luminance ~0.33–0.68), which is exactly what a wet-erase battle map wants underneath: the scuffs and veins read as an aged, used surface. It just needed to be sepia instead of grey.

A flat `albedo_color` multiply — the felt trick — was the wrong tool here. Felt is near-white, so a multiply tints it cleanly; multiplying a mid-grey by a warm colour only ever gives a muddy tan, because there is no brightness range left to spread a parchment across. So the colour is a **gradient map instead of a tint**: the grey's luminance is stretched from its compressed range into a four-stop sepia ramp — deep sepia in the crevices, tan and warm parchment through the mids, cream on the scuffs. That is a baked colour map rather than a material tint, the one deliberate exception to "tint in the material," and it is baked because a duotone is the only thing that turns neutral grey into parchment.

Several variants were rendered and looked at, from a deep aged-scroll tan down to a pale washed cream. What shipped is a **light parchment** — warm off-white with the sepia mottle still clearly reading, light enough that the grid and the minis sit clearly on top. The ramp stops are the tuning knob if it ever wants to go deeper or paler.

`NormalGL` (not DX, per the ambientCG note above) and `Roughness` are the plastic's own, downscaled to 1K. The roughness is biased matte — `0.60 + 0.40 × r`, so it floors well short of glossy — because parchment is never shiny and the raw plastic would have caught the table lamp like a laminate. `normal_scale` is dropped to 0.5 in the material: a battle map is nearly flat, and the full plastic relief swims at this camera angle.

The board wears it in `game/Board/board.tscn` as the `Mat` material, with the grid lines changed from the felt era's near-black to a soft sepia ink. B2 replaces the whole placeholder mat with tiles from a data file; until then this is the map.

>**FLAG (2026-09-10) — the "all CC0" line below is no longer strictly true, and is left as-is on purpose until it matters.** Two Freesound clips added 2026-09-10, `432917` (paper) and `258249` (wood bowl), are **CC-BY (Attribution)**, not CC0 — and neither is used in the game yet. If either ever ships, its author must be credited by name and the blanket line loosened. The standing intent (Kathleen, 2026-09-10) is to **credit every asset author regardless of licence anyway** — goodwill, and it future-proofs against any pack quietly moving from CC0 to CC-BY — so this section becomes a per-author list rather than one line. Not done yet because nothing here needs it; revisit when the first credits screen is built.

## Credits

CC0 requires nothing, but three lines on a credits screen are cheap:

> Textures from ambientCG.com, PolyHaven.com and ShareTextures.com.
> Sound from Freesound.org.
> 3D models from Quaternius.
> (Not all under CC0 — 3D is Quaternius Asset License, the SRD is CC-BY-4.0; see the sections above. This blanket line gets rebuilt into a proper per-source credit when the credits screen is built.)
