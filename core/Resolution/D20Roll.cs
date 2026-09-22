using System;
using System.Collections.Generic;
using Core.Dice;

namespace Core.Resolution
{
    // one d20 roll, already resolved: which faces came up, which one counts, and what the
    // modifier made of it. everything in the game that rolls against a number goes through here -
    // checks, saves and attacks differ in what they compare it to, not in how they roll.
    public sealed class D20Roll
    {
        D20Roll(IReadOnlyList<int> faces, int natural, int modifier, Advantage advantage)
        {
            Faces = faces;
            Natural = natural;
            Modifier = modifier;
            Advantage = advantage;
        }

        // both dice when there was advantage or disadvantage; the tray shows both and greys one
        public IReadOnlyList<int> Faces { get; }

        // the face that counts - what "a natural 20" means
        public int Natural { get; }

        public int Modifier { get; }

        public Advantage Advantage { get; }

        public int Total => Natural + Modifier;

        public bool IsNaturalTwenty => Natural == 20;

        public bool IsNaturalOne => Natural == 1;

        public bool Beats(int dc) => Total >= dc;

        public static D20Roll Make(IRng rng, int modifier, Advantage advantage = Advantage.Flat)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int count = advantage.Dice();
            var faces = new int[count];

            for (int i = 0; i < count; i++) faces[i] = rng.Roll(20);

            int natural = faces[0];

            for (int i = 1; i < count; i++)
                natural = advantage == Advantage.Advantage
                    ? Math.Max(natural, faces[i])
                    : Math.Min(natural, faces[i]);

            return new D20Roll(faces, natural, modifier, advantage);
        }

        // for tests and for replaying a saved roll; skips the RNG entirely
        public static D20Roll Fixed(int natural, int modifier = 0) =>
            new D20Roll(new[] { natural }, natural, modifier, Advantage.Flat);

        public override string ToString() =>
            $"d20 {string.Join("/", Faces)}" +
            (Advantage == Advantage.Flat ? "" : $" ({Advantage.Id()})") +
            $" -> {Natural}{(Modifier >= 0 ? "+" : "")}{Modifier} = {Total}";
    }
}
