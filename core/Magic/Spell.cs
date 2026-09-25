using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Space;

namespace Core.Magic
{
    // what damage does to a condition a spell put on a creature
    public enum DamageEnds
    {
        None,

        // any damage at all: Sleep, Hypnotic Pattern
        Any,

        // damage from the caster or the caster's allies: Charm Person, Suggestion
        CasterSide,
    }

    // SRD 5.2.1 Command's five words
    public enum Command
    {
        None,
        Approach,
        Drop,
        Flee,
        Grovel,
        Halt,
    }

    public enum SpeedChange
    {
        None,
        Double,
        Half,
        Zero,
    }

    public enum School
    {
        Abjuration,
        Conjuration,
        Divination,
        Enchantment,
        Evocation,
        Illusion,
        Necromancy,
        Transmutation,
    }

    // one primitive with its numbers filled in. a spell is a list of these, and that list is the
    // whole of what the spell does.
    public sealed class SpellEffect
    {
        public SpellEffect(Primitive primitive,
                           Reach reach = Reach.Creature,
                           DiceRoll amount = default,
                           DiceRoll perExtraLevel = default,
                           DamageType damageType = DamageType.None,
                           Condition condition = Condition.None,
                           Ability? save = null,
                           OnSave onSave = OnSave.None,
                           bool attackRoll = false,
                           Duration duration = Duration.Instant,
                           int radius = 0,
                           int targets = 1,
                           int extraTargetsPerLevel = 0,
                           int sway = 0,
                           DiceRoll swayDice = default,
                           Sways touches = Sways.None,
                           Skill skill = Skill.None,
                           bool cantripScaling = false,
                           string note = null,
                           int length = 0,
                           int width = 0)
        {
            Kind = primitive;
            Reach = reach;
            Amount = amount;
            PerExtraLevel = perExtraLevel;
            DamageType = damageType;
            Condition = condition;
            Save = save;
            OnSave = onSave;
            AttackRoll = attackRoll;
            Duration = duration;
            Radius = Math.Max(0, radius);
            Targets = Math.Max(1, targets);
            ExtraTargetsPerLevel = Math.Max(0, extraTargetsPerLevel);
            Sway = sway;
            SwayDice = swayDice;
            Touches = touches;
            Skill = skill;
            CantripScaling = cantripScaling;
            Note = note ?? "";
            Length = Math.Max(0, length);
            Width = Math.Max(0, width);
        }

        public Primitive Kind { get; }

        public Reach Reach { get; }

        // damage, healing, temporary hit points - whatever this primitive counts in
        public DiceRoll Amount { get; }

        // what upcasting adds, per level above the spell's own
        public DiceRoll PerExtraLevel { get; }

        public DamageType DamageType { get; }

        public Condition Condition { get; }

        // null means no saving throw; the DC is the caster's, not the spell's
        public Ability? Save { get; }

        public OnSave OnSave { get; }

        // a spell attack roll instead of a save - Fire Bolt, Guiding Bolt
        public bool AttackRoll { get; }

        public Duration Duration { get; }

        // squares, for a burst
        public int Radius { get; }

        // squares, for a line or a cone, and the side of a cube
        public int Length { get; }

        // squares across, for a line. a cone's width is SRD's - its distance from the origin - and
        // a cube's is its length, so neither carries one
        public int Width { get; }

        public int Targets { get; }

        public int ExtraTargetsPerLevel { get; }

        // a flat shift: Bless's +1d4 is SwayDice, Shield's +5 AC is Sway
        public int Sway { get; }

        public DiceRoll SwayDice { get; }

        // which rolls the sway reaches. declared, never inferred
        public Sways Touches { get; }

        // narrows a Checks sway to one skill - Guidance is any check, Enhance Ability is one
        public Skill Skill { get; }

        // SRD cantrips get another damage die at 5, 11 and 17
        public bool CantripScaling { get; }

