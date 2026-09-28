using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Dice;

namespace Game.Dice
{
    public sealed partial class DieSolid
    {
        // a tetrahedron rests on a vertex with no upward face, so the upward normal is noise
        // apex-read: a corner's number is on the face opposite it against the felt, so the code reads downward and the player reads the apex
        static DieSolid Tetrahedron()
        {
            Vector3[] v =
            {
                new(1, 1, 1),
                new(1, -1, -1),
                new(-1, 1, -1),
                new(-1, -1, 1),
            };

            // face i is the one opposite vertex i, so its outward normal points away from it
            var faces = new (Vector3 Normal, int Value)[v.Length];
            for (int i = 0; i < v.Length; i++) faces[i] = (-v[i].Normalized(), i + 1);

            return FromNormals(Die.D4, v, faces, DieFaceTable.ReadFrom.DownwardFace, AtCorners);
        }

        static DieSolid Cube()
        {
            var v = new List<Vector3>();
            foreach (int x in new[] { -1, 1 })
                foreach (int y in new[] { -1, 1 })
                    foreach (int z in new[] { -1, 1 })
                        v.Add(new Vector3(x, y, z));

            var faces = new (Vector3, int)[]
            {
                (Vector3.Up, 1),
                (Vector3.Down, 6),
                (Vector3.Right, 3),
                (Vector3.Left, 4),
                (Vector3.Back, 2),
                (Vector3.Forward, 5),
            };

            return FromNormals(Die.D6, v.ToArray(), faces, DieFaceTable.ReadFrom.UpwardFace, AtFaceCentres);
        }

        static DieSolid Octahedron()
        {
            Vector3[] v =
            {
                Vector3.Right, Vector3.Left,
                Vector3.Up, Vector3.Down,
                Vector3.Back, Vector3.Forward,
            };

            // each face faces a corner of the cube, and opposite corners sum to nine. WHICH corner
            // gets which number is left to Numbered, for the reason the d20's comment gives at
            // length: the hand-written table this replaced put 1, 2, 3 and 4 on the +x side and
            // measured 0.657 lopsided, twice what an octahedron can manage.
            var corners = new List<Vector3>();

            foreach (int x in new[] { -1, 1 })
                foreach (int y in new[] { -1, 1 })
                    foreach (int z in new[] { -1, 1 })
                        corners.Add(new Vector3(x, y, z));

            return FromNormals(Die.D8, v, Numbered(corners, 9),
                               DieFaceTable.ReadFrom.UpwardFace, AtFaceCentres);
        }

        // the ring offset c is not free: a kite's corners are coplanar only with the apex at (3 + 4*phi) times it, or the faces bow
        static DieSolid Trapezohedron()
        {
            const float h = 1f;
            float c = h / (3f + 4f * ((1f + Mathf.Sqrt(5f)) / 2f));

            var v = new List<Vector3> { new(0, h, 0), new(0, -h, 0) };

            // upper ring at 0, 72, 144 ... lower ring offset by 36, so they interleave
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Tau * i / 5f;
                v.Add(new Vector3(Mathf.Cos(a), c, Mathf.Sin(a)));
            }

            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Tau * (i + 0.5f) / 5f;
                v.Add(new Vector3(Mathf.Cos(a), -c, Mathf.Sin(a)));
            }

            int Upper(int i) => 2 + i % 5;
            int Lower(int i) => 7 + i % 5;

            // face i on top is opposite face i+2 underneath, which is what makes the pairs sum to eleven
            int[] under = { 7, 6, 10, 9, 8 };

            var faces = new List<(int[] Ring, int Value)>();

            for (int i = 0; i < 5; i++)
                faces.Add((new[] { 0, Upper(i), Lower(i), Upper(i + 1) }, i + 1));

            for (int i = 0; i < 5; i++)
                faces.Add((new[] { 1, Lower(i), Upper(i + 1), Lower(i + 1) }, under[i]));

