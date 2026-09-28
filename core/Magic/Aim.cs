using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Space;

namespace Core.Magic
{
    // where a spell was aimed. a spell wants creatures, a square, a direction, or none of them.
    // a line or a cone pointed at a square is thrown toward it (Template.Toward), which is how a
    // player aims one with a click; a facing is for everything that is not a click.
    public sealed class Aim
    {
        public Aim(IEnumerable<Actor> creatures = null, Cell? square = null, Facing? facing = null,
                   Skill skill = Skill.None, Ability? ability = null,
                   IEnumerable<Cell> squares = null, string mode = null,
                   DamageType damageType = DamageType.None)
        {
            Mode = mode ?? "";
            DamageType = damageType;
            Creatures = (creatures ?? Enumerable.Empty<Actor>()).Where(a => a != null).ToList();
            Squares = (squares ?? (square.HasValue ? new[] { square.Value } : System.Array.Empty<Cell>()))
                .ToList();
            Square = square ?? (Squares.Count > 0 ? Squares[0] : (Cell?)null);
            Facing = facing;
            Skill = skill;
            Ability = ability;
        }

        // every square picked, for a spell with more than one burst centre. Square is the first
        public IReadOnlyList<Cell> Squares { get; }

        // what the caster chose as they cast: Guidance's skill, Hex's ability
        public Skill Skill { get; }

        public Ability? Ability { get; }

        public Aim Choosing(Skill skill) =>
            new Aim(Creatures, Square, Facing, skill, Ability, Squares, Mode, DamageType)
            { Weapon = Weapon };

        public Aim Choosing(Ability ability) =>
            new Aim(Creatures, Square, Facing, Skill, ability, Squares, Mode, DamageType)
            { Weapon = Weapon };

        // which of a spell's modes: "blindness" or "deafness", "enlarge" or "reduce"
        public string Mode { get; }

        public Aim Choosing(string mode) =>
            new Aim(Creatures, Square, Facing, Skill, Ability, Squares, mode, DamageType)
            { Weapon = Weapon, Side = Side };

        // which side of a wall is its business side: for a straight wall, left or right of the
        // way it runs; for a ring, Left is inside and Right outside. Wall of Fire's burning side
        public Side Side { get; init; }

        public Aim On(Side side) =>
            new Aim(Creatures, Square, Facing, Skill, Ability, Squares, Mode, DamageType)
            { Weapon = Weapon, Side = side };

        // the damage type the caster picked: Chromatic Orb's
        public DamageType DamageType { get; }

        public Aim Choosing(DamageType type) =>
            new Aim(Creatures, Square, Facing, Skill, Ability, Squares, Mode, type) { Weapon = Weapon };

        // the weapon a spell swings: True Strike's
        public Attack Weapon { get; init; }

        public Aim With(Attack weapon) =>
            new Aim(Creatures, Square, Facing, Skill, Ability, Squares, Mode, DamageType)
            { Weapon = weapon };

        public static Aim OnMany(params Cell[] squares) =>
            new Aim(null, null, null, Skill.None, null, squares);

        public IReadOnlyList<Actor> Creatures { get; }

        public Cell? Square { get; }

        public Facing? Facing { get; }

        public static Aim At(params Actor[] creatures) => new Aim(creatures);

        public static Aim On(Cell square) => new Aim(null, square);

        public static Aim Toward(Facing facing) => new Aim(null, null, facing);

        public static readonly Aim Nothing = new Aim();
    }
}
