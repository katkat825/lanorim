using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;

namespace Content.Tests
{
    // the Druid's Wild Shape as form cards: v1_class_roster.md's curated forms, SRD 5.2.1's rules
    public class WildShapeTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Druid(int level = 2)
        {
            var hero = new Hero("Fen", Srd.Class("druid"), Srd.Kind("human"),
                                Srd.Background("hermit"),
                                Creation.Creation.Standard(Srd.Class("druid")), level);

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Wisdom] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Nature, Skill.Survival }, null, Srd.Items,
                       Srd.Spells.For("druid").Where(s => s.Level <= 1));

            return hero;
        }

        static Form Card(string id) => Srd.Forms.Find(id);


        // --- the cards ------------------------------------------------------------------------

        [Fact]
        public void FourCardsShipOneForEachJob()
        {
            Assert.True(Srd.Forms.Sound, string.Join("\n", Srd.Forms.Problems));
            Assert.InRange(Srd.Forms.Count, FormShelf.Fewest, FormShelf.Most);

            foreach (FormRole role in FormRoles.All)
                Assert.Single(Srd.Forms.For(role));

            Assert.Equal(FormRole.Scout, Card("cat").Role);
            Assert.Equal(FormRole.Travel, Card("riding_horse").Role);
            Assert.Equal(FormRole.Combat, Card("black_bear").Role);
            Assert.Equal(FormRole.Utility, Card("spider").Role);
        }

        [Fact]
        public void TheDruidsFeatureCountsTheCardsThatShip() =>
            Assert.Equal(Srd.Forms.Count,
                         Srd.Class("druid").Features.Single(f => f.Trait == Trait.Shape).Count);

        [Fact]
        public void TheCardsCarryTheSrdStatblocks()
        {
            Form bear = Card("black_bear");

            Assert.Equal(11, bear.ArmorClass);
            Assert.Equal(15, bear.Strength);
            Assert.Equal(30, bear.Climb);
            Assert.Equal(new DiceRoll(1, Die.D6), bear.Attacks.Single().Damage);

            Form spider = Card("spider");

            Assert.Equal(DamageType.Poison, spider.RiderFor(spider.Attacks.Single()).Type);
            Assert.Null(bear.RiderFor(bear.Attacks.Single()));
        }


        // --- gating ---------------------------------------------------------------------------

        [Fact]
        public void TheChallengeTableGatesTheCardsByLevel()
        {
            // SRD 5.2.1: CR 1/4 from 2, 1/2 from 4, 1 from 8, and no fly speed before 8
            Assert.Equal(2, Form.LevelFor(0, 0));
            Assert.Equal(2, Form.LevelFor(2, 0));
            Assert.Equal(4, Form.LevelFor(5, 0));
            Assert.Equal(8, Form.LevelFor(10, 0));
            Assert.Equal(8, Form.LevelFor(0, 60));

            Assert.Equal(new[] { "cat", "riding_horse", "spider" },
                         Druid(2).FormsOpen(Srd.Forms).Select(f => f.Id).OrderBy(i => i));

            Assert.DoesNotContain(Druid(3).FormsOpen(Srd.Forms), f => f.Id == "black_bear");
            Assert.Contains(Druid(4).FormsOpen(Srd.Forms), f => f.Id == "black_bear");
        }

        [Fact]
        public void ALevelTwoDruidCannotBeABear()
        {
            Hero hero = Druid(2);

            Assert.Equal("opens at level 4", hero.RefusesShape(Card("black_bear")));
            Assert.False(hero.Shift(Card("black_bear")));
            Assert.False(hero.IsShifted);
        }

        [Fact]
        public void AFirstLevelDruidHasNoWildShapeYet()
        {
            Hero hero = Druid(1);

            Assert.Null(hero.WildShape);
            Assert.Empty(hero.FormsOpen(Srd.Forms));
            Assert.False(hero.Shift(Card("cat")));
        }

        [Fact]
        public void OnlyTheDruidShifts()
        {
            var hero = new Hero("Yrsa", Srd.Class("barbarian"), Srd.Kind("orc"),
                                Srd.Background("guard"),
                                Creation.Creation.Standard(Srd.Class("barbarian")), 8);

            hero.Build(null, new[] { Skill.Athletics, Skill.Survival }, null, Srd.Items);

            Assert.Equal("no wild shape", hero.RefusesShape(Card("cat")));
        }


        // --- the action economy and the uses --------------------------------------------------

        [Fact]
        public void ShiftingCostsTheBonusActionAndLeavesTheActions()
        {
            Hero hero = Druid(4);
            var turn = new Turn(hero.Actor, hero.Budget, 1);

            int actions = turn.Actions;

            Assert.True(hero.Shift(Card("black_bear"), turn));
            Assert.Equal(0, turn.BonusActions);
            Assert.Equal(actions, turn.Actions);

            // no bonus action left, so neither a second shape nor the way back this turn
            Assert.Equal("no bonus action left", hero.RefusesShape(Card("cat"), turn));
            Assert.False(hero.Revert(turn));
            Assert.True(hero.IsShifted);

            // and next turn's bonus action takes it off
            Assert.True(hero.Revert(new Turn(hero.Actor, hero.Budget, 2)));
            Assert.False(hero.IsShifted);
        }

        [Fact]
        public void TwoUsesAShortRestGivesOneBackAndALongRestTheRest()
        {
            Hero hero = Druid(2);
            Feature shape = hero.WildShape;
            var resolver = new StandardResolver(new ScriptedRng(4));

            Assert.Equal(2, hero.UsesLeft(shape));

            Assert.True(hero.Shift(Card("cat")));
            Assert.True(hero.Shift(Card("spider")));
            Assert.Equal(0, hero.UsesLeft(shape));
            Assert.Equal("no uses left", hero.RefusesShape(Card("cat")));

            // SRD 5.2.1: one use back on a short rest, not all of them
            hero.ShortRest(resolver);
            Assert.Equal(1, hero.UsesLeft(shape));

            hero.ShortRest(resolver);
            Assert.Equal(2, hero.UsesLeft(shape));

            Assert.True(hero.Shift(Card("cat")));
            Assert.True(hero.Shift(Card("cat")));

            hero.LongRest();
            Assert.Equal(2, hero.UsesLeft(shape));
        }

        [Fact]
        public void AShortRestStillGivesEveryOtherFeatureBackWhole()
        {
            // Natural Recovery is a Circle of the Land feature at 6 in SRD 5.2.1
            Hero hero = Druid(6);
            Feature recovery = hero.Activatable.First(f => f.Id == "natural_recovery");
            var resolver = new StandardResolver(new ScriptedRng(4));

            hero.Actor.Suffer(5, DamageType.Slashing);

            Assert.True(hero.Invoke(recovery, resolver));
            Assert.True(hero.Shift(Card("cat")));

            hero.ShortRest(resolver);

            Assert.Equal(1, hero.UsesLeft(recovery));
            Assert.Equal(2, hero.UsesLeft(hero.WildShape));
        }


        // --- the body swap --------------------------------------------------------------------

        [Fact]
        public void TheShapeTakesTheBodyAndLeavesTheMind()
        {
            Hero hero = Druid(4);
            Actor actor = hero.Actor;

            int wisdom = actor.Scores[Ability.Wisdom];
            int proficiency = actor.ProficiencyBonus;

            Assert.True(hero.Shift(Card("black_bear")));

            Assert.Equal(15, actor.Scores[Ability.Strength]);
            Assert.Equal(12, actor.Scores[Ability.Dexterity]);
            Assert.Equal(14, actor.Scores[Ability.Constitution]);
            Assert.Equal(wisdom, actor.Scores[Ability.Wisdom]);
            Assert.Equal(proficiency, actor.ProficiencyBonus);

            Assert.Equal(11, actor.ArmorClass);
            Assert.Equal(30, actor.Speed);
            Assert.Equal(new[] { "bear_rend" }, hero.Attacks.Select(a => a.Id));

            // Rend: +4 and 1d6+2 at level 4's +2 proficiency - the statblock's own numbers
            Attack rend = hero.Attacks.Single();
            Assert.Equal(4, rend.Modifier(actor));
            Assert.Equal(new DiceRoll(1, Die.D6, 2), rend.DamageFor(actor));
        }

        [Fact]
        public void RevertingPutsBackExactlyWhatWasThere()
        {
            Hero hero = Druid(4);
            Actor actor = hero.Actor;

            // the ceiling on a boon lets a check reach past the base; the snapshot has to see it
            int[] scores = Abilities.All.Select(a => actor.Scores[a]).ToArray();
            int[] bases = Abilities.All.Select(a => actor.Scores.Base(a)).ToArray();
            int[] checks = Skills.All.Select(s => actor.CheckModifier(s)).ToArray();
            int[] saves = Abilities.All.Select(a => actor.SaveModifier(a)).ToArray();
            int armorClass = actor.ArmorClass;
            int speed = actor.Speed;
            string[] attacks = hero.Attacks.Select(a => a.Id).ToArray();
            int boons = actor.Boons.All.Count;
            int maximum = actor.Health.Maximum;

            foreach (string id in new[] { "black_bear", "cat", "spider", "riding_horse" })
            {
                Assert.True(hero.Shift(Card(id)), id);
                Assert.NotEqual(attacks, hero.Attacks.Select(a => a.Id).ToArray());

                Assert.True(hero.Revert());

                Assert.Equal(scores, Abilities.All.Select(a => actor.Scores[a]).ToArray());
                Assert.Equal(bases, Abilities.All.Select(a => actor.Scores.Base(a)).ToArray());
                Assert.Equal(checks, Skills.All.Select(s => actor.CheckModifier(s)).ToArray());
                Assert.Equal(saves, Abilities.All.Select(a => actor.SaveModifier(a)).ToArray());
                Assert.Equal(armorClass, actor.ArmorClass);
                Assert.Equal(speed, actor.Speed);
                Assert.Equal(attacks, hero.Attacks.Select(a => a.Id).ToArray());
                Assert.Equal(boons, actor.Boons.All.Count);
                Assert.Equal(maximum, actor.Health.Maximum);

                hero.LongRest();
            }
        }

        [Fact]
        public void ShiftingFromOneShapeToAnotherStillRevertsToTheDruid()
        {
            Hero hero = Druid(4);
            int strength = hero.Actor.Scores[Ability.Strength];
            int armorClass = hero.Actor.ArmorClass;

            Assert.True(hero.Shift(Card("black_bear")));
            Assert.True(hero.Shift(Card("cat")));

            Assert.Equal("cat", hero.Form.Id);
            Assert.Equal(3, hero.Actor.Scores[Ability.Strength]);

            Assert.True(hero.Revert());

            Assert.Equal(strength, hero.Actor.Scores[Ability.Strength]);
            Assert.Equal(armorClass, hero.Actor.ArmorClass);
        }

        [Fact]
        public void TheCatLendsItsTrainingForAsLongAsItIsWorn()
        {
            Hero hero = Druid(2);
            Actor actor = hero.Actor;

            Assert.Equal(Training.Untrained, actor.TrainingIn(Skill.Stealth));

            Assert.True(hero.Shift(Card("cat")));

            // Stealth +4, the statblock's number: the cat's Dexterity and the druid's proficiency
            Assert.Equal(4, actor.CheckModifier(Skill.Stealth));

            hero.Revert();

            Assert.Equal(actor.AbilityModifier(Ability.Dexterity),
                         actor.CheckModifier(Skill.Stealth));
        }

        [Fact]
        public void ShiftingGrantsTemporaryHitPointsEqualToTheDruidLevel()
        {
            Hero hero = Druid(4);
            int current = hero.Actor.Health.Current;

            Assert.True(hero.Shift(Card("black_bear")));

            Assert.Equal(4, hero.Actor.Health.Temporary);

            // and the druid's own hit points are untouched: SRD 5.2.1 keeps them
            Assert.Equal(current, hero.Actor.Health.Current);
        }

        [Fact]
        public void DroppingToZeroEndsTheShape()
        {
            Hero hero = Druid(4);
            int strength = hero.Actor.Scores[Ability.Strength];

            Assert.True(hero.Shift(Card("black_bear")));

            hero.Actor.Suffer(999, DamageType.Slashing);

            Assert.True(hero.Actor.IsDown);
            Assert.False(hero.IsShifted);
            Assert.Null(hero.Form);
            Assert.Null(hero.Actor.Shape);
            Assert.Equal(strength, hero.Actor.Scores[Ability.Strength]);
        }

        [Fact]
        public void BeingStunnedEndsTheShapeToo()
        {
            Hero hero = Druid(2);

            Assert.True(hero.Shift(Card("riding_horse")));

            hero.Actor.Apply(Condition.Stunned);

            Assert.False(hero.IsShifted);
            Assert.Equal(30, hero.Actor.Speed);
        }

        [Fact]
        public void ARestOrALevelEndsTheShape()
        {
            Hero hero = Druid(2);
            var resolver = new StandardResolver(new ScriptedRng(4));

            Assert.True(hero.Shift(Card("cat")));
            hero.ShortRest(resolver);
            Assert.False(hero.IsShifted);

            Assert.True(hero.Shift(Card("cat")));
            hero.LevelTo(4);
            Assert.False(hero.IsShifted);

            // the Wisdom the level-up raised is the druid's, and the Strength never saw the cat
            Assert.NotEqual(3, hero.Actor.Scores[Ability.Strength]);
        }


        // --- no spells in fur -----------------------------------------------------------------

        [Fact]
        public void AShiftedDruidCastsNothing()
        {
            Hero hero = Druid(2);
            Spell cantrip = hero.Caster.Cantrips.First();
            Spell leveled = hero.Caster.Leveled.First();
            var cast = new Incantation(new StandardResolver(new ScriptedRng(10)));

            Assert.True(hero.Caster.CanCast(cantrip, 0));

            Assert.True(hero.Shift(Card("cat")));

            Assert.False(hero.Caster.CanCast(cantrip, 0));
            Assert.False(hero.Caster.CanCast(leveled, leveled.Level));

            Casting refused = cast.Cast(hero.Caster, leveled, Aim.At(hero.Actor));

            Assert.False(refused.Cast);
            Assert.Equal("in a borrowed shape", refused.Refusal);

            // and nothing was paid for the refusal
            int highest = hero.Caster.HighestAffordable;

            hero.Revert();

            Assert.Equal(highest, hero.Caster.HighestAffordable);
            Assert.True(hero.Caster.CanCast(cantrip, 0));
        }


        // --- the reader -----------------------------------------------------------------------

        const string Good = @"{ ""forms"": [ {
            ""id"": ""wolf"", ""role"": ""combat"", ""challenge_times_ten"": 2,
            ""armor_class"": 12, ""speed"": 40,
            ""scores"": { ""str"": 14, ""dex"": 15, ""con"": 12 },
            ""attacks"": [ { ""id"": ""wolf_bite"", ""damage"": ""1d6"",
                              ""damage_type"": ""piercing"",
                              ""rider"": { ""condition"": ""prone"" } } ] } ] }";

        [Fact]
        public void AGoodCardReads()
        {
            Assert.True(FormReader.TryRead(Good, out IReadOnlyList<Form> forms,
                                           out IReadOnlyList<string> problems),
                        string.Join("\n", problems));

            Form wolf = forms.Single();

            Assert.Equal(2, wolf.MinimumLevel);
            Assert.Equal(Condition.Prone, wolf.RiderFor(wolf.Attacks.Single()).Condition);
        }

        [Fact]
        public void AFlyingCardWaitsForLevelEight()
        {
            string owl = Good.Replace(@"""speed"": 40", @"""speed"": 5, ""fly"": 60");

            Assert.True(FormReader.TryRead(owl, out IReadOnlyList<Form> forms, out _));
            Assert.Equal(8, forms.Single().MinimumLevel);
            Assert.False(forms.Single().OpenAt(7));
        }

        [Theory]
        [InlineData(@"""id"": ""wolf""", @"""id"": ""Wolf!""", "not a form id")]
        [InlineData(@"""role"": ""combat""", @"""role"": ""tank""", "not a role")]
        [InlineData(@"""challenge_times_ten"": 2", @"""challenge_times_ten"": 20",
                    "never reaches")]
        [InlineData(@"""challenge_times_ten"": 2,", "", "no challenge_times_ten")]
        [InlineData(@"""con"": 12", @"""con"": 12, ""wis"": 12", "the druid's own")]
        [InlineData(@"""dex"": 15, ", "", "no dex score")]
        [InlineData(@"""damage_type"": ""piercing""", @"""damage_type"": ""sharp""",
                    "not an SRD damage type")]
        [InlineData(@"{ ""condition"": ""prone"" }", "{ }", "neither damage nor a condition")]
        [InlineData(@"""armor_class"": 12,", "", "no armor_class")]
        public void ABadCardIsRefusedByName(string from, string to, string expected)
        {
            string text = Good.Replace(from, to);

            Assert.NotEqual(Good, text);

            Assert.False(FormReader.TryRead(text, out _, out IReadOnlyList<string> problems));
            Assert.Contains(problems, p => p.Contains(expected));
        }

        [Fact]
        public void ACardWithNothingToAttackWithIsRefused()
        {
            string text = Good.Substring(0, Good.IndexOf(@"""attacks""")) + @"""attacks"": [] } ] }";

            Assert.False(FormReader.TryRead(text, out _, out IReadOnlyList<string> problems));
            Assert.Contains(problems, p => p.Contains("nothing to attack with"));
        }

        [Fact]
        public void NotJsonAndNoFormsAreBothRefused()
        {
            Assert.False(FormReader.TryRead("{ nope", out _, out IReadOnlyList<string> bad));
            Assert.Contains(bad, p => p.Contains("not valid json"));

            Assert.False(FormReader.TryRead("{}", out _, out IReadOnlyList<string> empty));
            Assert.Contains(empty, p => p.Contains("no forms in it"));
        }
    }
}