        // free text for the bounded approximations; never shown to a player
        public string Note { get; }

        // THE FIELDS BELOW ARE WHAT LET A SPELL KEEP ITS SRD NAME. each one is a shape an SRD
        // spell's text takes that the fields above could not say - and a spell that could not say
        // it was an approximation, and an approximation has to be renamed (decisions_checklist.md
        // section 1, the hard rule). they are init-only because nearly every effect leaves them
        // alone, and a constructor with thirty arguments reads worse than one with twenty.

        // add the caster's spellcasting modifier to the amount, once: Cure Wounds' 2d8 + mod
        public bool AddsModifier { get; init; }

        // advantage or disadvantage, as opposed to a number
        public Leans Leans { get; init; }

        // spent on the first roll it touches
        public bool Once { get; init; }

        // broken when the bearer attacks or casts: Invisibility
        public bool EndsOnAttack { get; init; }

        // whose turn a NextTurn or NextTurnEnd duration counts
        public Until Until { get; init; }

        // extra damage every time the caster hits the target with an attack roll: Hex. typed by
        // DamageType
        public DiceRoll Mark { get; init; }

        // the skill is the caster's pick at the moment of casting, not the spell's: Guidance
        public bool ChosenSkill { get; init; }

        // an ability is the caster's pick at the moment of casting: Hex's disadvantage
        public bool ChosenAbility { get; init; }

        // an unarmored armor class base: Mage Armor's 13
        public int UnarmoredBase { get; init; }

        // SRD's Eldritch Blast: the cantrip grows by more beams, each its own attack roll, rather
        // than by more dice on one
        public bool Beams { get; init; }

        // the same saving throw as the effect before it, not a second one: Meteor Swarm's fire
        // and bludgeoning are one Dexterity save
        public bool SameSave { get; init; }

        // how many burst centres one casting gets: Meteor Swarm's four. a creature caught in
        // more than one is still caught once
        public int Points { get; init; } = 1;

        // a creature at or below this many hit points dies instead: Power Word Kill's 100
        public int SlaysAtOrBelow { get; init; }

        // lands only on a creature the effect before it landed on: Guiding Bolt's glimmer comes
        // with a hit and not with a miss
        public bool Follows { get; init; }

        // done again, without paying again, each time the caster spends the spell's repeat on a
        // later turn while holding it: Spiritual Weapon's swing. the rest of the spell - the
        // weapon appearing - happens once
        public bool Repeats { get; init; }

        // for an effect that reaches "zone": the moments its zone does it
        public Pulses Pulses { get; init; }

        // for the zone itself: difficult terrain inside it
        public bool Rough { get; init; }

        // for the zone itself: the caster's own side is left alone
        public bool SparesAllies { get; init; }

        // for an afflict: the check a creature may spend its action on to break free - Web's
        // Strength (Athletics). null is no way out but the spell ending
        Ability? _escape;

        public Ability? Escape { get => _escape; init => _escape = value; }

        // for an afflict: the target saves again at the end of each of its turns, and a success
        // ends it - Hold Person's, Blindness's
        public bool RepeatSave { get; init; }

        // the escape check's skill and DC, when they are not the usual ones: Maze's Study action
        // is Intelligence (Investigation) against DC 20, not the caster's DC
        public Skill EscapeSkill { get; init; }

        public int EscapeDc { get; init; }

        // FLYING and what rides with it (2026-09-25): Fly, Gaseous Form
        public int FlySpeed { get; init; }

        public bool NoAttacks { get; init; }

        public bool NoCasting { get; init; }

        public IReadOnlyList<Condition> Immune { get; init; } = Array.Empty<Condition>();

        // an area that touches only so many of the creatures in it: Slow's "up to six"
        public int UpTo { get; init; }

        // the target leaves the board for the duration and comes back when it ends: Banishment,
        // Maze
        public bool Banishes { get; init; }

