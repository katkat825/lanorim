using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Words;

namespace Core.Magic
{
    // one primitive with its numbers filled in. a spell is a list of these, and that list is the
    // whole of what the spell does. the settings every primitive may use are here; the ones only
    // one primitive reads are in that primitive's part of this class (SpellEffect.<Name>.cs, beside
    // its handler in Primitives/), and what the effect leaves on a creature is its BoonSpec (a sway)
    // and its LingerSpec. docs/spell_effect_reference.md is the vocabulary.
    public sealed partial class SpellEffect
    {
        public SpellEffect(Primitive primitive,
                           AimKind aim = AimKind.Creature,
                           DiceRoll amount = default,
                           DamageType damageType = DamageType.None,
                           Condition condition = Condition.None,
                           Ability? save = null,
                           OnSave onSave = OnSave.None,
                           bool attackRoll = false,
                           Duration duration = Duration.Instant,
                           int radius = 0,
                           int targets = 1,
                           string note = null,
                           int length = 0,
                           int width = 0)
        {
            Kind = primitive;
            AimKind = aim;
            Amount = amount;
            DamageType = damageType;
            Condition = condition;
            Save = save;
            OnSave = onSave;
            AttackRoll = attackRoll;
            Duration = duration;
            Radius = Math.Max(0, radius);
            Targets = Math.Max(1, targets);
            Note = note ?? "";
            Length = Math.Max(0, length);
            Width = Math.Max(0, width);
        }

        public Primitive Kind { get; }

        // who or what it lands on: 'aim' in the data
        public AimKind AimKind { get; }

        // damage, healing, temporary hit points, Aid's raise - whatever this primitive counts in
        public DiceRoll Amount { get; }

        // what casting it higher adds, per slot level above the spell's own
        public Upcast Upcast { get; init; } = Upcast.Nothing;

        public DamageType DamageType { get; }

        // the damage type is the caster's pick at the cast, from these: Chromatic Orb. a choice
        // left to the caster is the list of what may be chosen
        public IReadOnlyList<DamageType> DamageChoices { get; init; } = Array.Empty<DamageType>();

        public bool ChosenDamageType => DamageChoices.Count > 0;

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

        // free text for the bounded approximations; never shown to a player
        public string Note { get; }

        // for a spell with modes: this effect belongs to one of them, chosen at the cast.
        // Blindness/Deafness, Enlarge/Reduce, Greater Restoration's list
        public string Mode { get; init; } = "";

        // for a sway: the boon it puts on, whole - what it adds, which rolls, which way it leans,
        // what it forbids. read by content/Schema/BoonSpecReader.cs, the same reader a class stance
        // and an item use. its Duration is this effect's
        public BoonSpec Boon { get; init; } = BoonSpec.Nothing;

        // what it leaves on a creature it lands on, and how that ends: the way out, the save
        // track, what damage does to it. read by content/Spells/LingerSpecReader.cs
        public LingerSpec Linger { get; init; } = LingerSpec.Nothing;

        // when it lands: now, later, each turn, when the spell ends, on the repeats
        public Lands Lands { get; init; }

        // how a cantrip grows at caster levels 5, 11 and 17
        public CantripGrowth CantripGrowth { get; init; }

        // add the caster's spellcasting modifier to the amount, once: Cure Wounds' 2d8 + mod
        public bool AddsModifier { get; init; }


        // --- who it reaches -------------------------------------------------------------------------

        // how many burst centres one casting gets: Meteor Swarm's four. a creature caught in
        // more than one is still caught once
        public int Points { get; init; } = 1;

        // an area that touches only so many of the creatures in it: Slow's "up to six"
        public int UpTo { get; init; }

        // which creatures in an area or a zone count
        public Affects Affects { get; init; }

        // what a creature's tags do to it: Hold Person's Humanoid only, Sleep's sleepless untouched
        public IReadOnlyList<TagRule> TagRules { get; init; } = Array.Empty<TagRule>();

        // only a creature on one side of a hit point line: Power Word Stun's 150. null is anyone
        public HitPointGate HitPoints { get; init; }

        // a creature that can't see is unaffected: Hypnotic Pattern's "who can see the pattern"
        public bool NeedsSight { get; init; }

        // only a creature of this size or smaller: Telekinesis' "Huge or smaller"
        public Size? MaxSize { get; init; }

        // lands only on a creature the effect before it landed on: Guiding Bolt's glimmer comes
        // with a hit and not with a miss
        public bool Follows { get; init; }

        // for an effect that reaches "zone": the moments its zone does it
        public Pulses Pulses { get; init; }

        // for an effect that reaches the zone: only on the wall itself, not the ground beside it
        public bool CoreOnly { get; init; }

        // the repeating part has one target at a time: picking a new one ends it on the old one.
        // Telekinesis
        public bool Switches { get; init; }

        // ends every zone of magical darkness this effect's area touches: Sunburst
        public bool DispelsDarkness { get; init; }


        // --- the save --------------------------------------------------------------------------------

        // the same saving throw as the effect before it, not a second one: Meteor Swarm's fire
        // and bludgeoning are one Dexterity save
        public bool SameSave { get; init; }

        // a willing target (the caster's own side) is not asked to save: Enlarge/Reduce
        public bool SaveIfUnwilling { get; init; }

        // the target saves with advantage when it is being fought - Charm Person's "if you or
        // your allies are fighting it", which on a board is any hostile target
        public bool AdvantageIfFought { get; init; }

        // a failed save costs the target its concentration: Sleet Storm
        public bool BreaksConcentration { get; init; }


        public DiceRoll AmountAt(int spellLevel, int castAt, int casterLevel)
        {
            DiceRoll amount = Amount;

            if (CantripGrowth == CantripGrowth.Dice && spellLevel == 0)
            {
                int tiers = CantripTiers(casterLevel);

                return new DiceRoll(amount.Count * (1 + tiers), amount.Die, amount.Modifier);
            }

            int extra = Math.Max(0, castAt - spellLevel);
            DiceRoll more = Upcast.Amount;

            if (extra == 0 || more.IsNothing) return amount;

            return new DiceRoll(amount.Count + more.Count * extra,
                                amount.RollsAnything ? amount.Die : more.Die,
                                amount.Modifier + more.Modifier * extra);
        }

        public int TargetsAt(int spellLevel, int castAt, int casterLevel = 1) =>
            Targets + Upcast.Targets * Math.Max(0, castAt - spellLevel) +
            (CantripGrowth == CantripGrowth.Beams && spellLevel == 0 ? CantripTiers(casterLevel) : 0);

        public static int CantripTiers(int casterLevel) =>
            casterLevel >= 17 ? 3 : casterLevel >= 11 ? 2 : casterLevel >= 5 ? 1 : 0;

        public override string ToString() =>
            $"{Kind.Id()} {AimKind.Id()}" +
            (Amount.IsNothing ? "" : $" {Amount}") +
            (DamageType == DamageType.None ? "" : $" {DamageType.Id()}") +
            (Condition == Condition.None ? "" : $" {Condition.Id()}") +
            (Save.HasValue ? $" save {Save.Value.Id()} {OnSave.Id()}" : "") +
            (AttackRoll ? " attack" : "") +
            (Radius > 0 ? $" r{Radius}" : "") +
            (Length > 0 ? $" {Length}" + (Width > 0 ? $"x{Width}" : "") : "");
    }
}
