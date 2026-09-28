using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Core.Magic
{
    // ONE EFFECT MEETING ONE CREATURE, after the checks every primitive shares: whether it could be
    // touched at all, a Globe in the way, the attack roll or the saving throw. what a primitive's
    // handler is given to do its part (IPrimitiveHandler.Apply)
    public sealed class Contact
    {
        internal Contact(Incantation magic, IResolver resolver, Caster caster, Spell spell,
                         SpellEffect effect, Aim aim, Actor target, int castAt, Encounter fight,
                         Moment answering, DiceRoll amount, int modifier, Attempt attempt, bool landed,
                         int dc)
        {
            Magic = magic;
            Resolver = resolver;
            Caster = caster;
            Spell = spell;
            Effect = effect;
            Aim = aim;
            Target = target;
            CastAt = castAt;
            Fight = fight;
            Answering = answering;
            Amount = amount;
            Modifier = modifier;
            Attempt = attempt;
            Landed = landed;
            Dc = dc;
        }

        internal Incantation Magic { get; }

        public IResolver Resolver { get; }

        public Caster Caster { get; }

        public Spell Spell { get; }

        public SpellEffect Effect { get; }

        public Aim Aim { get; }

        public Actor Target { get; }

        public int CastAt { get; }

        // null off the board: a spell cast in the campaign's story
        public Encounter Fight { get; }

        // the moment a reaction or a smite answers, when it answers one
        public Moment Answering { get; }

        // the amount at the level it was cast, and Cure Wounds' modifier on top
        public DiceRoll Amount { get; }

        public int Modifier { get; }

        // the attack roll or the saving throw, when there was one
        public Attempt Attempt { get; }

        // what the roll said: a hit, a failed save, or nothing to roll
        public bool Landed { get; }

        // the caster's save DC for this spell
        public int Dc { get; }

        // a save that was made stops it: the first thing most primitives ask
        public bool Resisted => !Landed && Effect.OnSave != OnSave.None;

        // it changed nothing on the creature; the report still says how the roll went
        public Landing Report(bool landed) => new Landing(Effect, Target, landed, 0, Attempt);
    }
}
