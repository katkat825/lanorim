using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Space;

namespace Core.Magic
{
    // A SPELL'S PERSISTENT AREA, AS THE FIGHT SEES IT. made by a Zone effect when the spell is
    // cast on a board; what it does to a creature is the spell's effects that reach "zone", each
    // at the moments its own `pulses` name. nothing about any particular spell is in here - Spirit
    // Guardians and Moonbeam are both this class with different data.
    // which side of a wall is its business side (Wall of Fire's burning side)
    public enum Side
    {
        None,
        Left,
        Right,
    }

    public sealed class SpellZone : IZone
    {
        readonly Incantation _incantation;

        internal SpellZone(Incantation incantation, Caster caster, Spell spell, SpellEffect area,
                           int castAt, Cell? centre)
        {
            _incantation = incantation;
            Caster = caster;
            Spell = spell;
            Area = area;
            CastAt = castAt;
            Centre = centre;

            Acts = spell.Effects.Where(e => e.Reach == Reach.Zone).ToList();
            Pulses = Acts.Aggregate(Pulses.None, (all, e) => all | e.Pulses);
        }

        public Caster Caster { get; }

        public Spell Spell { get; }

        // the Zone effect itself: its shape and size, whether it is rough, whom it spares
        public SpellEffect Area { get; }

        public int CastAt { get; }

        // null when the zone goes wherever its owner goes - an emanation
        public Cell? Centre { get; private set; }

        public IReadOnlyList<SpellEffect> Acts { get; }

        public string Source => Spell.Id;

        public Actor Owner => Caster.Actor;

        public Pulses Pulses { get; }

        public bool Rough => Area.Rough;

        public bool Ground => Area.Ground;

        public bool SparesAllies => Area.SparesAllies;

        public Obscurement Obscures => Area.Obscures;

        public bool Magical => Area.MagicalDarkness;

        public bool AlliesOnly => Area.AlliesOnly;

        // the sways it lays on whoever stands inside
        public IEnumerable<SpellEffect> Auras => Acts.Where(e => e.WhileInside);

        public int BlocksSpellsUpTo =>
            Area.BlocksSpellsUpTo > 0
                ? Area.BlocksSpellsUpTo + Math.Max(0, CastAt - Spell.Level)
                : 0;

        public bool FollowsOwner => !Centre.HasValue;

        // Fog Cloud grows by 20 feet a slot level
        public int Radius =>
            Math.Max(0, Area.Radius + Area.RadiusPerExtraLevel * Math.Max(0, CastAt - Spell.Level));

        // how a wall was put down: the way it runs, the side that is its business, and whether it
        // is a ring rather than straight
        public Facing Facing { get; init; } = Facing.East;

        public Side Side { get; init; }

        public bool Ring { get; init; }

        public IEnumerable<Cell> Squares(Battlefield field)
        {
            if (Area.Reach == Reach.Wall) return WallSquares(field).Concat(BesideSquares(field));

            Cell? at = Centre ?? field.Where(Owner);

            if (!at.HasValue) return Enumerable.Empty<Cell>();

            return Area.Reach == Reach.Square
                ? field.Square(at.Value, Math.Max(1, Area.Length))
                : field.Burst(at.Value, Radius);
        }

        public bool Covers(Battlefield field, Cell cell) => Squares(field).Contains(cell);

        public bool Core(Battlefield field, Cell cell) =>
            Area.Reach == Reach.Wall ? WallSquares(field).Contains(cell) : Covers(field, cell);

        public int Cover => Area.Cover;

        // --- walls ------------------------------------------------------------------------------

        static (int dx, int dy) Step(Facing facing) => facing switch
        {
            Facing.North => (0, -1),
            Facing.South => (0, 1),
            Facing.West => (-1, 0),
            _ => (1, 0),
        };

        // the wall itself: a run of Length squares from the centre the way it faces, or the
        // outline of a RingSize block with the centre at its top-left corner
        public IEnumerable<Cell> WallSquares(Battlefield field)
        {
            if (!Centre.HasValue) yield break;

            Cell start = Centre.Value;

            if (Ring)
            {
                int side = Math.Max(2, Area.RingSize);
                start = Corner(side);

                for (int y = 0; y < side; y++)
                    for (int x = 0; x < side; x++)
                        if (x == 0 || y == 0 || x == side - 1 || y == side - 1)
                        {
                            var cell = new Cell(start.X + x, start.Y + y);
                            if (field.Map.Contains(cell)) yield return cell;
                        }

                yield break;
            }

            (int dx, int dy) = Step(Facing);

            for (int i = 0; i < Math.Max(1, Area.Length); i++)
            {
                var cell = new Cell(start.X + dx * i, start.Y + dy * i);
                if (field.Map.Contains(cell)) yield return cell;
            }
        }

