using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Items;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Xunit;

namespace Content.Tests
{
    // A HERO GOES OUT THROUGH THE WHOLE PIPE AND COMES BACK THE SAME HERO: captured, written as
    // JSON, read, and rebuilt against the library. "The same" is judged by the character sheet,
    // because the sheet is every number the player can see - if it matches, nothing they could
    // notice moved.
    public class HeroSaveTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Made(string cls, string background, Dictionary<Ability, int> spend,
                         Skill[] skills, int level,
                         SpellResourceMode resource = SpellResourceMode.Slots,
                         int highestSpell = 0)
        {
            CharacterClass made = Srd.Class(cls);

            var hero = new Hero("Pell", made, Srd.Kind("human"), Srd.Background(background),
                                Creation.Creation.Standard(made), level, null, resource)
            {
                Alignment = Alignment.ChaoticGood,
            };

            hero.Build(spend, skills, null, Srd.Items,
                       Srd.Spells.For(cls).Where(s => s.Level <= highestSpell).ToList());

            return hero;
        }

        // the whole pipe: nothing may go missing between the live hero and the rebuilt one
        static Hero RoundTrip(Hero hero)
        {
            var save = new SaveGame { Campaign = "ash_yard", Hero = HeroSaves.Capture(hero) };

            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(save));

            Assert.True(read.Ok, string.Join("\n", read.Problems));
            Assert.Empty(read.Problems);

            Hero back = HeroSaves.Restore(read.Value.Hero, Srd,
                                          out IReadOnlyList<ContentProblem> problems);

            Assert.NotNull(back);
            Assert.Empty(problems);

