using System;
using Core.Dice;

namespace Content.Companions
{
    // WHERE THE COMPANION WANDERS, AND WHEN (cc_task_ui-issues-10-01.md 4). It stands on the table beyond
    // the map, beside the GM screen, and every so often it walks to another spot near its home: "unlike
    // minis, a companion is alive". Decided here, where it is tested; the figure on the table only walks it.
    //
    // In the table's own metres, seen from the default camera: +X is right, -Z is away from you. Every
    // spot is within Radius of home, never nearer the map than NearestZ (the map is toward +Z), and never
    // left of LeftmostX (where the GM screen stands). Home is pulled inside those lines if it isn't.
    public sealed class Roaming
    {
        readonly IRng _rng;

        public Roaming((double X, double Z) home, double radius, double leftmostX, double nearestZ, IRng rng,
                       double every = DefaultEvery, double jitter = DefaultJitter)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Radius = Math.Max(0, radius);
            LeftmostX = leftmostX;
            NearestZ = nearestZ;
            Every = Math.Max(0.5, every);
            Jitter = Math.Max(0, jitter);
            Home = Inside(home);
            At = Home;
            Wait();
        }

        public const double DefaultEvery = 9.0;
        public const double DefaultJitter = 4.0;

        public (double X, double Z) Home { get; }

        public double Radius { get; }

        public double LeftmostX { get; }

        public double NearestZ { get; }

        public double Every { get; }

        public double Jitter { get; }

        // where it stands, or where it is walking to
        public (double X, double Z) At { get; private set; }

        public bool Walking { get; private set; }

        // seconds it has stood still since it last arrived: the figure settles after a while
        public double Still { get; private set; }

        double _left;

        // time passes while it stands; true on the frame it sets off for a new spot (At)
        public bool Tick(double delta)
        {
            if (Walking) return false;

            Still += delta;
            _left -= delta;

            if (_left > 0) return false;

            At = Next();
            Walking = true;
            return true;
        }

        // it got there, or something (a mood, a gesture) stopped it where it stands
        public void Arrived((double X, double Z) where)
        {
            At = Inside(where);
            Walking = false;
            Still = 0;
            Wait();
        }

        // a spot within Radius of home, inside the lines: a random bearing and distance, the area even
        (double X, double Z) Next()
        {
            double angle = Unit() * 2 * Math.PI;
            double distance = Radius * Math.Sqrt(Unit());

            return Inside((Home.X + Math.Cos(angle) * distance, Home.Z + Math.Sin(angle) * distance));
        }

        (double X, double Z) Inside((double X, double Z) spot) =>
            (Math.Max(spot.X, LeftmostX), Math.Min(spot.Z, NearestZ));

        void Wait() => _left = Math.Max(0.5, Every + Jitter * (Unit() - 0.5));

        double Unit() => (_rng.Roll(1000) - 1) / 999.0;
    }
}
