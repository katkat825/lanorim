using System;
using Core.Localization;
using Core.Words;

namespace Core.Characters
{
    // a boon riding on an actor: Bless's +1d4, Shield's +5 AC, a Bane, Guidance, a ring's +1. what
    // it IS is its BoonSpec; this is only what happened when it was put on - whose it is, where it
    // came from, what was rolled, and how much of it is left. spells, items and class features all
    // make these, which is why it lives here and not in Core.Magic. Boon.Of is the one way to make
    // one (a test holds that).
    public sealed class Boon
    {
        Boon(string id, string source, BoonSpec spec, Actor owner, int raisedMaximum)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Source = source ?? id;
            Spec = spec ?? BoonSpec.Nothing;
            Owner = owner;
            MaxHitPoints = raisedMaximum;
            Decoys = Spec.Decoys;
        }

        // spec is what the boon is. owner is whose turn ends a turn-shaped one and whose hits a
        // mark pays out on; raisedMaximum is Aid's rolled raise
        public static Boon Of(BoonSpec spec, string id, string source = null, Actor owner = null,
                              int raisedMaximum = 0) =>
            new Boon(id, source, spec, owner, raisedMaximum);

        public string Id { get; }

        // the spell or feature that put it there; ending concentration takes every boon with the
        // same source off at once
        public string Source { get; }

        public BoonSpec Spec { get; }

        public Duration Duration => Spec.Duration;

        // WHOSE TURN ENDS IT, for the two turn-shaped durations. null is the bearer's own - a
        // Shield on yourself. a Guiding Bolt's glimmer sits on the target and ends on the caster's
        // turn, which is why it is a separate thing from who is wearing it. a mark's owner is the
        // caster whose hits it pays out on
        public Actor Owner { get; }

        // hit point maximum raised (and current hit points with it) while it lasts: Aid's rolled 5
        public int MaxHitPoints { get; }

        // Mirror Image's duplicates, counted down as they are destroyed: the one part of a boon
        // that changes after it is put on
        public int Decoys { get; set; }

        // the fight has seen the start of the owner's next turn, so the owner's next turn end
        // is the one that counts
        internal bool Armed { get; set; }

        public string NameKey => KeyConventions.FeatureName(Id);

        public bool TouchesCheck(Skill skill) =>
            Spec.Checks && (Spec.Skill == Skill.None || Spec.Skill == skill);

        public bool TouchesSave(Ability ability) => Spec.Saves && With(ability);

        // an advantage or disadvantage on a check, narrowed by the boon's skill and ability
        public bool LeansOnCheck(Ability ability, Skill skill) =>
            (Spec.Skill == Skill.None || Spec.Skill == skill) && With(ability);

        // a roll made with this ability is one it reaches: every roll, unless the boon names one
        public bool With(Ability? used) => !Spec.Ability.HasValue || Spec.Ability == used;

        public override string ToString() =>
            Id + (Spec.Flat != 0 ? $" {Spec.Flat:+0;-0}" : "") +
            (Spec.Dice.IsNothing ? "" : $" +{Spec.Dice}") +
            (Spec.ArmorClass != 0 ? $" ac {Spec.ArmorClass:+0;-0}" : "") +
            $" ({EnumWords.Name(Duration)})";
    }
}