            return back;
        }

        static void SameSheet(Hero was, Hero back)
        {
            SheetView a = SheetView.Of(was);
            SheetView b = SheetView.Of(back);

            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.ClassKey, b.ClassKey);
            Assert.Equal(a.SpeciesKey, b.SpeciesKey);
            Assert.Equal(a.LineageKey, b.LineageKey);
            Assert.Equal(a.BackgroundKey, b.BackgroundKey);
            Assert.Equal(a.AlignmentKey, b.AlignmentKey);
            Assert.Equal(a.Level, b.Level);

            Assert.Equal(a.ArmorClass, b.ArmorClass);
            Assert.Equal(a.Initiative, b.Initiative);
            Assert.Equal(a.Speed, b.Speed);
            Assert.Equal(a.HitPoints, b.HitPoints);
            Assert.Equal(a.MaxHitPoints, b.MaxHitPoints);
            Assert.Equal(a.TemporaryHitPoints, b.TemporaryHitPoints);
            Assert.Equal(a.HitDice, b.HitDice);
            Assert.Equal(a.MaxHitDice, b.MaxHitDice);
            Assert.Equal(a.ProficiencyBonus, b.ProficiencyBonus);
            Assert.Equal(a.ConditionKeys.OrderBy(k => k, StringComparer.Ordinal),
                         b.ConditionKeys.OrderBy(k => k, StringComparer.Ordinal));

            Assert.Equal(a.Abilities.Select(r => (r.Ability, r.Score, r.Modifier, r.Save,
                                                  r.SaveProficient)),
                         b.Abilities.Select(r => (r.Ability, r.Score, r.Modifier, r.Save,
                                                  r.SaveProficient)));

            Assert.Equal(a.Skills.Select(r => (r.Skill, r.Modifier, r.Training, r.Passive)),
                         b.Skills.Select(r => (r.Skill, r.Modifier, r.Training, r.Passive)));

            Assert.Equal(a.Actions, b.Actions);
            Assert.Equal(a.BonusActions, b.BonusActions);
            Assert.Equal(a.Reactions, b.Reactions);
            Assert.Equal(a.ExtraActionsBanked, b.ExtraActionsBanked);

            Assert.Equal(a.Weapons.Select(w => (w.NameKey, w.AttackBonus, w.Damage.ToString())),
                         b.Weapons.Select(w => (w.NameKey, w.AttackBonus, w.Damage.ToString())));

            Assert.Equal(a.Casts, b.Casts);
            Assert.Equal(a.Resource, b.Resource);
            Assert.Equal(a.Slots, b.Slots);
            Assert.Equal(a.PointsLeft, b.PointsLeft);
            Assert.Equal(a.PointsMost, b.PointsMost);
            Assert.Equal(a.Spells.Select(s => (s.NameKey, s.Castable, s.HighestLevel)),
                         b.Spells.Select(s => (s.NameKey, s.Castable, s.HighestLevel)));

            Assert.Equal(a.Special.Select(s => (s.NameKey, s.UsesLeft)),
                         b.Special.Select(s => (s.NameKey, s.UsesLeft)));

            // and what is carried, which the sheet does not show all of
            Assert.Equal(Carried(was), Carried(back));
            Assert.Equal(was.Pack.Gold, back.Pack.Gold);
            Assert.Equal(was.Equipment.Worn.OrderBy(p => p.Key).Select(p => (p.Key, p.Value.Id)),
                         back.Equipment.Worn.OrderBy(p => p.Key).Select(p => (p.Key, p.Value.Id)));
        }

        static IEnumerable<(string, int)> Carried(Hero hero) =>
            hero.Pack.Everything.GroupBy(s => s.Item.Id)
                .Select(g => (g.Key, g.Sum(s => s.Count)))
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .ToList();

        static Feature FeatureOf(Hero hero, string id) => hero.Features.Single(f => f.Id == id);


        // --- the four heroes ----------------------------------------------------------------------

        [Fact]
        public void AFighterWhoSurgedAndCaughtHisBreathComesBackAsHeWas()
        {
            Hero fighter = Made("fighter", "soldier",
                                new Dictionary<Ability, int>
                                {
                                    [Ability.Strength] = 2,
                                    [Ability.Constitution] = 1,
                                },
                                new[] { Skill.Perception, Skill.Survival }, 5);

            Assert.True(fighter.Invoke(FeatureOf(fighter, "second_wind"),
                                       new StandardResolver(new ScriptedRng(4))));
            Assert.True(fighter.Budget.DrawExtraAction());

            fighter.Actor.Suffer(19, DamageType.Slashing);

            // chain mail off, scale mail on - the chain goes back in the pack
            Item scale = Srd.Items.Find("scale_mail");
            fighter.Pack.Take(scale);
            Assert.True(fighter.Wear(scale));
            Assert.True(fighter.Pack.Has("chain_mail"));

            Hero back = RoundTrip(fighter);

            SameSheet(fighter, back);

            // three uses at level 5 (SRD 5.2.1 p.47), one spent
            Assert.Equal(2, back.UsesLeft(FeatureOf(back, "second_wind")));
            Assert.Equal(0, back.Budget.ExtraActionsLeft);
            Assert.Equal("scale_mail", back.Equipment.In(Slot.Body).Id);
        }

        [Fact]
        public void AMageWithSlotsSpentAndPoisonInHimComesBackAsHeWas()
        {
            Hero mage = Made("mage", "sage",
                             new Dictionary<Ability, int>
                             {
                                 [Ability.Intelligence] = 2,
                                 [Ability.Constitution] = 1,
                             },
                             new[] { Skill.Investigation, Skill.Insight }, 5,
                             highestSpell: 3);

            Assert.True(mage.Caster.Resource.Pay(1));
            Assert.True(mage.Caster.Resource.Pay(3));
            Assert.True(mage.Caster.Resource.Pay(3));

            mage.Actor.Suffer(6, DamageType.Poison);
            mage.Actor.Apply(Condition.Poisoned);

            Hero back = RoundTrip(mage);

            SameSheet(mage, back);

            var slots = Assert.IsType<SpellSlots>(back.Caster.Resource);
            Assert.Equal(3, slots.Remaining(1));
            Assert.Equal(0, slots.Remaining(3));
            Assert.True(back.Actor.Has(Condition.Poisoned));
            Assert.Equal(mage.Caster.Known.Select(s => s.Id), back.Caster.Known.Select(s => s.Id));
        }

        [Fact]
        public void APointsCasterRemembersTheSixthLevelSpellWentOffToday()
        {
            Hero mage = Made("mage", "sage",
                             new Dictionary<Ability, int>
                             {
                                 [Ability.Intelligence] = 1,
                                 [Ability.Constitution] = 1,
                                 [Ability.Wisdom] = 1,
                             },
                             new[] { Skill.Arcana, Skill.Investigation }, 11,
                             SpellResourceMode.Points, highestSpell: 6);

            Assert.True(mage.Caster.Resource.Pay(6));
            Assert.True(mage.Caster.Resource.Pay(2));

            Hero back = RoundTrip(mage);

            SameSheet(mage, back);

            var points = Assert.IsType<SpellPoints>(back.Caster.Resource);
            Assert.Equal(SpellPoints.PoolFor(CasterProgression.Full, 11) -
                         SpellPoints.CostOf(6) - SpellPoints.CostOf(2), points.Remaining);
            Assert.True(points.HasSpent(6));
            Assert.False(points.CanPay(6));

            // and the pool is still a pool: only the once-a-day level is shut
            Assert.True(points.CanPay(5));
        }

        [Fact]
        public void ADruidInTheCatComesBackInTheCatWithOneUseLeft()
        {
            Hero druid = Made("druid", "hermit",
                              new Dictionary<Ability, int>
                              {
                                  [Ability.Wisdom] = 2,
                                  [Ability.Constitution] = 1,
                              },
                              new[] { Skill.Nature, Skill.Survival }, 5, highestSpell: 1);

            Assert.True(druid.Shift(Srd.Forms.Find("cat")));
            druid.Actor.Suffer(8, DamageType.Piercing);

            Hero back = RoundTrip(druid);

            SameSheet(druid, back);

            Assert.True(back.IsShifted);
            Assert.Equal("cat", back.Form.Id);
            Assert.Equal(1, back.UsesLeft(back.WildShape));

            // and the druid underneath is the druid: the cat comes off to the same body
            druid.Revert();
            back.Revert();

            SameSheet(druid, back);
        }


        // --- what a save keeps and what it rebuilds ---------------------------------------------

        // the array as picked, and the spend beside it: the finished scores are what the rules
        // made of those, and the rules make them again
        [Fact]
        public void TheScoresSavedAreTheArrayAndTheSpendNotTheFinishedNumbers()
        {
            Hero fighter = Made("fighter", "soldier",
                                new Dictionary<Ability, int>
                                {
                                    [Ability.Strength] = 2,
                                    [Ability.Constitution] = 1,
                                },
                                null, 5);

            SavedHero saved = HeroSaves.Capture(fighter);

            Assert.Equal(15, saved.Scores[Ability.Strength]);
            Assert.Equal(2, saved.BackgroundSpend[Ability.Strength]);
            Assert.NotEqual(saved.Scores[Ability.Strength],
                            fighter.Actor.Scores.Base(Ability.Strength));
        }

        [Fact]
        public void TheSameHeroWritesTheSameBytes()
        {
            Hero mage = Made("mage", "sage", null, null, 5, highestSpell: 2);
            mage.Actor.Apply(Condition.Poisoned);
            mage.Actor.Apply(Condition.Prone);

            string once = SaveWriter.Write(new SaveGame { Hero = HeroSaves.Capture(mage) });
            string twice = SaveWriter.Write(new SaveGame { Hero = HeroSaves.Capture(mage) });

            Assert.Equal(once, twice);
        }


        // --- as far as it can be read ------------------------------------------------------------

        // the one refusal: there is no hero to build without a class
        [Fact]
        public void AClassThisBuildDoesNotHaveIsRefused()
        {
            SavedHero saved = HeroSaves.Capture(Made("fighter", "soldier", null, null, 3));
            saved.Class = "warlock";

            Hero back = HeroSaves.Restore(saved, Srd, out IReadOnlyList<ContentProblem> problems);

            Assert.Null(back);
            Assert.Contains(problems, p => p.IsAFault && p.Where == "hero.class");
        }

        [Fact]
        public void AnUnknownItemOrSpellIsACautionAndTheRestOfTheHeroLoads()
        {
            Hero mage = Made("mage", "sage", null, null, 5, highestSpell: 2);
            mage.Actor.Suffer(5, DamageType.Fire);

            var save = new SaveGame { Hero = HeroSaves.Capture(mage) };
            save.Hero.Known.Add("wish_upon_a_star");
            save.Hero.Pack.Add(new SavedStack("bag_of_endless_socks", 2));
            save.Hero.Worn[Slot.Shield] = "shield_of_nothing";

            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(save));

            Hero back = HeroSaves.Restore(read.Value.Hero, Srd,
                                          out IReadOnlyList<ContentProblem> problems);

            Assert.NotNull(back);
            Assert.Equal(3, problems.Count);
            Assert.All(problems, p => Assert.True(p.IsACaution));
            Assert.Contains(problems, p => p.What.Contains("wish_upon_a_star"));
            Assert.Contains(problems, p => p.What.Contains("bag_of_endless_socks"));
            Assert.Contains(problems, p => p.What.Contains("shield_of_nothing"));

            // and everything this build DID understand is the hero it was
            SameSheet(mage, back);
        }
    }
}
