using Core.Characters;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Words;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // the new conditions the faithful spells needed, held to the SRD's core effects
    public class FaithfulConditionsTests : FaithfulSpellFixture
    {
        [Fact]
        public void TheNewConditionsLoadByName()
        {
            foreach (string id in new[] { "blinded", "charmed", "deafened", "incapacitated",
                                          "invisible", "paralyzed", "petrified" })
                Assert.True(EnumWords.TryParse(id, out Condition _), id);
        }

        [Fact]
        public void AHitFromBesideAParalyzedCreatureIsACriticalAndFromAfarItIsNot()
        {
            var resolver = new StandardResolver(new ScriptedRng(15));
            var attacker = new Actor("attacker", 1, new AbilityScores(), Allegiance.Hero);
            Actor target = Goblin();
            target.Apply(Condition.Paralyzed);

            var sword = new Attack("sword", new DiceRoll(1, Die.D8), DamageType.Slashing);

            Assert.True(Strike.Roll(resolver, attacker, target, sword, close: true).IsCritical);
            Assert.False(Strike.Roll(resolver, attacker, target, sword, close: false).IsCritical);
        }

        [Fact]
        public void ParalyzedAndStunnedCreaturesAreAttackedWithAdvantageAndFailStrengthSaves()
        {
            foreach (Condition condition in new[] { Condition.Paralyzed, Condition.Stunned,
                                                    Condition.Petrified, Condition.Unconscious })
            {
                Actor target = Goblin();
                target.Apply(condition);

                Assert.Equal(Advantage.Advantage, target.AdvantageAgainstMe);
                Assert.True(target.IsIncapacitated, condition.Id());

                Attempt save = Checks.Save(new StandardResolver(new ScriptedRng(20)), target,
                                           Ability.Strength, 5);
                Assert.False(save.Succeeded, condition.Id());
            }
        }

        [Fact]
        public void ProneIsAdvantageFromBesideAndDisadvantageFromAfar()
        {
            var attacker = new Actor("attacker", 1, new AbilityScores(), Allegiance.Hero);
            Actor target = Goblin();
            target.Apply(Condition.Prone);

            Assert.Equal(Advantage.Advantage, Strike.Lean(attacker, target, close: true));
            Assert.Equal(Advantage.Disadvantage, Strike.Lean(attacker, target, close: false));
        }

        [Fact]
        public void BlindedAndInvisibleAreTheSightRule()
        {
            var attacker = new Actor("attacker", 1, new AbilityScores(), Allegiance.Hero);
            Actor target = Goblin();

            target.Apply(Condition.Invisible);
            Assert.Equal(Advantage.Disadvantage, Strike.Lean(attacker, target));
            Assert.Equal(Advantage.Advantage, Strike.Lean(target, attacker));

            target.Remove(Condition.Invisible);
            attacker.Apply(Condition.Blinded);
            Assert.Equal(Advantage.Disadvantage, Strike.Lean(attacker, target));
            Assert.Equal(Advantage.Advantage, Strike.Lean(target, attacker));
        }

        [Fact]
        public void AdvantageAndDisadvantageCancelHoweverManySourcesEachSideHas()
        {
            // a hidden, poisoned attacker (advantage, disadvantage) at a restrained target
            // (advantage): SRD says flat, however the sources stack up
            var attacker = new Actor("attacker", 1, new AbilityScores(), Allegiance.Hero);
            attacker.Boons.Add(Boon.Of(new BoonSpec { Leans = Leans.AdvantageOnAttacks }, "hidden"));
            attacker.Apply(Condition.Poisoned);

            Actor target = Goblin();
            target.Apply(Condition.Restrained);

            Assert.True(Strike.Lean(attacker, target).IsFlat());
            Assert.Equal(1, Strike.Lean(attacker, target).Dice());
        }

        [Fact]
        public void PetrifiedResistsEverythingAndCannotBePoisoned()
        {
            Actor statue = Goblin();
            statue.Apply(Condition.Poisoned);
            statue.Apply(Condition.Petrified);

            Assert.False(statue.Has(Condition.Poisoned));
            Assert.False(statue.Apply(Condition.Poisoned));
            Assert.Equal(5, statue.Suffer(10, DamageType.Fire));
        }

        [Fact]
        public void UnconsciousComesWithProneAndProneStaysWhenItEnds()
        {
            Actor sleeper = Goblin();
            sleeper.Apply(Condition.Unconscious);

            Assert.True(sleeper.Has(Condition.Prone));

            sleeper.Remove(Condition.Unconscious);

            Assert.True(sleeper.Has(Condition.Prone));
        }

        [Fact]
        public void AStatblocksConditionImmunityKeepsTheConditionOff()
        {
            Actor skeleton = Goblin("skeleton");
            skeleton.MakeImmune(Condition.Poisoned);

            Assert.False(skeleton.Apply(Condition.Poisoned));
        }

        [Fact]
        public void IncapacitatedBreaksConcentration()
        {
            Caster wizard = Wizard(out Actor me);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            cast.Cast(wizard, Book.Find("bless"), Aim.At(me));
            Assert.True(me.IsConcentrating);

            me.Apply(Condition.Incapacitated);
            cast.Check(new[] { me });

            Assert.False(me.IsConcentrating);
        }
    }
}