        // the top-left square of a block `side` squares across, centred where the zone was put
        // down - the same block Template.Square covers, so a cage and its square zone agree
        Cell Corner(int side)
        {
            int from = -(side - 1) / 2;

            return new Cell(Centre.Value.X + from, Centre.Value.Y + from);
        }

        // the whole block an enclosure closes round: Forcecage's cage or box
        public bool Within(Cell cell)
        {
            if (!Centre.HasValue) return false;

            int side = Math.Max(1, Area.RingSize);
            Cell corner = Corner(side);

            return cell.X >= corner.X && cell.X < corner.X + side && cell.Y >= corner.Y && cell.Y < corner.Y + side;
        }

        // every pulse, not once a turn: Wall of Fire burns on the way in and again at the end
        public bool EachTime => Area.EachTime;

        // the squares strictly inside a ring
        public bool Inside(Battlefield field, Cell cell)
        {
            if (!Centre.HasValue) return false;

            int side = Math.Max(2, Area.RingSize);
            Cell corner = Corner(side);

            return cell.X > corner.X && cell.X < corner.X + side - 1 && cell.Y > corner.Y && cell.Y < corner.Y + side - 1;
        }

        // the ground beside the wall it still reaches, on the chosen side: a straight wall's left
        // or right, a ring's inside (Left) or outside (Right)
        public IEnumerable<Cell> BesideSquares(Battlefield field)
        {
            if (Area.Beside <= 0 || Side == Side.None || !Centre.HasValue) return Enumerable.Empty<Cell>();

            var wall = new HashSet<Cell>(WallSquares(field));
            var beside = new HashSet<Cell>();

            if (Ring)
            {
                foreach (Cell w in wall)
                    foreach (Cell near in field.Burst(w, Area.Beside))
                        if (!wall.Contains(near) && Inside(field, near) == (Side == Side.Left))
                            beside.Add(near);

                return beside;
            }

            (int dx, int dy) = Step(Facing);

            // left of the way it runs is a quarter turn anticlockwise
            (int lx, int ly) = Side == Side.Left ? (dy, -dx) : (-dy, dx);

            foreach (Cell w in wall)
                for (int i = 1; i <= Area.Beside; i++)
                {
                    var cell = new Cell(w.X + lx * i, w.Y + ly * i);
                    if (field.Map.Contains(cell) && !wall.Contains(cell)) beside.Add(cell);
                }

            return beside;
        }

        // Forcecage: the ring's outline, as board edges, and what they were before
        IReadOnlyDictionary<Border, Edge> _was;

        internal void Enclose(Battlefield field)
        {
            if (!Centre.HasValue) return;

            int side = Math.Max(1, Area.RingSize);
            Cell corner = Corner(side);
            var borders = new List<Border>();

            for (int i = 0; i < side; i++)
            {
                borders.Add(Border.North(new Cell(corner.X + i, corner.Y)));
                borders.Add(Border.South(new Cell(corner.X + i, corner.Y + side - 1)));
                borders.Add(Border.West(new Cell(corner.X, corner.Y + i)));
                borders.Add(Border.East(new Cell(corner.X + side - 1, corner.Y + i)));
            }

            _was = field.Raise(borders, Area.Encloses);
        }

        public void Removed(Encounter fight)
        {
            if (_was == null) return;

            fight.Field.Lower(_was);
            _was = null;
        }

        public void Act(Encounter fight, Actor creature, Pulse pulse) =>
            _incantation.Pulse(this, fight, creature, pulse);

        internal void MoveTo(Cell cell) => Centre = cell;

        public override string ToString() =>
            $"{Spell.Id} zone of {Owner.Id}" + (Centre.HasValue ? $" at {Centre}" : " around them") +
            $", {Pulses.Id()}" + (Rough ? ", rough" : "");
    }
}
