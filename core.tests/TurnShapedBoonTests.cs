using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Rules;

namespace Core.Tests
{
    public class TurnShapedBoonTests
    {
        [Fact]
        public void UntilTheEndOfTheOwnersNextTurnSurvivesTheTurnItWasPutOnIn()
        {
            var caster = Combatants.Hero();
            var target = Combatants.Goblin();

            // put on during the caster's own turn: that turn's end is not the one that counts
            target.Boons.Add(Boon.Of(new BoonSpec { Duration = Duration.NextTurnEnd, Leans = Leans.AdvantageAgainst },
                                     "glimmer", "glimmer", caster));

            target.Boons.TurnEnding(caster, target);
            Assert.True(target.Boons.Has("glimmer"));

            // the target's own turn is not the owner's
            target.Boons.TurnStarting(target, target);
            target.Boons.TurnEnding(target, target);
            Assert.True(target.Boons.Has("glimmer"));

            target.Boons.TurnStarting(caster, target);
            Assert.True(target.Boons.Has("glimmer"));

            target.Boons.TurnEnding(caster, target);
            Assert.False(target.Boons.Has("glimmer"));
        }

        [Fact]
        public void AOnceBoonAgainstIsSpentByTheNextAttackAtItsBearer()
        {
            var attacker = Combatants.Hero();
            var target = Combatants.Goblin();

            target.Boons.Add(Boon.Of(new BoonSpec { Leans = Leans.AdvantageAgainst, Once = true },
                                     "glimmer", "glimmer"));

            Assert.Equal(Advantage.Advantage, target.AdvantageAgainstMe);

            Strike.Roll(new StandardResolver(new ScriptedRng(10, 10)), attacker, target,
                        Combatants.Longsword);

            Assert.Equal(Advantage.Flat, target.AdvantageAgainstMe);
        }

        [Fact]
        public void AMarkPaysOutOnlyOnItsOwnersHits()
        {
            var owner = Combatants.Hero();
            var stranger = Combatants.Hero();
            var target = Combatants.Goblin(hp: 100);
            target.SetHealth(new Health(100));

            target.Boons.Add(Boon.Of(new BoonSpec
            {
                Duration = Duration.Concentration,
                Mark = new Mark(DiceRoll.Parse("1d6"), DamageType.Necrotic),
            }, "hex", "hex", owner));

            // a hit for 5 on the longsword, and 6 on the mark
            Blow marked = Strike.Make(new StandardResolver(new ScriptedRng(19, 5, 6)), owner,
                                      target, Combatants.Longsword);

            Assert.Equal(5 + 4 + 6, marked.Suffered);

            Blow plain = Strike.Make(new StandardResolver(new ScriptedRng(19, 5, 6)), stranger,
                                     target, Combatants.Longsword);

            Assert.Equal(5 + 4, plain.Suffered);
        }
    }
}
