using System.Collections.Generic;
using Core.Characters;

namespace Core.Magic
{
    // RELIEVE: a condition taken away - Lesser Restoration - or every reduction to an ability score
    // (Greater Restoration)
    public sealed class RelieveHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Relieve;

        public bool Helps(SpellEffect effect) => true;

        public bool KindWhateverElse => true;

        public IReadOnlyList<string> Keys { get; } = new[] { "restores_abilities" };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.Condition == Condition.None && !effect.RestoresAbilities)
                yield return "a relieve effect with no 'condition'";
        }

        public Landing Apply(Contact c)
        {
            SpellEffect effect = c.Effect;
            Actor target = c.Target;

            if (effect.RestoresAbilities)
                return new Landing(effect, target, target.Scores.Restore());

            bool removed = target.Remove(effect.Condition);

            // the spell that put it there is over on this creature too
            c.Magic.Unplace(target, effect.Condition);

            return new Landing(effect, target, removed, 0, null, effect.Condition);
        }
    }
}
