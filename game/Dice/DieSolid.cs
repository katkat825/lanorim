using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Dice;

namespace Game.Dice
{
    // generated, not an asset, so the mesh, hull, numerals and face table are one description that can't silently disagree
    public sealed partial class DieSolid
    {
        public readonly struct Facet
        {
            public readonly Vector3 Normal;

            public readonly Vector3 Centre;

            // counter-clockwise seen from outside
            public readonly int[] Ring;

            public readonly int Value;

            // centre to nearest edge - sets how big the numeral can be
            public readonly float Inradius;

            public Facet(Vector3 normal, Vector3 centre, int[] ring, int value, float inradius)
            {
                Normal = normal;
                Centre = centre;
                Ring = ring;
                Value = value;
                Inradius = inradius;
            }
        }

        public readonly struct Numeral
        {
            public readonly Vector3 Position;
            public readonly Vector3 Facing;

            // zero lets DieParts centre it on the face; a d4 needs it pointing at the corner
            public readonly Vector3 Up;

            public readonly int Value;

            // em size of the glyph, in metres - a digit renders about 0.7 of it
            public readonly float Height;

            public Numeral(Vector3 position, Vector3 facing, Vector3 up, int value, float height)
            {
                Position = position;
                Facing = facing;
                Up = up;
                Value = value;
                Height = height;
            }
        }

        // sized by circumradius, not edge length or volume, so the six shapes read as one ~50mm set
        // the d6 is the exact cube the tuning and fairness numbers were measured on - don't drift it
        static readonly Dictionary<Die, float> Radius = new()
        {
            [Die.D4] = 0.0380f,
            [Die.D6] = 0.0250f * 1.7320508f,
            [Die.D8] = 0.0425f,
            [Die.D10] = 0.0400f,
            [Die.D12] = 0.0350f,

            // the money shot (ART_DIRECTION section 6): the one the player throws thousands of
            // times, so it is the largest of the set by a hair, the way a real d20 is
            [Die.D20] = 0.0380f,
        };

        readonly Facet[] _facets;

        DieSolid(Die size, Vector3[] vertices, Facet[] facets, DieFaceTable.ReadFrom readFrom, Numeral[] numerals)
        {
            Size = size;
            Vertices = vertices;
            _facets = facets;
            ReadFrom = readFrom;
            Numerals = numerals;

            Circumradius = vertices.Max(v => v.Length());
            Volume = MeasureVolume(vertices, facets);
            MinFlatAlignment = MeasureFlatness(facets);
            Lopsidedness = MeasureLopsidedness(facets);

            // every face from 1 to n, once each. cheap, and it catches a numbering that dropped or
            // doubled a value - which is what a mis-paired opposite looks like from the outside
            int[] values = facets.Select(f => f.Value).OrderBy(x => x).ToArray();

            if (!values.SequenceEqual(Enumerable.Range(1, facets.Length)))
                throw new InvalidOperationException(
                    $"A {size.Label()} carries the faces {string.Join(", ", values)}, which is not " +
                    $"1 to {facets.Length} once each.");

            // A DIE WHOSE HIGH NUMBERS ARE ALL ON ONE SIDE IS A DIE THAT ROLLS HIGH, as soon as
            // anything about the throw favours a direction - and something always does.
            //
            // Reported, not refused, because HOW LOW THIS CAN GO IS A PROPERTY OF THE SHAPE and
            // not of the numbering. A cube cannot get under 0.657 at all: three orthogonal axes
            // have nothing to cancel against, so the ordinary 1-opposite-6 numbering everybody's
            // dice use is already the best a d6 can do. A d12 can reach 0.078. An absolute limit
            // would therefore condemn a perfect cube and wave a bad dodecahedron through, which is
            // worse than useless - so the number goes in the log and a sweep decides.
        }

        // the value-weighted sum of the face normals, over the biggest it could be. 0 is perfectly
        // balanced; 1 would be every high number on one side and every low number on the other
        public float Lopsidedness { get; }

        static float MeasureLopsidedness(Facet[] facets)
        {
            float middle = (facets.Min(f => f.Value) + facets.Max(f => f.Value)) / 2f;

            var sum = Vector3.Zero;
            float most = 0f;

            foreach (Facet facet in facets)
            {
                sum += facet.Normal.Normalized() * (facet.Value - middle);
                most += Mathf.Abs(facet.Value - middle);
            }

            return most <= 0f ? 0f : sum.Length() / most;
        }

        public Die Size { get; }

        // corners, in the die's own space, centred on the origin
        public Vector3[] Vertices { get; }

        public IReadOnlyList<Facet> Facets => _facets;

        public DieFaceTable.ReadFrom ReadFrom { get; }

        public Numeral[] Numerals { get; }

        public float Circumradius { get; }

        // cubic metres - DieBody turns this into mass, so a set behaves like one material
        public float Volume { get; }

        // the alignment below which this shape cannot be lying flat
        // per shape because one fixed number would miss the d10's shallow edge or reject flat d4s
        public float MinFlatAlignment { get; }

        // the same normals the mesh was built from, by construction
        public DieFaceTable FaceTable() =>
            new(_facets.Select(f => new DieFaceTable.Face(f.Normal, f.Value)).ToArray(), ReadFrom);

        static readonly Dictionary<Die, DieSolid> Cache = new();

        // cached - the geometry is immutable and every die of a size shares it
        public static DieSolid For(Die size)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(size, out DieSolid solid)) return solid;

                solid = size switch
                {
                    Die.D4 => Tetrahedron(),
                    Die.D6 => Cube(),
                    Die.D8 => Octahedron(),
                    Die.D10 => Trapezohedron(),
                    Die.D12 => Dodecahedron(),
                    Die.D20 => Icosahedron(),

                    // a d100 is two d10s on a real table, and it is two here too: throw
                    // Percentile() for the tens and DieSolid.For(Die.D10) for the units
                    Die.D100 => throw new ArgumentOutOfRangeException(
                            nameof(size), size,
                            "A d100 is thrown as two dice: DieSolid.Percentile() and a d10."),

                    _ => throw new ArgumentOutOfRangeException(
                            nameof(size), size, "No solid for that die size."),
                };

                Cache[size] = solid;
                return solid;
            }
        }
    }
}
