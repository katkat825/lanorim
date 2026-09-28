using System.Collections.Generic;
using Core.Localization;
using Core.Words;

namespace Core.Resolution
{
    // the SRD 5.2.1 DC ladder. a campaign names a rung or gives a custom number
    // (updated_decisions.md, skill checks).
    [Fallback(Difficulty.Medium)]
    public enum Difficulty
    {
        VeryEasy = 5,
        Easy = 10,
        Medium = 15,
        Hard = 20,
        VeryHard = 25,
        NearlyImpossible = 30,
    }

    public static class Difficulties
    {
        public static readonly IReadOnlyList<Difficulty> Ladder = new[]
        {
            Difficulty.VeryEasy, Difficulty.Easy, Difficulty.Medium,
            Difficulty.Hard, Difficulty.VeryHard, Difficulty.NearlyImpossible,
        };

        public static int Dc(this Difficulty difficulty) => (int)difficulty;

        public static string NameKey(this Difficulty difficulty) =>
            KeyConventions.Key(KeyConventions.DifficultyNs, difficulty.Id(), "name");

        // the rung a raw DC sits on, for showing a custom number as words. rounds down to the rung
        // it has reached, so DC 17 reads as Medium - it is not yet Hard.
        public static Difficulty Nearest(int dc)
        {
            Difficulty reached = Difficulty.VeryEasy;

            foreach (Difficulty d in Ladder)
                if (dc >= d.Dc())
                    reached = d;

            return reached;
        }
    }
}