        // held this many rounds, a banished creature with one of these tags doesn't come back:
        // Banishment's minute on an Aberration, Celestial, Elemental, Fey or Fiend
        public int GoneAfterRounds { get; init; }

        public IReadOnlyList<string> GoneTags { get; init; } = Array.Empty<string>();

        // a zone on the ground, which a flyer passes over: Grease, Spike Growth
        public bool Ground { get; init; }

        // it ends on a target that drops to 0 hit points: Gaseous Form
        public bool EndsAtZero { get; init; }

        // an afflict that ends the moment its bearer makes an attack roll, deals damage or casts a
        // spell: Invisibility
        public bool EndsOnAct { get; init; }

        // a sway that turns a named spell's damage away while it lasts: Shield and Magic Missile
        public string WardsSpell { get; init; } = "";

        // an area that leaves its caster out: Entangle's "each creature (other than you)"
        public bool SparesCaster { get; init; }

        // dim light beyond the bright: Light's "Dim Light for an additional 20 feet". light is
        // told, not played, in a fight - this is the number the table would draw
        public int DimRadius { get; init; }

        // the advantage against the bearer counts only for an attacker that can see it: Faerie Fire
        public bool IfSeen { get; init; }

        // the disadvantage against the bearer doesn't hold for an attacker with Truesight: Blur
        public bool NotVsTruesight { get; init; }

        // the attack is made from the spell's own zone, against a creature this many squares from
        // it: Spiritual Weapon's force, "a creature within 5 feet of the force"
        public int NearZone { get; init; }

        // a failed save turns a shape-shifted creature back, and it can't shift again until it
        // leaves the zone: Moonbeam
        public bool RevertsShape { get; init; }

        // creatures with these tags make the save with disadvantage: Shatter's Construct
        public IReadOnlyList<string> DisadvantageTags { get; init; } = Array.Empty<string>();

        // creatures with these tags fail the save without rolling: Blight's Plant
        public IReadOnlyList<string> AutoFailTags { get; init; } = Array.Empty<string>();

        // the abilities a 'chosen' ability may be: Enhance Ability's five (not Constitution)
        public IReadOnlyList<Ability> AbilityChoices { get; init; } = Array.Empty<Ability>();

        // an afflict from a zone that holds only while its bearer is in the zone: Web's "while in
        // the webs"
        public bool WhileInZone { get; init; }

        // every creature after the first must be within this many squares of the first, and each
        // only once: Chain Lightning's leaps
        public int NearFirst { get; init; }

        // reduced to 0 hit points by it, the creature is dust: dead at once and past any revival
        // v1 has. Disintegrate
        public bool Dust { get; init; }

        // it ends a creation of magical force at the aimed square: Disintegrate
        public bool EndsForce { get; init; }

        // a creature with this tag killed by it rises as this statblock at the start of the
        // caster's next turn, on the caster's side: Finger of Death's Humanoid and Zombie
        public string RaisesAs { get; init; } = "";

        public string RaisesTag { get; init; } = "";

        // a teleport that brings one willing creature from beside the caster along: Dimension Door
        public bool Passenger { get; init; }

        // a teleport to anywhere in range, seen or not, that fails for 4d6 force on an occupied
        // square: Dimension Door
        public bool Unseen { get; init; }

        // held this many rounds, the condition stays when the spell ends: Flesh to Stone's minute
        public int PermanentAfterRounds { get; init; }

        // a zone of exactly one square, radius 0 on purpose: Spiritual Weapon's force
        public bool Point { get; init; }

        // narrows a check or save lean to one ability: Enlarge's Strength
        public Ability? LeansOn { get; init; }

        // damage types the sway halves: Stoneskin
        public IReadOnlyList<DamageType> Resists { get; init; } = Array.Empty<DamageType>();

        // feet of speed the sway adds or takes: Ray of Frost's -10
        public int Speed { get; init; }

        // the bearer makes no opportunity attacks: Shocking Grasp
        public bool NoOpportunityAttacks { get; init; }

