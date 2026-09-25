using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Core.Combat;
using Core.Localization;
using Core.Magic;

namespace Content.Spells
{
    // how far a spell reaches, as the card says it
    public enum RangeKind
    {
        // it happens to or around the caster: Shield, Burning Hands, Spirit Guardians
        Self,

        // range 0 aimed at somebody else: Cure Wounds
        Touch,

        // a number of feet
        Feet,
    }

    // EVERYTHING A SPELL CARD SHOWS, AS KEYS AND NUMBERS. the card in Godot (EB Garamond, laid out
    // by Kathleen) is a set of labels over this and decides nothing: which words, which numbers,
    // whether it is greyed out and why, all come from here - so a card cannot disagree with the
    // spell it is a card for.
    //
    // a key with a {0} in its English (the range in feet, the size of the area, the level of the
    // slot) is shown with the matching number from this card filled in.
    public sealed class SpellCard
    {
        SpellCard(Spell spell) => Spell = spell;

        public Spell Spell { get; }

        public string NameKey => Spell.NameKey;

        public string DescriptionKey => Spell.DescriptionKey;

        public int Level => Spell.Level;

        public bool IsCantrip => Spell.IsCantrip;

        public string LevelKey { get; private set; }

        public string SchoolKey { get; private set; }

        public string CastingTimeKey { get; private set; }

        public RangeKind RangeKind { get; private set; }

        public string RangeKey { get; private set; }

        public int RangeFeet { get; private set; }

        // the biggest area the spell makes, or null when it makes none
        public string AreaKey { get; private set; }

        public int AreaFeet { get; private set; }

        public bool Concentration => Spell.Concentration;

        public bool Ritual => Spell.Ritual;

        // A LANORIM VERSION, NOT THE SRD'S. true for the bounded approximations, which already
        // carry a name of their own (decisions_checklist.md section 1); the card says so as well,
        // because CC-BY asks that changes be indicated and a renamed spell is still a changed one
        public bool Adapted => Spell.Approximated;

        // --- what it asks of the caster, when there is one ------------------------------------

        public bool RollsToHit => Spell.Attacks;

        public int? AttackBonus { get; private set; }

        public Core.Characters.Ability? Save => Spell.SavedAgainst;

        public int? SaveDc { get; private set; }

        // castable right now at its own level. false with a reason when it is not, so the card is
        // greyed with something to say rather than just grey
        public bool Castable { get; private set; }

        public string NotCastableKey { get; private set; }

        // what casting it costs in the mode this caster chose
        public string CostKey { get; private set; }

        public int CostAmount { get; private set; }

        // the levels it can be cast at right now, lowest to highest. a spell that does not scale
        // is only ever its own level, whatever the caster could pay for
        public int LowestLevel => Spell.Level;

        public int HighestLevel { get; private set; }

        public bool Upcastable => Spell.Upcastable;


        public static SpellCard Of(Spell spell, Caster caster = null)
        {
            if (spell == null) throw new ArgumentNullException(nameof(spell));

            var card = new SpellCard(spell)
            {
                LevelKey = spell.IsCantrip ? Ui("spell_level", "cantrip") : Ui("spell_level", "leveled"),
                SchoolKey = SchoolKeyOf(spell.School),
                CastingTimeKey = CastingTimeKeyOf(spell),
            };

            card.ReadRange(caster);
            card.ReadArea();

            if (caster != null) card.ReadCaster(caster);
            else card.HighestLevel = spell.Level;

            return card;
        }

        void ReadRange(Caster caster)
        {
            bool onlyAboutTheCaster = Spell.Effects.All(e => e.Reach == Reach.Caster ||
                                                             e.Reach == Reach.Around ||
                                                             e.Reach.IsDirected());

            if (Spell.Range == 0 && onlyAboutTheCaster)
            {
                RangeKind = RangeKind.Self;
                RangeKey = Ui("range", "self");
            }
            else if (Spell.Range == 0)
            {
                RangeKind = RangeKind.Touch;
                RangeKey = Ui("range", "touch");
            }
            else
            {
                RangeKind = RangeKind.Feet;
                RangeFeet = Spell.RangeAt(caster?.Actor.Level ?? 1) * Feet;
                RangeKey = Ui("range", "feet");
            }
        }

