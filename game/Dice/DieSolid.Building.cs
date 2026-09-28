using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Dice;

namespace Game.Dice
{
    public sealed partial class DieSolid
    {
        delegate Numeral[] Numbering(Vector3[] vertices, Facet[] facets, float radius);

        // a face's rim is every vertex furthest along its normal, so a typo yields a missing face, not a subtly wrong one
        static DieSolid FromNormals(
            Die size, Vector3[] vertices, (Vector3 Normal, int Value)[] faces,
            DieFaceTable.ReadFrom readFrom, Numbering numbering)
        {
            var polygons = faces
                .Select(f => (Ring: RimAlong(vertices, f.Normal.Normalized()), f.Value))
                .ToArray();

            return FromPolygons(size, vertices, polygons, readFrom, numbering);
        }

        static DieSolid FromPolygons(
            Die size, Vector3[] vertices, (int[] Ring, int Value)[] faces,
            DieFaceTable.ReadFrom readFrom, Numbering numbering)
        {
            float radius = Radius[size];
            float scale = radius / vertices.Max(v => v.Length());
            Vector3[] scaled = vertices.Select(v => v * scale).ToArray();

            var facets = new Facet[faces.Length];

            for (int i = 0; i < faces.Length; i++)
            {
                int[] ring = (int[])faces[i].Ring.Clone();

                Vector3 centre = Average(ring.Select(k => scaled[k]));
                Vector3 normal = (scaled[ring[1]] - scaled[ring[0]])
                    .Cross(scaled[ring[2]] - scaled[ring[0]]).Normalized();

                // flip to outward: every solid is centred on the origin, so the face centre points the way out
                if (normal.Dot(centre) < 0f)
                {
                    Array.Reverse(ring);
                    normal = -normal;
                }

                facets[i] = new Facet(normal, centre, ring, faces[i].Value, EdgeDistance(scaled, ring, centre));
            }

            return new DieSolid(size, scaled, facets, readFrom, numbering(scaled, facets, radius));
        }

        // vertices within a whisker of the furthest are the face; on solids this regular the next one in is nowhere near
        static int[] RimAlong(Vector3[] vertices, Vector3 normal)
        {
            float furthest = vertices.Max(v => v.Dot(normal));

            int[] on = Enumerable.Range(0, vertices.Length)
                .Where(i => vertices[i].Dot(normal) > furthest - 0.001f)
                .ToArray();

            if (on.Length < 3)
                throw new InvalidOperationException(
                    $"A face normal of {normal} touches {on.Length} vertices - that is a corner or an edge, not a face.");

            Vector3 centre = Average(on.Select(i => vertices[i]));

            Vector3 u = (vertices[on[0]] - centre).Normalized();
            Vector3 w = normal.Cross(u);

            return on
                .OrderBy(i => Mathf.Atan2((vertices[i] - centre).Dot(w), (vertices[i] - centre).Dot(u)))
                .ToArray();
        }

        static Numeral[] AtFaceCentres(Vector3[] vertices, Facet[] facets, float radius) =>
            facets
                .Select(f => new Numeral(f.Centre, f.Normal, Vector3.Zero, f.Value, f.Inradius))
                .ToArray();

        const float CornerInset = 0.50f;   // face middle towards the corner, as a share of the way

        const float CornerHeight = 0.90f;  // numeral size, as a share of the face's inradius

        // the d4's numbers sit in the corners (three per face) so the result shows at the top corner, not face-down on the felt
        static Numeral[] AtCorners(Vector3[] vertices, Facet[] facets, float radius)
        {
            var numerals = new List<Numeral>();

            foreach (Facet f in facets)
                foreach (int corner in f.Ring)
                {
                    // corner's value is facets[corner] - the face opposite it, the one on the felt when this corner is up
                    int value = facets[corner].Value;

                    Vector3 outward = vertices[corner] - f.Centre;

                    numerals.Add(new Numeral(
                        f.Centre + outward * CornerInset,
                        f.Normal,

                        // points at the corner it belongs to, so it reads upright when that corner is on top
                        outward.Normalized(),
                        value,
                        f.Inradius * CornerHeight));
                }

            return numerals.ToArray();
        }

        static Vector3 Average(IEnumerable<Vector3> points)
        {
            var sum = Vector3.Zero;
            int n = 0;

            foreach (Vector3 p in points) { sum += p; n++; }

            return sum / n;
        }

        static float EdgeDistance(Vector3[] vertices, int[] ring, Vector3 centre)
        {
            float nearest = float.PositiveInfinity;

            for (int i = 0; i < ring.Length; i++)
            {
                Vector3 a = vertices[ring[i]];
                Vector3 b = vertices[ring[(i + 1) % ring.Length]];
                nearest = Mathf.Min(nearest, centre.DistanceTo((a + b) * 0.5f));
            }

            return nearest;
        }

        // fans each face into triangles and sums the tetrahedra back to the origin
        static float MeasureVolume(Vector3[] vertices, Facet[] facets)
        {
            float total = 0f;

            foreach (Facet f in facets)
                for (int i = 1; i < f.Ring.Length - 1; i++)
                {
                    Vector3 a = vertices[f.Ring[0]];
                    Vector3 b = vertices[f.Ring[i]];
                    Vector3 c = vertices[f.Ring[i + 1]];
                    total += a.Dot(b.Cross(c));
                }

            return Mathf.Abs(total) / 6f;
        }

        // halfway, in dot product, between a face lying flat and the die on its shallowest edge
        static float MeasureFlatness(Facet[] facets)
        {
            float shallowest = -1f;

            for (int i = 0; i < facets.Length; i++)
                for (int j = 0; j < facets.Length; j++)
                {
                    if (i == j) continue;
                    shallowest = Mathf.Max(shallowest, facets[i].Normal.Dot(facets[j].Normal));
                }

            // half-angle: the cosine of half an angle whose cosine is d is sqrt((1+d)/2)
            float onEdge = Mathf.Sqrt((1f + shallowest) / 2f);

            return (onEdge + 1f) / 2f;
        }
    }
}