        // the bearer gets nothing from being unseen: Faerie Fire
        public bool Exposes { get; init; }


        // --- ADDED 2026-09-24 so more spells keep their SRD names (cc_task_unattended-run) ---

        // for a zone: what it does to sight inside it. Fog Cloud and Darkness are heavy
        public Obscurement Obscures { get; init; }

        // for a zone: magical darkness - Truesight sees through it, Darkvision does not
        public bool MagicalDarkness { get; init; }

        // ends every zone of magical darkness this effect's area touches: Sunburst
        public bool DispelsDarkness { get; init; }

        // for an afflict: what taking damage does to it. Sleep and Hypnotic Pattern end on any
        // damage; Charm Person only on damage from the caster's own side
        public DamageEnds EndsOnDamage { get; init; }

        // for an afflict with a repeat save: taking damage also calls for the save, with
        // advantage - Hideous Laughter
        public bool SaveOnDamage { get; init; }

        // for an afflict: someone within 5 feet may spend an action to end it - Sleep's "shake it
        // out of the spell", Hypnotic Pattern's stupor
        public bool Shakeable { get; init; }

        // for an afflict with a repeat save: a failed repeat save (WorsensAfter of them) swaps the
        // condition for this one and the saves stop - Sleep's Unconscious, Flesh to Stone's
        // Petrified. SucceedsAfter successes end it; SRD's usual is one
        public Condition Worsens { get; init; }

        public int WorsensAfter { get; init; } = 1;

        public int EndsAfter { get; init; } = 1;

        // only creatures with one of these tags are affected; anything else is untouched, as if
        // it had saved. Hold Person's Humanoid
        public IReadOnlyList<string> OnlyTags { get; init; } = Array.Empty<string>();

        // creatures with one of these tags are untouched: an elf's Trance against Sleep, a
        // Construct against Flesh to Stone
        public IReadOnlyList<string> ExceptTags { get; init; } = Array.Empty<string>();

        // extra dice against creatures with one of these tags: Divine Smite's fiends and undead
        public IReadOnlyList<string> ExtraAgainst { get; init; } = Array.Empty<string>();

        public DiceRoll ExtraAmount { get; init; }

        // the target saves with advantage when it is being fought - Charm Person's "if you or
        // your allies are fighting it", which on a board is any hostile target
        public bool AdvantageIfFought { get; init; }

        // hit-point gates: only a creature at or below / above this many hit points is affected -
        // Power Word Stun's 150
        public int OnlyAtOrBelow { get; init; }

        public int OnlyAbove { get; init; }

        // a willing target (the caster's own side) is not asked to save: Enlarge/Reduce
        public bool SaveIfUnwilling { get; init; }

        // for a spell with modes: this effect belongs to one of them, chosen at the cast.
        // Blindness/Deafness, Enlarge/Reduce, Greater Restoration's list
        public string Mode { get; init; } = "";

        // the damage type is the caster's pick at the cast, from these: Chromatic Orb
        public IReadOnlyList<DamageType> DamageChoices { get; init; } = Array.Empty<DamageType>();

        public bool ChosenDamageType => DamageChoices.Count > 0;

        // for a damage effect: the damage lands at the end of the target's next turn instead of
        // now - Vitriolic Sphere's second 5d4
        public bool Delayed { get; init; }

        // for a damage effect: it lands again at the start of each of the target's turns while
        // it lasts - Searing Smite's burning
        public bool Recurs { get; init; }

        // the target saves at the end of each of its turns to end this, without a save to begin
        // with - Searing Smite's Constitution save
        public Ability? EndSave { get; init; }

        // a failed save costs the target its concentration: Sleet Storm
        public bool BreaksConcentration { get; init; }

        // for a sway: the bearer's speed doubled, halved or made 0
        public SpeedChange SpeedChange { get; init; }

        public bool Truesight { get; init; }

        // for a sway: the amount raises the hit point maximum and current hit points - Aid
        public bool RaisesMaximum { get; init; }

