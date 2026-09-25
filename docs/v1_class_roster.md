# v1 class roster — the SRD-based rebuild

*Internal design doc. Naming: the player-facing name is always **Lanorim**; internal use is either **Maps & Math Rocks** or Lanorim.*

Written 2026-09-21 from the class/species scope workbook, rewritten into current **5e-SRD terminology**.
The workbook was authored in the abandoned homebrew's words (*Vigor, Channeling, Strain, Snag/Trouble,
Nerve, die-step, two-action*); none of those apply now. This doc uses SRD terms: **HP**, **spellcasting**,
the **nat-1/nat-20 consequence pool**, **milestone leveling**, and **2 actions + 1 bonus action + 1 reaction**.

## What v1 ships — 7 classes

**Barbarian, Fighter, Rogue, Mage, Cleric, Paladin, Druid.** Between them they cover nine SRD class fantasies.
One SRD subclass each in v1; the rest are reference / fast-follow.

Decisions baked in (2026-09-21):

- **Mage is a merged arcane class.** One class carries Wizard's breadth, Warlock's pact identity, and
  Sorcerer's innate/elemental themes. There is no separate Wizard, Warlock, or Sorcerer in v1 — those
  fantasies are Mage growth choices.
- **Fighter ships as its own class but reuses an existing companion** — no new companion or campaign-
  dialogue lane is written for it (it shares the Barbarian's bonded wolf). This is the trick that keeps extra classes cheap: the engine was
  always cheap, the companion writing was the cost, and a shared companion means we don't pay it twice.
  **Paladin (added as the 7th class) uses the same trick** — see below.
- **Paladin ships as the 7th class, sharing Cleric's companion.** It's a Cleric + Fighter hybrid, so
  both its systems already exist; the only genuinely new bit is a bounded aura stance. **Bard, Ranger,
  Sorcerer, Monk do not ship as standalone classes** — see the deferred table. (Ranger was the next cheap
  candidate and is intentionally deferred for v1.)

## Per class — SRD chassis → v1 adaptation

**Barbarian** — *Path of the Berserker*. STR, d12. Furious frontline survivor.
Keep Rage, Unarmored Defense, Reckless Attack, Weapon Mastery, Danger Sense.
Adapt: Rage's per-day use table → one bounded stance (cost/cooldown); **Extra Attack → +1 action**
(stacks on the base 2, so a raging Barbarian at the Extra-Attack level gets 3 actions). *Companion: bonded wolf.*

**Fighter** — *Champion*. STR or DEX, d10. Disciplined weapon master.
Keep Fighting Style, Second Wind, Weapon Mastery, tactical riders, Improved Critical.
Adapt: **Extra Attack → +1 action** (3 actions at the Extra-Attack level); **Action Surge → +1 action once
per rest** (a burst turn, not every round); the higher multiattack ladder collapses into that single +1
for v1. Fighter's edge is more actions plus fighting-style riders and reliability.
*Companion: shares the Barbarian's bonded wolf (no new lane).*

**Rogue** — *Thief*. DEX, d8. Skilled infiltrator and opportunist.
Keep Expertise, Sneak Attack, Cunning Action, Uncanny Dodge, Evasion, Thieves' Tools.
Adapt: Sneak Attack fires as a damage rider when a setup condition is met; **Cunning Action stays a
bonus-action feature** — Dash / Disengage / Hide on your bonus action, no extra full action; campaign-
specific tricks use authored interactable tags rather than a general object sandbox. *Companion: gossiping raven.*

**Mage** *(merged)* — Wizard (*Evoker*) breadth + Warlock (*Fiend*) pact identity + Sorcerer (*Draconic*)
innate themes. INT / CHA, d6. Broad arcane effects plus pact flavor.
Keep spell choice, cantrips, and rituals (as tagged narrative actions where a campaign opts in).
Adapt: **no spellbook transcription, no daily preparation, no Pact Magic slot table, no general Metamagic
builder.** Mage learns authored effects through milestone growth; invocations, metamagic, and elemental
identity are a small curated growth catalog, not build platforms. *Companion: bound imp in a ring.*

**Cleric** — *Life Domain*. WIS, d8. Armored divine champion and healer.
Keep Spellcasting, Channel Divinity, healing, radiant offense, undead turning.
Adapt: Divine Intervention → an authored miracle menu / campaign hook, not a freeform request. An optional
armored-smite / protection growth route covers most of Paladin without a sixth class. *Companion: saint's fragment.*

**Paladin** — *Oath of Devotion*. STR + CHA, d10. Holy knight — armored melee, burst smite, protective conviction.
Keep Lay on Hands, Spellcasting (the paladin list is already inside the v1 spell set), Channel Divinity,
Weapon Mastery, Divine Smite.
Adapt: **Extra Attack → +1 action** (3 actions at the Extra-Attack level); Divine Smite is a damage rider; auras → a
self-centered defensive stance, not a party buff; Faithful Steed → authored travel access, not a
controlled actor. As a Cleric + Fighter hybrid it reuses both classes' systems; only the aura stance is new.
*Companion: shares Cleric's saint's fragment (no new lane).*

**Druid** — *Circle of the Land*. WIS, d8. Nature mage and shapeshifter.
Keep Spellcasting, nature/utility, environment interaction.
Adapt: **Wild Shape → 3–5 curated form cards** (scout, travel, sturdy combat, utility), each a
pre-authored stat block — not an arbitrary beast-catalog converter. Wild Companion stays narrative or a
one-action effect, never a second controllable combatant. This is the single most expensive v1 feature;
the curated-forms cap is a declared product constraint. *Companion: borrowed-shape spirit.*

## Deferred / route-covered classes

| Class | v1 status | Covered by | Why not standalone in v1 |
|---|---|---|---|
| Bard | Partial | Rogue + Mage | Bardic Inspiration is built for allies; a solo game has no combat party for it to buff. |
| Ranger | Partial | Rogue + Druid | Skirmish + nature fantasy already covered; per-creature favored-enemy content is expensive. |
| Sorcerer | Folded | Mage (innate/elemental themes) | General Metamagic multiplies validation/UI across the whole spell catalog. |
| Monk | Full defer | — | Bespoke unarmed-combo grammar + animation; lowest-ranked core class. |

## Companion minis (v1)

The companion is a **live thing on the table, outside the map/campaign** — its own token, distinct from
the on-map minis. The 3D base is **Quaternius + KayKit** (`decisions_checklist.md` §4); the companions below
are all Quaternius creatures:

| Companion | Quaternius mini | Status |
|---|---|---|
| Barbarian + Fighter — bonded wolf | Wolf (Animated Animal Pack) | free download |
| Mage — bound imp | Imp (Bestiary – Dungeon Monsters) | **already owned** |
| Druid — borrowed-shape spirit | Fox / wolf / beast (Animated Animal Pack) | free download |
| Cleric + Paladin — saint's fragment | abstract — a relic/gem/wisp prop or small effect, no creature | flexible |
| Rogue — gossiping raven | no Quaternius bird | **open** — source a matching CC0 bird, re-theme, or commission |

**Companion voices in v1: 5 across 7 classes** — Barbarian + Fighter share one; Cleric + Paladin share
one; Rogue, Mage, and Druid each have their own.

Licence: Quaternius Asset License (commercial use, no credit required, no reselling the raw assets). See
`../THIRD_PARTY.md` for the single Quaternius licence block and pack list.

## Cross-cutting terminology corrections (applies to every class)

- **Extra Attack / multiattack:** ports literally as **+1 action**, stacking on the base 2 (an Extra-Attack
  martial gets 3 actions + 1 bonus + 1 reaction). The base 2 is solo compensation for running one hero, not a
  replacement for Extra Attack. The 5e multiattack ladder (2/3/4 attacks) collapses to a single +1 in v1;
  Action Surge = +1 action once per rest. *(Corrected 2026-09-23 — earlier drafts said "not literal".)*
- **Spell resource:** the spells shown on your sheet are the ones you can cast (flat **known/equipped**
  model) — no daily preparation. **Expenditure [DECIDED 2026-09-23]:** cantrips are at-will; leveled spells
  spend **spell slots** (SRD full-caster table for Mage/Cleric/Druid, half-caster for Paladin) **or spell
  points**, the player's choice at character creation. Tracked in `decisions_checklist.md` §1.
- **ASI levels:** feats are deferred, so each SRD "ASI or feat" level is a straight **+2 to spend**.
- **"Channeling" is retired** — Cleric/Paladin use Channel Divinity (SRD); casters use the known/equipped
  model above.
