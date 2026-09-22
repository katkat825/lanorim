using System.Collections.Generic;
using Core.Dice;
using Core.Statistics;

namespace Core.Tests
{
    public class DieTests
    {
        [Fact]
        public void TheValueIsTheFaceCount()
        {
            Assert.Equal(20, Die.D20.Sides());
            Assert.Equal(100, Die.D100.Sides());
        }

        [Fact]
        public void TheSetIsTheSevenDiceOnTheTable()
        {
            Assert.Equal(7, DieExtensions.All.Count);
            Assert.Contains(Die.D20, DieExtensions.All);
            Assert.DoesNotContain(Die.None, DieExtensions.All);
        }

        [Theory]
        [InlineData("d20", Die.D20)]
        [InlineData("D6", Die.D6)]
        [InlineData(" d100 ", Die.D100)]
        public void ParsesTheLabelItPrints(string text, Die die)
        {
            Assert.True(DieExtensions.TryParse(text, out Die read));
            Assert.Equal(die, read);
        }

        [Theory]
        [InlineData("d7")]
        [InlineData("d")]
        [InlineData("20")]
        [InlineData("")]
        public void RefusesADieTheGameDoesNotOwn(string text) =>
            Assert.False(DieExtensions.TryParse(text, out _));

        [Fact]
        public void AverageRoundsTheHalfFaceDown()
        {
            Assert.Equal(4, Die.D8.Average());   // 4.5
            Assert.Equal(3, Die.D6.Average());   // 3.5
            Assert.Equal(10, Die.D20.Average()); // 10.5
        }
    }

    public class DiceRollTests
    {
        [Theory]
        [InlineData("2d6+3", 2, Die.D6, 3)]
        [InlineData("d8", 1, Die.D8, 0)]
        [InlineData("1d4-1", 1, Die.D4, -1)]
        [InlineData("10d10 + 20", 10, Die.D10, 20)]
        public void ParsesTheNotation(string text, int count, Die die, int modifier)
        {
            Assert.True(DiceRoll.TryParse(text, out DiceRoll dice, out string problem));
            Assert.Null(problem);
            Assert.Equal(new DiceRoll(count, die, modifier), dice);
        }

        [Fact]
        public void ABareNumberIsAFlatAmount()
        {
            Assert.True(DiceRoll.TryParse("4", out DiceRoll dice, out _));
            Assert.Equal(DiceRoll.Flat(4), dice);
            Assert.False(dice.RollsAnything);

            // a signed one too - "+3" in a damage field means 3 damage, and refusing it would be
            // a data error for no gain
            Assert.True(DiceRoll.TryParse("+3", out DiceRoll signed, out _));
            Assert.Equal(DiceRoll.Flat(3), signed);
        }

        [Theory]
        [InlineData("2d7")]
        [InlineData("0d6")]
        [InlineData("two d6")]
        [InlineData("")]
        [InlineData("d6+")]
        public void RefusesNonsenseAndSaysWhy(string text)
        {
            Assert.False(DiceRoll.TryParse(text, out _, out string problem));
            Assert.False(string.IsNullOrWhiteSpace(problem));
        }

        [Theory]
        [InlineData("2d6+3")]
        [InlineData("1d8")]
        [InlineData("3d10-2")]
        [InlineData("0")]
        [InlineData("7")]
        public void RoundTripsThroughItsOwnText(string text)
        {
            DiceRoll dice = DiceRoll.Parse(text);

            Assert.Equal(dice, DiceRoll.Parse(dice.ToString()));
        }

        [Fact]
        public void BoundsAndAverageAreTheSrdOnes()
        {
            DiceRoll dice = DiceRoll.Parse("2d6+3");

            Assert.Equal(5, dice.Minimum);
            Assert.Equal(15, dice.Maximum);
            Assert.Equal(10, dice.Average); // 2 x 3.5 = 7, + 3
        }

        [Fact]
        public void ACriticalDoublesTheDiceAndNotTheModifier()
        {
            DiceRoll dice = DiceRoll.Parse("1d8+4").Doubled();

            Assert.Equal(new DiceRoll(2, Die.D8, 4), dice);
            Assert.Equal(6, dice.Minimum);
            Assert.Equal(20, dice.Maximum);
        }

        [Fact]
        public void RollingReportsEveryFaceItThrew()
        {
            var rng = new ScriptedRng(3, 5);

            int total = DiceRoll.Parse("2d6+1").Roll(rng, out IReadOnlyList<int> faces);

            Assert.Equal(9, total);
            Assert.Equal(new[] { 3, 5 }, faces);
        }

        [Fact]
        public void AFlatAmountRollsNothingAndStillAnswers()
        {
            var rng = new ScriptedRng(6);

            Assert.Equal(4, DiceRoll.Flat(4).Roll(rng, out IReadOnlyList<int> faces));
            Assert.Empty(faces);
        }
    }

    public class RngTests
    {
        [Fact]
        public void ASeedReplaysExactly()
        {
            var a = new SeededRng(12345);
            var b = new SeededRng(12345);

            for (int i = 0; i < 200; i++) Assert.Equal(a.Roll(20), b.Roll(20));
        }

        [Fact]
        public void NearbySeedsDoNotOpenWithTheSameRoll()
        {
            // SplitMix64 scrambles the seed first; without it, seeds 1 and 2 correlate
            var opening = new HashSet<int>();

            for (int seed = 1; seed <= 20; seed++) opening.Add(new SeededRng(seed).Roll(20));

            Assert.True(opening.Count >= 8, $"only {opening.Count} distinct opening rolls in 20 seeds");
        }

        [Fact]
        public void TheD20IsUniform()
        {
            // the same chi-squared test check-fairness runs, kept in dotnet test so a change to
            // the generator cannot pass unnoticed
            var rng = new SeededRng(20260921);
            var tally = new FaceTally(20);

            for (int i = 0; i < 200_000; i++) tally.Add(rng.Roll(20));

            Assert.Equal(Fairness.Uniform, tally.Verdict);
            Assert.Equal(Fairness.Uniform, tally.DriftVerdict);
        }

        [Fact]
        public void EveryDieOnTheTableIsUniform()
        {
            foreach (Die die in new[] { Die.D4, Die.D6, Die.D8, Die.D10, Die.D12, Die.D20 })
            {
                var rng = new SeededRng(die.Sides() * 7919);
                var tally = new FaceTally(die.Sides());

                for (int i = 0; i < 60_000; i++) tally.Add(die.Roll(rng));

                Assert.True(tally.Verdict == Fairness.Uniform,
                            $"{die.Label()} came out {tally.Verdict}: {tally}");
            }
        }

        [Fact]
        public void ARecordingRngKeepsWhatWasThrown()
        {
            var rng = new RecordingRng(new ScriptedRng(4, 9));

            rng.Roll(6);
            rng.Roll(20);

            Assert.Equal(new[] { (6, 4), (20, 9) }, rng.Rolls);
        }

        [Fact]
        public void AScriptedRngClampsToTheDieItIsAskedFor() =>
            Assert.Equal(6, new ScriptedRng(19).Roll(6));
    }
}