        public bool DeathWard { get; init; }

        public bool NoReactions { get; init; }

        public bool ActionOrBonus { get; init; }

        public bool NoActions { get; init; }

        public bool LimitedAction { get; init; }

        public DiceRoll WeaponDice { get; init; }

        // the weapon dice come off rather than on: Reduce's -1d4, never below 1 damage
        public bool WeaponDiceLess { get; init; }

        public int Decoys { get; init; }

        public int EasesPerLongRest { get; init; }

        // lands when the spell ends on the creature rather than when it is cast: Haste's lethargy
        public bool OnEnd { get; init; }

        // for an area: the caster's own side is left out - SRD's "creatures of your choice".
        // (on the zone itself this says the same about the zone's pulses)
        // SparesAllies above is read for both

        // done only when the spell is repeated on a later action, never on the cast itself:
        // Produce Flame's hurl
        public bool RepeatOnly { get; init; }

        // Chromatic Orb: when two of the damage dice match, the effect leaps to another creature
        // within this many squares of the one it hit, once per slot level
        public int Leaps { get; init; }

        // extra dice on a hit by cantrip tier - none, then 5, 11, 17: True Strike's radiant
        public IReadOnlyList<DiceRoll> ExtraTiers { get; init; } = Array.Empty<DiceRoll>();

        // for a zone: centred on the caster's square wherever the aim was - Call Lightning's cloud
        // above you, the Globe of Invulnerability
        public bool OnCaster { get; init; }

        // the square this effect is aimed at has to be under the spell's zone: Call Lightning's
        // bolts fall only under the cloud
        public bool WithinZone { get; init; }

        // more dice when the fight's setting has this tag: Call Lightning's storm
        public string BonusIf { get; init; } = "";

        public DiceRoll BonusAmount { get; init; }

        // for damage with a save: a creature that fails spends its reaction moving as far as its
        // speed allows away from the caster (Murmur of Dread)
        public bool ReactionFlee { get; init; }

        // for a wall: how many squares across its ring is (the block it closes round); 0 is a
        // wall that can only be straight
        public int RingSize { get; init; }

        // for a wall: how far beside it, on the side the caster picks, it still reaches -
        // Wall of Fire's 10 feet
        public int Beside { get; init; }

        // for an effect that reaches the zone: only on the wall itself, not the ground beside it
        public bool CoreOnly { get; init; }

        // for a zone: it acts every time, not once per turn - Wall of Fire
        public bool EachTime { get; init; }

        // for a zone: three-quarters cover to a creature behind it (+5)
        public int Cover { get; init; }

        // for a zone: its outline becomes walls on the board - "bars" (seen and shot through,
        // never walked through) or "solid" - Forcecage's cage and box
        public Edge Encloses { get; init; }

        // for a shift of the caster: magical travel - a creature inside a Forcecage has to make a
        // Charisma save first
        public bool Teleports { get; init; }

        // for a shift of an area's creatures: pushed this many squares straight away from the
        // caster - Thunderwave's 10 feet
        public int Push { get; init; }

        // for a conjure: which item, and how many
        public string Item { get; init; } = "";

        public int Count { get; init; }

        // for a sway that reaches the zone: it is on whoever stands inside, for as long as they
        // stand there - Pass without Trace's aura
        public bool WhileInside { get; init; }

        // for a zone: only the caster's own side - "you and each creature you choose"
        public bool AlliesOnly { get; init; }

        // for a direct: the word
        public Command Command { get; init; }

        // for an afflict: while it holds, the creature Dashes away from the caster at the start of
        // each of its turns by the safest route - Fear
        public bool Flees { get; init; }

        // for an afflict: it drops what it holds as the condition lands - Fear
        public bool Disarms { get; init; }

        // for a repeat save: only when the creature ends its turn out of the caster's line of
        // sight - Fear
        public bool RepeatUnseen { get; init; }

