using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Magic
{
    // what casting a spell did, or why it could not be cast. the one thing Incantation.Cast returns
    public sealed class Casting
    {
        public Casting(Spell spell, Actor caster, int castAt, bool cast, string refusal = null,
                       IReadOnlyList<Landing> landings = null, IReadOnlyList<Cell> squares = null,
                       IReadOnlyList<Cell> covered = null, bool countered = false)
        {
            Spell = spell;
            Caster = caster;
            CastAt = castAt;
            Cast = cast;
            Refusal = refusal ?? "";
            Landings = landings ?? Array.Empty<Landing>();
            Squares = squares ?? Array.Empty<Cell>();
            Covered = covered ?? Array.Empty<Cell>();
            Countered = countered;
        }

        public Spell Spell { get; }

        public Actor Caster { get; }

        public int CastAt { get; }

        public bool Cast { get; }

        // why not, in engineer's English - never shown to a player, the UI greys the card instead
        public string Refusal { get; }

        public IReadOnlyList<Landing> Landings { get; }

        // the squares a zone, a wall or a light now covers
        public IReadOnlyList<Cell> Squares { get; }

        // the squares a line, a cone or a cube swept, whether anybody was standing in them or
        // not - what the board lights up so the player sees the shape that was thrown
        public IReadOnlyList<Cell> Covered { get; }

        // stopped mid-cast by somebody's reaction. the action is gone and the resource is not:
        // SRD 5.2.1's Counterspell wastes the casting, and the slot is not expended
        public bool Countered { get; }

        public int TotalDamage =>
            Landings.Where(l => l.Effect.Kind == Primitive.Damage).Sum(l => l.Amount);

        public int TotalHealing =>
            Landings.Where(l => l.Effect.Kind == Primitive.Heal).Sum(l => l.Amount);

        public IEnumerable<Actor> Touched => Landings.Select(l => l.Target).Where(a => a != null).Distinct();

        // the items the spell made, for the content layer to put in the caster's pack
        public IEnumerable<(string item, int count)> Conjured =>
            Landings.Where(l => l.Effect.Kind == Primitive.Conjure && l.Landed)
                    .Select(l => (l.Effect.Item.Id, l.Amount));

        public static Casting Refused(Spell spell, Actor caster, int castAt, string why) =>
            new Casting(spell, caster, castAt, false, why);

        public static Casting Stopped(Spell spell, Actor caster, int castAt) =>
            new Casting(spell, caster, castAt, false, "countered", countered: true);

        public override string ToString() =>
            Cast
                ? $"{Caster?.Id} casts {Spell?.Id} at level {CastAt}: " +
                  (Landings.Count == 0 ? "nothing to report" : string.Join("; ", Landings))
                : $"{Caster?.Id} cannot cast {Spell?.Id}: {Refusal}";
    }
}