            return FromPolygons(Die.D10, v.ToArray(), faces.ToArray(),
                                DieFaceTable.ReadFrom.UpwardFace, AtFaceCentres);
        }

        // Twenty faces. The face normals are DERIVED FROM THE VERTICES rather than tabulated:
        // every three mutually adjacent corners are a face, and its normal is the way their
        // middle points. Writing them out by hand is a cyclic rotation waiting to happen - the
        // first attempt at this listed the dodecahedron's vertices in the wrong rotation and got
        // twenty normals that each pointed at a corner, which the rim finder caught but only
        // because it checks. Derived, it cannot be the wrong way round.
        //
        // The numbering is generated too: opposite faces sum to 21, which is the rule a real d20
        // obeys and the only one a player can check without a protractor.
        static DieSolid Icosahedron()
        {
            float phi = (1f + Mathf.Sqrt(5f)) / 2f;

            var v = new List<Vector3>();

            foreach (int a in new[] { -1, 1 })
                foreach (int b in new[] { -1, 1 })
                {
                    v.Add(new Vector3(0, a, b * phi));
                    v.Add(new Vector3(a, b * phi, 0));
                    v.Add(new Vector3(a * phi, 0, b));
                }

            // the shortest distance between any two corners is the edge; on a solid this regular
            // the next distance up is nowhere near it
            float edge = float.PositiveInfinity;

            for (int i = 0; i < v.Count; i++)
                for (int j = i + 1; j < v.Count; j++)
                    edge = Mathf.Min(edge, v[i].DistanceTo(v[j]));

            float slack = edge * 0.01f;

            bool Adjacent(int i, int j) => Mathf.Abs(v[i].DistanceTo(v[j]) - edge) < slack;

            var normals = new List<Vector3>();

            for (int i = 0; i < v.Count; i++)
                for (int j = i + 1; j < v.Count; j++)
                    for (int k = j + 1; k < v.Count; k++)
                        if (Adjacent(i, j) && Adjacent(j, k) && Adjacent(i, k))
                            normals.Add((v[i] + v[j] + v[k]).Normalized());

            if (normals.Count != 20)
                throw new InvalidOperationException(
                    $"An icosahedron has twenty faces and this one found {normals.Count}.");

            return FromNormals(Die.D20, v.ToArray(), Numbered(normals, 21),
                               DieFaceTable.ReadFrom.UpwardFace, AtFaceCentres);
        }

        // NUMBERING A DIE IS NOT THE SAME AS PAIRING IT UP, and getting that wrong is measurable.
        //
        // Opposite faces summing to 21 is necessary and nowhere near sufficient. The first version
        // of this walked the normals in the order they were generated, which put 1, 2, 3 and 4 on
        // one side of the solid and 17, 18, 19 and 20 on the other - a whole hemisphere of low
        // numbers. A 4000-throw sweep caught it: the faces themselves were fine (chi-squared 30.6
        // on 19 degrees of freedom, barely over the 5% line) but the MEAN was 2.88 standard errors
        // high, because any small directional bias in the throw lands on a hemisphere, and on that
        // die a hemisphere meant "high".
        //
        // A real d20 scatters its numbers so no direction is worth anything. This does the same
        // thing by measurement rather than by tradition: of the 2^n ways to decide which end of
        // each opposite pair takes the low number, take the one whose value-weighted normals sum
        // closest to zero. Exhaustive at n = 10 (1024 of them), so it is deterministic and there
        // is no seed to remember.
        static (Vector3 Normal, int Value)[] Numbered(IReadOnlyList<Vector3> normals, int opposite)
        {
            // NORMALIZED BEFORE ANYTHING IS COMPARED. A dot product only says "opposite" at -1 for
            // unit vectors; on the d8's raw cube corners, (-1,-1,-1) dotted with (-1,1,1) is -1
            // too, and the pairing silently put four faces opposite the wrong four. It built and
            // ran - the faces were all there, just paired wrong - and only the lopsidedness
            // measurement gave it away.
            var axes = new List<Vector3>();

            foreach (Vector3 raw in normals)
            {
                Vector3 normal = raw.Normalized();

                if (!axes.Any(a => a.Dot(normal) < -0.999f)) axes.Add(normal);
            }

            if (axes.Count * 2 != normals.Count)
                throw new InvalidOperationException(
                    $"{normals.Count} face normals made {axes.Count} opposite pairs; every face " +
                    "on these solids has exactly one opposite.");

            int pairs = axes.Count;

            // WHICH AXIS GETS WHICH NUMBER MATTERS AS MUCH AS WHICH WAY ROUND IT GOES, and for a
            // small die it matters more: signs alone take the d12 from 0.575 to 0.454, and adding
            // the order takes it to 0.078. So permute when the factorial is small enough to walk
            // and fall back to signs alone when it is not - at ten pairs that is 3.6 million
            // orders, and the d20 reaches 0.093 on signs alone anyway.
            int[] order = Enumerable.Range(0, pairs).ToArray();
            int[] bestOrder = (int[])order.Clone();

            int best = 0;
            float bestPull = float.PositiveInfinity;

            foreach (int[] tried in pairs <= PermuteUpTo ? Orders(order) : new[] { order })
                for (int choice = 0; choice < 1 << pairs; choice++)
                {
                    float pull = Pull(axes, tried, choice, opposite);

                    if (pull >= bestPull) continue;

                    bestPull = pull;
                    best = choice;
                    bestOrder = (int[])tried.Clone();
                }

            var faces = new List<(Vector3, int)>();

            for (int i = 0; i < pairs; i++)
            {
                int low = bestOrder[i] + 1;
                bool flip = (best & (1 << i)) != 0;

                faces.Add((flip ? -axes[i] : axes[i], low));
                faces.Add((flip ? axes[i] : -axes[i], opposite - low));
            }

            return faces.ToArray();
        }

        // 8! is forty thousand and 9! is three hundred and sixty; the line is drawn where walking
        // them all still costs nothing at load
        const int PermuteUpTo = 7;

        // every ordering of the axes, in place, Heap's algorithm
        static IEnumerable<int[]> Orders(int[] order)
        {
            int n = order.Length;
            var counters = new int[n];

            yield return order;

            int i = 0;

            while (i < n)
            {
                if (counters[i] >= i)
                {
                    counters[i] = 0;
                    i++;
                    continue;
                }

                int swap = i % 2 == 0 ? 0 : counters[i];

                (order[swap], order[i]) = (order[i], order[swap]);

                counters[i]++;
                i = 0;

                yield return order;
            }
        }

        // how hard the numbering pulls in any one direction: each face's normal weighted by how
        // far its value is from the middle, all summed. zero means no direction is worth more than
        // any other, which is the property a physical die is trying to have
        static float Pull(IReadOnlyList<Vector3> axes, int[] order, int choice, int opposite)
        {
            var sum = Vector3.Zero;
            float middle = (opposite + 1) / 2f;

            for (int i = 0; i < axes.Count; i++)
            {
                float low = order[i] + 1 - middle;
                bool flip = (choice & (1 << i)) != 0;

                // the opposite face carries the opposite weight by construction, so one term per
                // pair says it: +low on one end, -low on the other
                sum += (flip ? -axes[i] : axes[i]) * low * 2f;
            }

            return sum.Length();
        }

        // the tens die of a percentile pair: the same ten-sided solid, numbered 00 to 90. Its
        // faces carry 0, 10, 20 ... 90, so a throw of the pair adds to 1-100 with the 00+0 case
        // read as 100, the way it is read at a table.
        public static DieSolid Percentile()
        {
            lock (Cache)
            {
                if (_percentile != null) return _percentile;

                DieSolid units = For(Die.D10);

                var facets = units.Facets
                    .Select(f => new Facet(f.Normal, f.Centre, f.Ring,
                                           (f.Value % 10) * 10, f.Inradius))
                    .ToArray();

                Numeral[] numerals = facets
                    .Select(f => new Numeral(f.Centre, f.Normal, Vector3.Zero, f.Value,
                                             f.Inradius * PercentileHeight))
                    .ToArray();

                _percentile = new DieSolid(Die.D10, units.Vertices, facets,
                                           DieFaceTable.ReadFrom.UpwardFace, numerals);

                return _percentile;
            }
        }

        static DieSolid _percentile;

        // two digits in the space of one, so the glyphs come down to fit the face
        const float PercentileHeight = 0.62f;

        static DieSolid Dodecahedron()
        {
            float phi = (1f + Mathf.Sqrt(5f)) / 2f;
            float inv = 1f / phi;

            var v = new List<Vector3>();

            foreach (int x in new[] { -1, 1 })
                foreach (int y in new[] { -1, 1 })
                    foreach (int z in new[] { -1, 1 })
                        v.Add(new Vector3(x, y, z));

            foreach (int a in new[] { -1, 1 })
                foreach (int b in new[] { -1, 1 })
                {
                    v.Add(new Vector3(0, a * inv, b * phi));
                    v.Add(new Vector3(a * inv, b * phi, 0));
                    v.Add(new Vector3(a * phi, 0, b * inv));
                }

            // the order inside each triple matters: the usual icosahedron vertices give normals that
            // touch a corner, not a face. Which face takes which number is Numbered's, same as the
            // d8 and the d20 - the hand table this replaced measured 0.575 lopsided against the
            // 0.078 a dodecahedron can reach, the worst of the set.
            var normals = new List<Vector3>();

            foreach (int a in new[] { -1, 1 })
                foreach (int b in new[] { -1, 1 })
                {
                    normals.Add(new Vector3(0, a * phi, b));
                    normals.Add(new Vector3(a, 0, b * phi));
                    normals.Add(new Vector3(a * phi, b, 0));
                }

            return FromNormals(Die.D12, v.ToArray(), Numbered(normals, 13),
                               DieFaceTable.ReadFrom.UpwardFace, AtFaceCentres);
        }
    }
}