        // for a disarm: the caster's spellcasting ability check against the creature's check with
        // this ability - Telekinesis' contest with the holder's Strength
        public Ability? Contest { get; init; }

        // only a creature of this size or smaller: Telekinesis' "Huge or smaller"
        public Size? MaxSize { get; init; }

        // the repeating part has one target at a time: picking a new one ends it on the old one.
        // Telekinesis
        public bool Switches { get; init; }

        // for a zone: it has to be put down on an empty square - Flaming Sphere
        public bool Unoccupied { get; init; }

        // for a repeating shift of the zone: it rolls square by square and stops at the first
        // creature in its way, which it rams (the zone's "ram" pulse) - Flaming Sphere
        public bool Rams { get; init; }

        // for a relieve: every reduction to an ability score ends - Greater Restoration
        public bool RestoresAbilities { get; init; }

        // for a heal: it works on the dead, bringing them back with the amount - Raise Dead's 1
        public bool Revives { get; init; }

        // a creature that can't see is unaffected: Hypnotic Pattern's "who can see the pattern"
        public bool NeedsSight { get; init; }

        // for an afflict: the creature cannot end the condition itself - Hideous Laughter's
        // "it can't end the Prone condition on itself"
        public bool Pinned { get; init; }

        // creatures with one of these tags succeed on the save without rolling: Flesh to Stone's
        // Construct
        public IReadOnlyList<string> AutoSaveTags { get; init; } = Array.Empty<string>();

        // for a dispel: only curses, all of them, whatever their level - Remove Curse
        public bool Curses { get; init; }

        // for a sway: a rewrite of these weapons (Shillelagh's club and quarterstaff) - they swing
        // with the caster's spellcasting ability and roll RewriteDie, or the die for the caster's
        // cantrip tier when RewriteDieTiers is given (d8, d10, d12, 2d6)
        public IReadOnlyList<string> Weapons { get; init; } = Array.Empty<string>();

        public DiceRoll RewriteDie { get; init; }

        public IReadOnlyList<DiceRoll> RewriteDieTiers { get; init; } = Array.Empty<DiceRoll>();

        public DamageType RewriteDamageType { get; init; }

        // for a zone: more radius per slot level above the spell's own - Fog Cloud's 20 feet
        public int RadiusPerExtraLevel { get; init; }

        // for a zone: it moves this many squares straight away from its caster at the start of the
        // caster's turn - Cloudkill's 10 feet
        public int Drifts { get; init; }

        // for a sway: size categories up or down - Enlarge's one larger, Reduce's one smaller
        public int SizeStep { get; init; }

        // for a zone: spells of this level or lower cast from outside cannot affect anything
        // inside - Globe of Invulnerability's 5, one higher per slot level above its own
        public int BlocksSpellsUpTo { get; init; }

        public DiceRoll AmountAt(int spellLevel, int castAt, int casterLevel)
        {
            DiceRoll amount = Amount;

            if (CantripScaling && spellLevel == 0)
            {
                int tiers = CantripTiers(casterLevel);

                return new DiceRoll(amount.Count * (1 + tiers), amount.Die, amount.Modifier);
            }

            int extra = Math.Max(0, castAt - spellLevel);

            if (extra == 0 || PerExtraLevel.IsNothing) return amount;

            return new DiceRoll(amount.Count + PerExtraLevel.Count * extra,
                                amount.RollsAnything ? amount.Die : PerExtraLevel.Die,
                                amount.Modifier + PerExtraLevel.Modifier * extra);
        }

        public int TargetsAt(int spellLevel, int castAt, int casterLevel = 1) =>
            Targets + ExtraTargetsPerLevel * Math.Max(0, castAt - spellLevel) +
            (Beams && spellLevel == 0 ? CantripTiers(casterLevel) : 0);

        public static int CantripTiers(int casterLevel) =>
            casterLevel >= 17 ? 3 : casterLevel >= 11 ? 2 : casterLevel >= 5 ? 1 : 0;

