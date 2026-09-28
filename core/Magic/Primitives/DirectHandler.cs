using System.Collections.Generic;
using Core.Characters;

namespace Core.Magic
{
    // DIRECT: the creature's next turn is decided for it, from a closed list of words: Command.
    // the word is obeyed at the start of its turn (Incantation.Obey)
    public sealed class DirectHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Direct;

        public IReadOnlyList<string> Keys { get; } = new[] { "command" };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.Command == Command.None) yield return "a direct says its 'command'";
        }

        public Landing Apply(Contact c)
        {
            if (c.Resisted) return new Landing(c.Effect, c.Target, false, 0, c.Attempt);

            // done at the start of its next turn, and then it is over
            c.Magic.Place(new Placement
            {
                Target = c.Target,
                Caster = c.Caster,
                Spell = c.Spell.Id,
                Level = c.CastAt,
                Dc = c.Dc,
                Duration = Duration.NextTurnEnd,
                Owner = c.Target,
                Command = c.Effect.Command,
            }, c.Fight);

            if (c.Fight != null) c.Magic.Watch(c.Fight);

            return new Landing(c.Effect, c.Target, true, 0, c.Attempt);
        }
    }
}
