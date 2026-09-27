using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Dice;
using Core.Space;

namespace Content.Combat
{
    // WHAT A CHOICE WOULD DO, before it is made: the hit chance and expected damage, or the save
    // and whether a success halves it, and the squares a template would cover
    public sealed class Preview
    {
        public bool Legal { get; init; }

        public string WhyNotKey { get; init; }

        public IReadOnlyList<Actor> Targets { get; init; } = Array.Empty<Actor>();

        // 0 to 1; -1 when there is no attack roll
        public double HitChance { get; init; } = -1;

        public int? SaveDc { get; init; }

        public Ability? SaveAbility { get; init; }

        public bool HalfOnSave { get; init; }

        // the chance the (first) target fails its save
        public double FailChance { get; init; } = -1;

        public DiceRoll Damage { get; init; }

        public double ExpectedDamage { get; init; }

        public IReadOnlyList<Cell> Template { get; init; } = Array.Empty<Cell>();

        public Facing? Facing { get; init; }
    }
}