        public override string ToString() =>
            $"{Kind.Id()} {Reach.Id()}" +
            (Amount.IsNothing ? "" : $" {Amount}") +
            (DamageType == DamageType.None ? "" : $" {DamageType.Id()}") +
            (Condition == Condition.None ? "" : $" {Condition.Id()}") +
            (Save.HasValue ? $" save {Save.Value.Id()} {OnSave.Id()}" : "") +
            (AttackRoll ? " attack" : "") +
            (Radius > 0 ? $" r{Radius}" : "") +
            (Length > 0 ? $" {Length}" + (Width > 0 ? $"x{Width}" : "") : "");
    }

    public sealed class Spell
    {
        public Spell(string id, int level, School school, IReadOnlyList<SpellEffect> effects,
                     int range = 0, bool concentration = false, bool ritual = false,
                     bool approximated = false, IReadOnlyList<string> classes = null,
                     CastingTime castingTime = CastingTime.Action, Trigger? trigger = null,
                     CastingTime? repeat = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Level = Math.Clamp(level, 0, 9);
            School = school;
            Effects = effects ?? Array.Empty<SpellEffect>();
            Range = Math.Max(0, range);
            Concentration = concentration;
            Ritual = ritual;
            Approximated = approximated;
            Classes = classes ?? Array.Empty<string>();
            CastingTime = castingTime;
            Trigger = trigger;
            Repeat = repeat;
        }

        public string Id { get; }

        // 0 is a cantrip, and a cantrip is at-will
        public int Level { get; }

        public School School { get; }

        // squares. 0 is touch or self
        public int Range { get; }

        public bool Concentration { get; }

        public bool Ritual { get; }

        // one of the eight that ship as a bounded approximation (v1_spell_list.md)
        public bool Approximated { get; }

        public CastingTime CastingTime { get; }

        // for a reaction spell, the moment it answers. null for everything cast on a turn
        public Trigger? Trigger { get; }

        public bool IsReaction => CastingTime == CastingTime.Reaction;

        // cast only in answer to a moment the fight offers - a reaction, or a smite's bonus action
        public bool Answers => Trigger.HasValue;

        // the modes the caster picks between at the cast, if any: Blindness/Deafness
        public IReadOnlyList<string> Modes =>
            Effects.Select(e => e.Mode).Where(m => m.Length > 0).Concat(Shapes).Distinct().ToList();

        // SRD calls it a curse: Remove Curse ends it, whatever its level. Hex, Bestow Curse
        public bool Curse { get; init; }

        // a cantrip whose range doubles at 5, 11 and 17: Spare the Dying's 15 feet
        public bool RangeScales { get; init; }

        // how long a spell that repeats without concentration stays repeatable: Produce Flame's
        // flame. Instant for everything else
        public Duration Lasts { get; init; } = Duration.Instant;

        // a weapon attack the spell makes: True Strike. it needs a weapon chosen at the cast
        public bool Strikes => Effects.Any(e => e.Kind == Primitive.Strike);

        // shapes a wall may be put down in, picked at the cast like a mode: "line" or "ring"
        public IReadOnlyList<string> Shapes { get; init; } = Array.Empty<string>();

        // not an SRD 5.2.1 spell at all - on the v1 list, but ships under an original name like an
        // approximation does (decisions_checklist.md section 1, 2026-09-24): Murmur of Dread,
        // Wyrmbreath Boon
        public bool NotInSrd { get; init; }

        // what must not be shown under an SRD spell's name
        public bool Renamed => Approximated || NotInSrd;

        // --- 2026-09-25, the SRD check -------------------------------------------------------------

        // a reaction spell that also answers being targeted by this spell: Shield's Magic Missile
        public string AnswersSpell { get; init; } = "";

        // a casting time of a minute or more - Identify, Raise Dead, Foresight. v1 casts these out
        // of a fight only, where the minute passes in the telling
        public bool OutOfCombat { get; init; }