        void ReadArea()
        {
            (string shape, int feet) best = (null, 0);

            foreach (SpellEffect effect in Spell.Effects)
            {
                (string shape, int feet) area = effect.Reach switch
                {
                    Reach.Line => ("line", effect.Length * Feet),
                    Reach.Cone => ("cone", effect.Length * Feet),
                    Reach.Cube => ("cube", effect.Length * Feet),
                    Reach.Burst or Reach.Around when effect.Radius > 0 => ("radius", effect.Radius * Feet),
                    _ => (null, 0),
                };

                if (area.shape != null && area.feet > best.feet) best = area;
            }

            if (best.shape == null) return;

            AreaKey = Ui("area", best.shape);
            AreaFeet = best.feet;
        }

        void ReadCaster(Caster caster)
        {
            if (RollsToHit) AttackBonus = caster.AttackModifier;

            if (Save.HasValue) SaveDc = caster.SaveDc;

            Castable = caster.CanCast(Spell, Spell.Level);

            if (!Castable)
                NotCastableKey = !caster.Knows(Spell.Id) ? Ui("not_castable", "not_known")
                               : caster.Actor.IsShifted ? Ui("not_castable", "shifted")
                               : Spell.Answers ? Ui("not_castable", "reaction")
                               : Ui("not_castable", "spent");

            // a reaction spell is never cast from the card - the fight offers it - but whether it
            // could be answered with right now is still worth showing, so it reads as castable
            // when it is paid for and says it waits for its moment
            if (Spell.Answers && caster.Knows(Spell.Id) && !caster.Actor.IsShifted &&
                caster.CanCast(Spell, Spell.Level))
            {
                Castable = true;
                NotCastableKey = null;
            }

            HighestLevel = Spell.Level;

            if (Upcastable)
                for (int level = Spell.Level + 1; level <= SpellLevels.Highest; level++)
                    if (caster.CanCast(Spell, level)) HighestLevel = level;

            if (Spell.IsCantrip)
            {
                CostKey = Ui("spell_cost", "at_will");
            }
            else if (caster.Mode == SpellResourceMode.Points)
            {
                CostKey = Ui("spell_cost", "points");
                CostAmount = SpellPoints.CostOf(Spell.Level);
            }
            else
            {
                CostKey = Ui("spell_cost", "slot");
                CostAmount = Spell.Level;
            }
        }

        const int Feet = 5;

        static string Ui(string subject, string aspect) =>
            KeyConventions.Key(KeyConventions.UiNs, subject, aspect, "name");

        public static string SchoolKeyOf(School school) => Ui("school", school.Id());

        public static string CastingTimeKeyOf(Spell spell) =>
            spell.Answers
                ? KeyConventions.Key(KeyConventions.UiNs, "casting_time", spell.CastingTime.Id(),
                                     "name", spell.Trigger.Value.Id())
                : Ui("casting_time", spell.CastingTime.Id());

        // every key a card can show. derived, like the rest of EngineKeys, so a new school or a
        // new trigger is owed its English the day it exists
        public static IEnumerable<string> Keys()
        {
            yield return Ui("spell_level", "cantrip");
            yield return Ui("spell_level", "leveled");

            foreach (School school in Schools.All) yield return SchoolKeyOf(school);

            yield return Ui("casting_time", CastingTime.Action.Id());
            yield return Ui("casting_time", CastingTime.BonusAction.Id());

            // a smite answers your own hit with a bonus action; everything else is a reaction
            foreach (Trigger trigger in Triggers.All)
                yield return KeyConventions.Key(KeyConventions.UiNs, "casting_time",
                                                trigger == Trigger.Struck
                                                    ? CastingTime.BonusAction.Id()
                                                    : CastingTime.Reaction.Id(),
                                                "name", trigger.Id());

            foreach (string range in new[] { "self", "touch", "feet" }) yield return Ui("range", range);

            foreach (string shape in new[] { "line", "cone", "cube", "radius" })
                yield return Ui("area", shape);

            foreach (string cost in new[] { "at_will", "points", "slot" })
                yield return Ui("spell_cost", cost);

            foreach (string why in new[] { "not_known", "shifted", "reaction", "spent" })
                yield return Ui("not_castable", why);

            yield return Ui("spell_card", "adapted");
        }

        public override string ToString() =>
            $"{Spell.Id}: level {Level}, {RangeKind}" + (RangeFeet > 0 ? $" {RangeFeet} ft" : "") +
            (AreaKey != null ? $", {AreaFeet} ft {AreaKey}" : "") +
            (CostKey != null ? $", {CostKey} {CostAmount}" : "") +
            (Castable ? "" : $", not castable ({NotCastableKey})");
    }
}
