using System;
using System.Collections.Generic;
using Core.Dice;

namespace Core.Magic
{
    // the settings only a strike reads. StrikeHandler declares their keys
    public sealed partial class SpellEffect
    {
        // extra dice on a hit by cantrip tier - none, then 5, 11, 17: True Strike's radiant
        public IReadOnlyList<DiceRoll> ExtraTiers { get; init; } = Array.Empty<DiceRoll>();
    }
}
