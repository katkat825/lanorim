using Core.Characters;
using Core.Dice;

namespace Core.Combat
{
    // HIT POINTS OFF A CREATURE, AS THE TABLE SAYS IT: who did it, with what (a weapon's or a spell's
    // name key), the dice, what they came to and what the target actually took (resistance, a save
    // for half). A Blow is a weapon's and a Landing a spell's; this is the one shape the log reads for
    // both, so a Fire Bolt and a greataxe say their damage the same way.
    public sealed class Harm
    {
        public Harm(Actor by, Actor target, string whatKey, DiceRoll dice, int rolled, int suffered,
                    DamageType type)
        {
            By = by;
            Target = target;
            WhatKey = whatKey ?? "";
            Dice = dice;
            Rolled = rolled;
            Suffered = suffered;
            Type = type;
        }

        public Actor By { get; }

        public Actor Target { get; }

        public string WhatKey { get; }

        public DiceRoll Dice { get; }

        public int Rolled { get; }

        public int Suffered { get; }

        public DamageType Type { get; }

        public override string ToString() => $"{By?.Id} {WhatKey} {Dice} -> {Rolled}, {Target?.Id} takes {Suffered} {Type}";
    }
}
