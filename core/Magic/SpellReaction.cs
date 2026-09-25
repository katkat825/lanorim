using System;
using System.Linq;
using Core.Characters;
using Core.Combat;

namespace Core.Magic
{
    // a reaction spell, as something the fight can offer. the window asks whether it can answer -
    // the spell is on the sheet, there is something to pay with, and the moment is one it is for
    // and in its range - and the caster's chooser decides whether it does. what it then does is
    // the spell's primitives and nothing else: there is no Shield class and no Counterspell class.
    public sealed class SpellReaction : IReaction
    {
        readonly Caster _caster;
        readonly Incantation _incantation;

        public SpellReaction(Caster caster, Spell spell, Incantation incantation)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            Spell = spell ?? throw new ArgumentNullException(nameof(spell));
            _incantation = incantation ?? throw new ArgumentNullException(nameof(incantation));

            if (!spell.Trigger.HasValue)
                throw new ArgumentException($"{spell.Id} is not cast in answer to anything",
                                            nameof(spell));
        }

        public Spell Spell { get; }

        // what the last answer did, for whoever wants to show it
        public Casting Last { get; private set; }

        public string Id => Spell.Id;

        public Trigger Trigger => Spell.Trigger.Value;

        // a smite is a bonus action taken in answer to your own hit; everything else a reaction
        public Spend Cost => Spell.CastingTime == CastingTime.BonusAction ? Spend.Bonus : Spend.Reaction;

        // armor class the spell puts on its own caster: Shield's five. read off the primitives,
        // not the spell's name
        public int Deflects =>
            Spell.Effects.Where(e => e.Kind == Primitive.Sway && e.Reach == Reach.Caster &&
                                     (e.Touches & Sways.ArmorClass) != 0)
                 .Sum(e => e.Sway);

        public bool AlsoAnswers(Moment moment) =>
            moment != null && moment.Trigger == Trigger.Targeted && Spell.AnswersSpell.Length > 0 &&
            moment.Spell == Spell.AnswersSpell;

        public bool CanAnswer(Encounter fight, Actor reactor, Moment moment)
        {
            if (moment == null) return false;

            // Shield: being targeted by the one spell it names (Magic Missile)
            bool targeted = moment.Trigger == Trigger.Targeted && Spell.AnswersSpell.Length > 0 &&
                            moment.Spell == Spell.AnswersSpell;

            if (moment.Trigger != Trigger && !targeted) return false;

            if (!ReferenceEquals(reactor, _caster.Actor)) return false;

            if (!_caster.Knows(Spell.Id) || !_caster.CanCast(Spell, Spell.Level)) return false;

            switch (moment.Trigger)
            {
                // being hit, being hurt, being targeted: only ever about you
                case Trigger.Hit:
                case Trigger.Damaged:
                case Trigger.Targeted:
                    if (!ReferenceEquals(moment.Target, reactor)) return false;
                    break;

                case Trigger.LeaveReach:
                    if (!ReferenceEquals(moment.Target, reactor)) return false;
                    break;

                // your own melee hit - a weapon or an unarmed strike, never a shot or a spell
                case Trigger.Struck:
                    if (!ReferenceEquals(moment.Source, reactor)) return false;
                    if (moment.Blow?.Attack == null || moment.Blow.Attack.IsRanged) return false;
                    return true;
            }

            // a spell that reaches something other than its caster has to reach the one who
            // caused the moment: Counterspell's sixty feet, Hellish Rebuke's sixty feet
            bool outward = Spell.Effects.Any(e => e.Reach != Reach.Caster);

            if (outward && fight != null &&
                !fight.Field.InRange(reactor, moment.Source, Math.Max(1, Spell.Range)))
                return false;

            // Counterspell: "when you see a creature ... casting a spell"
            if (moment.Trigger == Trigger.Cast && fight != null && !fight.Sees(reactor, moment.Source))
                return false;

            return true;
        }

        public void Answer(Encounter fight, Actor reactor, Moment moment) =>
            Last = _incantation.Answer(_caster, Spell, moment, fight);

        public override string ToString() => $"{Id} ({Trigger.ToString().ToLowerInvariant()})";
    }
}
