using System;
using Content.Companions;
using Core.Dice;
using Xunit;

namespace Content.Tests
{
    // THE COMPANION WALKS NEAR ITS HOME AND NEVER ONTO THE MAP (cc_task_ui-issues-10-01.md 4)
    public class RoamingTests
    {
        // home 0.14 right of the screen's corner at x 0.5; the map's far edge (less its clearance) at z -0.4
        static Roaming Made(int seed = 7) =>
            new Roaming((0.64, -0.5), 0.12, 0.52, -0.4, new SeededRng(seed), every: 9, jitter: 4);

        [Fact]
        public void ItStandsStillAWhileThenSetsOff()
        {
            Roaming roaming = Made();

            Assert.False(roaming.Walking);
            Assert.False(roaming.Tick(6.9));
            Assert.Equal(6.9, roaming.Still, 3);

            bool off = false;
            for (int i = 0; i < 100 && !off; i++) off = roaming.Tick(0.1);

            Assert.True(off);
            Assert.True(roaming.Walking);
            Assert.False(roaming.Tick(100), "it doesn't pick another spot while walking");
        }

        [Fact]
        public void EverySpotIsNearHomeAndOffTheMapAndRightOfTheScreen()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                Roaming roaming = Made(seed);

                for (int walk = 0; walk < 200; walk++)
                {
                    while (!roaming.Tick(1)) { }

                    (double x, double z) = roaming.At;
                    double far = Math.Sqrt((x - roaming.Home.X) * (x - roaming.Home.X) +
                                           (z - roaming.Home.Z) * (z - roaming.Home.Z));

                    Assert.True(far <= roaming.Radius + 1e-9, $"{far} from home");
                    Assert.True(z <= roaming.NearestZ, $"z {z} is over the map");
                    Assert.True(x >= roaming.LeftmostX, $"x {x} is behind the screen");

                    roaming.Arrived(roaming.At);
                    Assert.Equal(0, roaming.Still);
                }
            }
        }

        [Fact]
        public void AHomeOnTheMapIsPulledOffIt()
        {
            var roaming = new Roaming((0.3, 0.2), 0.1, 0.52, -0.4, new SeededRng(1));

            Assert.Equal((0.52, -0.4), roaming.Home);
        }

        [Fact]
        public void StoppedMidWalkItStandsWhereItWas()
        {
            Roaming roaming = Made();
            while (!roaming.Tick(1)) { }

            roaming.Arrived((0.6, -0.45));

            Assert.False(roaming.Walking);
            Assert.Equal((0.6, -0.45), roaming.At);
        }
    }
}