        // cast at this level or higher it needs no concentration: Major Image at 4+. 0 is never
        public int ConcentrationBelow { get; init; }

        // casting it again ends the one already cast: Foresight
        public bool EndsPrevious { get; init; }

        // its repeat moves it to a new creature, and only once the one it is on has dropped to 0
        // hit points: Hex, Hunter's Mark
        public bool MovesWhenDown { get; init; }

        // a creation of magical force, which Disintegrate destroys: Wall of Force, Forcecage
        public bool ForceCreation { get; init; }

        // the save DC is 8 + proficiency + this ability's modifier, whoever casts it: a species'
        // own attack (the Dragonborn's Breath Weapon uses Constitution)
        public Ability? DcAbility { get; init; }

        public int RangeAt(int casterLevel) =>
            RangeScales && IsCantrip ? Range << SpellEffect.CantripTiers(casterLevel) : Range;

        // what a later turn spends to do the repeating part again, for as long as the caster
        // holds the spell. null for a spell that happens once
        public CastingTime? Repeat { get; }

        public IReadOnlyList<SpellEffect> Effects { get; }

        // which class lists it appears on
        public IReadOnlyList<string> Classes { get; }

        public bool IsCantrip => Level == 0;

        public bool Upcastable => !IsCantrip && Effects.Any(
            e => !e.PerExtraLevel.IsNothing || e.ExtraTargetsPerLevel > 0);

        public int MaxRadius => Effects.Count == 0 ? 0 : Effects.Max(e => e.Radius);

        // what the CAST needs aiming at - an effect done only on a later action (a breath, a hurl)
        // is aimed then, not now
        public bool NeedsATargetSquare =>
            Effects.Any(e => !e.RepeatOnly && e.Reach.NeedsATargetSquare());

        // a line, a cone or a cube: it has to be pointed, at a square or in a direction
        public bool NeedsADirection => Effects.Any(e => !e.RepeatOnly && e.Reach.IsDirected());

        public bool Attacks => Effects.Any(e => e.AttackRoll);

        public Ability? SavedAgainst => Effects.FirstOrDefault(e => e.Save.HasValue)?.Save;

        public bool Does(Primitive primitive) => Effects.Any(e => e.Kind == primitive);

        public string NameKey => KeyConventions.SpellName(Id);

        public string DescriptionKey => KeyConventions.SpellDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;
        }


        public override string ToString() =>
            $"{Id} (level {Level} {School.ToString().ToLowerInvariant()}" +
            (Concentration ? ", concentration" : "") + $"): " +
            string.Join(" + ", Effects);
    }

    public static class Schools
    {
        public static readonly IReadOnlyList<School> All = new[]
        {
            School.Abjuration, School.Conjuration, School.Divination, School.Enchantment,
            School.Evocation, School.Illusion, School.Necromancy, School.Transmutation,
        };

        public static string Id(this School school) => school.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out School school)
        {
            foreach (School s in All)
            {
                if (!string.Equals(s.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                school = s;
                return true;
            }

            school = School.Evocation;
            return false;
        }

        public static string Id(this Duration duration) =>
            duration switch
            {
                Duration.NextTurn => "next_turn",
                Duration.NextTurnEnd => "next_turn_end",
                Duration.TurnEnd => "turn_end",
                Duration.LongRest => "long_rest",
                _ => duration.ToString().ToLowerInvariant(),
            };

        public static bool TryParse(string id, out Duration duration)
        {
            foreach (Duration d in new[]
                     {
                         Duration.Instant, Duration.Concentration,
                         Duration.Encounter, Duration.Rest, Duration.NextTurn,
                         Duration.NextTurnEnd, Duration.TurnEnd, Duration.LongRest,
                     })
            {
                if (!string.Equals(d.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                duration = d;
                return true;
            }

            duration = Duration.Instant;
            return false;
        }
    }
}
