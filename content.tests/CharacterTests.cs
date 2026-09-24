using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Creation;
using Content.Items;
using Content.Schema;
using Content.Sheet;
using Content.Species;
using Core.Characters;
using Core.Dice;
using Core.Magic;
using Core.Resolution;

namespace Content.Tests
{
    public class LibraryTests
    {
        static readonly Library Srd = Library.Srd();

        [Fact]
        public void EverySrdFileLoadsWithoutAProblem() =>
            Assert.True(Srd.Sound, string.Join("\n", Srd.Problems));

        [Fact]
        public void TheSevenClassesOfTheRosterAreAllThere()
        {
            Assert.Equal(7, Roster.Classes.Count);

            foreach (string id in Roster.Classes) Assert.NotNull(Srd.Class(id));
        }

        [Fact]
        public void TheSevenSpeciesOfTheRosterAreAllThere()
        {
            Assert.Equal(7, Roster.Species.Count);

            foreach (string id in Roster.Species) Assert.NotNull(Srd.Kind(id));

            Assert.Equal(7, Srd.Playable.Count());
        }

        [Fact]
        public void NoDeferredClassSlippedIn()
        {
            foreach (string deferred in new[] { "bard", "ranger", "sorcerer", "monk",
                                                "wizard", "warlock" })
                Assert.Null(Srd.Class(deferred));
        }

        [Fact]
        public void NoDeferredSpeciesSlippedIn()
        {
            foreach (string deferred in new[] { "gnome", "goliath", "half_elf", "half_orc" })
                Assert.Null(Srd.Kind(deferred));
        }

        [Fact]
        public void EveryClassHasOneSubclassAndACompanion()
        {
            foreach (CharacterClass cls in Srd.Classes)
            {
                Assert.False(string.IsNullOrEmpty(cls.Subclass), cls.Id);
                Assert.False(string.IsNullOrEmpty(cls.Companion), cls.Id);
            }
        }

        [Fact]
        public void FiveCompanionVoicesCoverSevenClasses()
        {
            // the trick that makes the sixth and seventh class cheap: Fighter shares Barbarian's
            // and Paladin shares Cleric's (v1_class_roster.md)
            Assert.Equal(5, Srd.Classes.Select(c => c.Companion).Distinct().Count());

            Assert.Equal(Srd.Class("barbarian").Companion, Srd.Class("fighter").Companion);
            Assert.Equal(Srd.Class("cleric").Companion, Srd.Class("paladin").Companion);
        }

        [Fact]
        public void TheFourCastersCastAndTheThreeMartialsDoNot()
        {
            foreach (string id in new[] { "mage", "cleric", "paladin", "druid" })
                Assert.True(Srd.Class(id).Casts, id);

            foreach (string id in new[] { "barbarian", "fighter", "rogue" })
                Assert.False(Srd.Class(id).Casts, id);
        }

        [Fact]
        public void NoClassPortsExtraAttackLiterally()
        {
            // the hero already has two actions (v1_class_roster.md); a feature that granted an
            // extra action every round would be Extra Attack by another name
            foreach (CharacterClass cls in Srd.Classes)
                foreach (Feature feature in cls.Features.Where(f => f.Trait == Trait.ActionGrant))
                    Assert.True(feature.Uses > 0 || feature.Grants != Grants.Action,
                                $"{cls.Id}/{feature.Id} grants an action every round");
        }

        [Fact]
        public void ElfIsTheOnlySpeciesWithLineagesAndAllThreeShipped()
        {
            Assert.Equal(3, Srd.LineagesOf("elf").Count());

            foreach (Kind kind in Srd.Playable.Where(k => k.Id != "elf"))
                Assert.Empty(kind.Lineages);
        }

        [Fact]
        public void TheWoodElfIsFaster() =>
            Assert.Equal(35, Srd.Kind("elf_wood").Speed);

        [Fact]
        public void EveryClassesStartingGearExists()
        {
            foreach (CharacterClass cls in Srd.Classes)
                foreach (string gear in cls.StartingGear)
                    Assert.True(Srd.Items.Has(gear), $"{cls.Id}: {gear}");
        }

        [Fact]
        public void EverySpellOnAClassListNamesAClassThatExists()
        {
            foreach (Spell spell in Srd.Spells.All)
                foreach (string id in spell.Classes)
                    Assert.True(Roster.Classes.Contains(id), $"{spell.Id}: {id}");
        }

        [Fact]
        public void EveryCasterHasSomethingToCast()
        {
            foreach (CharacterClass cls in Srd.Classes.Where(c => c.Casts))
                Assert.True(Srd.Spells.For(cls.Id).Any(s => s.Level == 1), $"{cls.Id}: no level 1");

            // SRD gives the Paladin no cantrips, and the creator must not ask for two
            foreach (string id in new[] { "mage", "cleric", "druid" })
                Assert.True(Srd.Spells.For(id).Any(s => s.IsCantrip), $"{id}: no cantrips");
        }

        [Fact]
        public void EveryItemAClassIsLockedToIsUsableByThatClass()
        {
            foreach (Item item in Srd.Items.All.Where(i => i.Classes.Count > 0))
                foreach (string id in item.Classes)
                    Assert.True(Roster.Classes.Contains(id), $"{item.Id}: {id}");
        }
    }

    public class ClassProgressionTests
    {
        static readonly Library Srd = Library.Srd();

        [Fact]
        public void HitPointsFollowTheSrdFormula()
        {
            CharacterClass fighter = Srd.Class("fighter");

            // d10, +3 Constitution: 13 at level 1, then 6+3 a level after
            Assert.Equal(13, fighter.HitPointsAt(1, 3));
            Assert.Equal(22, fighter.HitPointsAt(2, 3));
            Assert.Equal(58, fighter.HitPointsAt(6, 3));
        }

        [Fact]
        public void SneakAttackClimbsADieEveryTwoLevels()
        {
            Feature sneak = Srd.Class("rogue").Features.First(f => f.Id == "sneak_attack");

            Assert.Equal(new DiceRoll(1, Die.D6), sneak.AmountAt(1));
            Assert.Equal(new DiceRoll(1, Die.D6), sneak.AmountAt(2));
            Assert.Equal(new DiceRoll(2, Die.D6), sneak.AmountAt(3));
            Assert.Equal(new DiceRoll(10, Die.D6), sneak.AmountAt(19));
        }

        [Fact]
        public void AFeatureIsNotOwnedBeforeItsLevel()
        {
            CharacterClass paladin = Srd.Class("paladin");

            Assert.DoesNotContain(paladin.By(1), f => f.Id == "divine_smite");
            Assert.Contains(paladin.By(2), f => f.Id == "divine_smite");
        }

        // the resource grows with the level, whichever mode the character chose
        [Fact]
        public void ACastersResourceGrowsWithTheLevel()
        {
            CharacterClass mage = Srd.Class("mage");

            var first = (SpellSlots)mage.ResourceAt(1, SpellResourceMode.Slots);
            var fifth = (SpellSlots)mage.ResourceAt(5, SpellResourceMode.Slots);

            Assert.Equal(1, first.Highest);
            Assert.Equal(3, fifth.Highest);

            Assert.True(SpellPoints.PoolFor(CasterProgression.Full, 5) >
                        SpellPoints.PoolFor(CasterProgression.Full, 1));
        }

        [Fact]
        public void APaladinIsAHalfCaster()
        {
            Assert.Equal(CasterProgression.Half, Srd.Class("paladin").Progression);
            Assert.Equal(CasterProgression.Full, Srd.Class("cleric").Progression);

            var paladin = (SpellSlots)Srd.Class("paladin").ResourceAt(10, SpellResourceMode.Slots);
            var cleric = (SpellSlots)Srd.Class("cleric").ResourceAt(10, SpellResourceMode.Slots);

            Assert.True(paladin.Highest < cleric.Highest);
        }
    }

    public class HeroTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Fighter(int level = 1)
        {
            var hero = new Hero("Brenna", Srd.Class("fighter"), Srd.Kind("human"),
                                Srd.Background("soldier"),
                                Creation.Creation.Standard(Srd.Class("fighter")), level);

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Strength] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Athletics, Skill.Perception },
                       null, Srd.Items);

            return hero;
        }

        [Fact]
        public void AFreshFighterIsDressedAndArmed()
        {
            Hero hero = Fighter();

            Assert.NotNull(hero.Equipment.In(Items.Slot.Body));
            Assert.NotNull(hero.Equipment.In(Items.Slot.MainHand));
            Assert.NotNull(hero.Equipment.In(Items.Slot.Shield));

            // chain mail 16, no Dexterity, plus the shield
            Assert.Equal(18, hero.Actor.ArmorClass);
        }

        [Fact]
        public void TheBackgroundsPointsLandOnTheAbilityScores()
        {
            Hero hero = Fighter();

            // the standard array puts 15 in Strength for a fighter, then soldier's +2
            Assert.Equal(17, hero.Actor.Scores.Base(Ability.Strength));
        }

        [Fact]
        public void SkillsComeFromTheClassAndTheBackground()
        {
            Hero hero = Fighter();

            Assert.Equal(Training.Proficient, hero.Actor.TrainingIn(Skill.Athletics));
            Assert.Equal(Training.Proficient, hero.Actor.TrainingIn(Skill.Perception));

            // soldier trains athletics and intimidation
            Assert.Equal(Training.Proficient, hero.Actor.TrainingIn(Skill.Intimidation));
        }

        [Fact]
        public void ABarbarianReadsItsArmorClassOffConstitution()
        {
            var hero = new Hero("Yrsa", Srd.Class("barbarian"), Srd.Kind("orc"),
                                Srd.Background("guard"),
                                new AbilityScores(15, 14, 15, 8, 12, 10));

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Strength] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Athletics, Skill.Survival },
                       null, Srd.Items);

            hero.TakeOff(Items.Slot.Body);

            // 10 + dex 2 + con 3, and the hide armor is off
            Assert.Equal(Ability.Constitution, hero.Actor.UnarmoredDefense);
            Assert.Equal(15, hero.Actor.ArmorClass);
        }

        [Fact]
        public void LevellingRaisesHitPointsAndKeepsTheDamageYouTook()
        {
            Hero hero = Fighter(4);

            hero.Actor.Suffer(10, DamageType.Slashing);

            int was = hero.Actor.Health.Current;
            int wasMax = hero.Actor.Health.Maximum;

            hero.LevelTo(5);

            Assert.True(hero.Actor.Health.Maximum > wasMax);
            Assert.Equal(hero.Actor.Health.Maximum - (wasMax - was), hero.Actor.Health.Current);
        }

        [Fact]
        public void AnAbilityScoreImprovementIsAStraightTwoToSpend()
        {
            // feats are deferred, so each ASI level is +2 (decisions_checklist.md section 1)
            Assert.Equal(0, Hero.AbilityScoreImprovements(3));
            Assert.Equal(1, Hero.AbilityScoreImprovements(4));
            Assert.Equal(5, Hero.AbilityScoreImprovements(20));

            Hero low = Fighter(3);
            Hero high = Fighter(4);

            Assert.Equal(low.Actor.Scores.Base(Ability.Strength) + 2,
                         high.Actor.Scores.Base(Ability.Strength));
        }

        [Fact]
        public void AnImprovementNeverPushesAScorePastTwenty()
        {
            Hero hero = Fighter(20);

            foreach (Ability ability in Abilities.All)
                Assert.InRange(hero.Actor.Scores.Base(ability), 1, Abilities.Ceiling);
        }

        [Fact]
        public void ARogueOnlyGetsItsSneakAttackWhenItHasTheAdvantage()
        {
            var hero = new Hero("Pell", Srd.Class("rogue"), Srd.Kind("halfling"),
                                Srd.Background("criminal"),
                                Creation.Creation.Standard(Srd.Class("rogue")), 5);

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Dexterity] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Stealth, Skill.Acrobatics, Skill.Perception,
                               Skill.Investigation },
                       new[] { Skill.Stealth },
                       Srd.Items);

            Assert.Empty(hero.RidersFor(false));
            Assert.Contains(hero.RidersFor(true), r => r.Id == "sneak_attack");

            // 3d6 at level 5
            Assert.Equal(new DiceRoll(3, Die.D6),
                         hero.RidersFor(true).First(r => r.Id == "sneak_attack").Damage);
        }

        [Fact]
        public void AStanceIsSwitchedOnAndOffAndRunsOut()
        {
            var hero = new Hero("Yrsa", Srd.Class("barbarian"), Srd.Kind("orc"),
                                Srd.Background("guard"),
                                Creation.Creation.Standard(Srd.Class("barbarian")));

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Strength] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Athletics, Skill.Survival }, null, Srd.Items);

            Feature rage = hero.Activatable.First(f => f.Id == "rage");

            Assert.True(hero.Invoke(rage));
            Assert.Equal(2, hero.Actor.Boons.FlatOnDamage);

            Assert.True(hero.EndStance(rage));
            Assert.Equal(0, hero.Actor.Boons.FlatOnDamage);

            // three uses, and the fourth is refused
            Assert.True(hero.Invoke(rage));
            Assert.True(hero.Invoke(rage));
            Assert.False(hero.Invoke(rage));

            hero.LongRest();

            Assert.True(hero.Invoke(rage));
        }

        [Fact]
        public void AnOrcStaysUpOnceBetweenRests()
        {
            var hero = new Hero("Yrsa", Srd.Class("barbarian"), Srd.Kind("orc"),
                                Srd.Background("guard"),
                                Creation.Creation.Standard(Srd.Class("barbarian")));

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Strength] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Athletics, Skill.Survival }, null, Srd.Items);

            hero.Actor.Suffer(999, DamageType.Slashing);

            Assert.True(hero.Actor.IsDown);
            Assert.True(hero.Intercept());
            Assert.Equal(1, hero.Actor.Health.Current);
            Assert.False(hero.Actor.Has(Condition.Unconscious));

            hero.Actor.Suffer(999, DamageType.Slashing);

            Assert.False(hero.Intercept());
        }

        [Fact]
        public void SecondWindHealsAndRunsOut()
        {
            Hero hero = Fighter(3);

            hero.Actor.Suffer(15, DamageType.Slashing);

            Feature wind = hero.Activatable.First(f => f.Id == "second_wind");
            var resolver = new StandardResolver(new ScriptedRng(6));

            int was = hero.Actor.Health.Current;

            Assert.True(hero.Invoke(wind, resolver));
            Assert.Equal(was + 7, hero.Actor.Health.Current);
        }

        [Fact]
        public void ALongRestFillsHitPointsAndMana()
        {
            var hero = new Hero("Sable", Srd.Class("mage"), Srd.Kind("tiefling"),
                                Srd.Background("sage"),
                                Creation.Creation.Standard(Srd.Class("mage")), 5);

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Intelligence] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Arcana, Skill.History }, null, Srd.Items,
                       Srd.Spells.For("mage").Where(s => s.Level <= 3));

            hero.Actor.Suffer(5, DamageType.Slashing);
            hero.Caster.Resource.Pay(1);

            hero.LongRest();

            Assert.Equal(hero.Actor.Health.Maximum, hero.Actor.Health.Current);

            // and the day's magic is back too, whichever mode this character chose
            Assert.Equal(3, hero.Caster.Resource.Highest);
        }

        [Fact]
        public void ATieflingResistsFire()
        {
            var hero = new Hero("Sable", Srd.Class("mage"), Srd.Kind("tiefling"),
                                Srd.Background("sage"),
                                Creation.Creation.Standard(Srd.Class("mage")));

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Intelligence] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Arcana, Skill.History }, null, Srd.Items);

            Assert.Equal(Defense.Resistant, hero.Actor.DefenseAgainst(DamageType.Fire));
        }

        [Fact]
        public void WearingSomethingElseSwapsItAndTheOldOneGoesBackInThePack()
        {
            Hero hero = Fighter();

            Item plate = Srd.Items.Find("plate_armor");
            hero.Pack.Take(plate);

            Item was = hero.Equipment.In(Items.Slot.Body);

            Assert.True(hero.Wear(plate));
            Assert.Equal("plate_armor", hero.Equipment.In(Items.Slot.Body).Id);
            Assert.True(hero.Pack.Has(was.Id));
        }

        [Fact]
        public void ATwoHandedWeaponTakesTheShieldOff()
        {
            Hero hero = Fighter();

            Item greatsword = Srd.Items.Find("greatsword");
            hero.Pack.Take(greatsword);

            Assert.True(hero.Wear(greatsword));
            Assert.Null(hero.Equipment.In(Items.Slot.Shield));
            Assert.Null(hero.Equipment.In(Items.Slot.MainHand));
            Assert.True(hero.Pack.Has("shield"));
        }

        [Fact]
        public void AnItemsBoonGoesOnWithItAndComesOffWithIt()
        {
            Hero hero = Fighter(5);

            Item ring = Srd.Items.Find("ring_of_protection");
            hero.Pack.Take(ring);

            int was = hero.Actor.ArmorClass;

            Assert.True(hero.Wear(ring));
            Assert.Equal(was + 1, hero.Actor.ArmorClass);

            hero.TakeOff(Items.Slot.Trinket);

            Assert.Equal(was, hero.Actor.ArmorClass);
        }

        [Fact]
        public void AnItemAboveYourLevelIsRefused()
        {
            Hero hero = Fighter(1);

            Item ring = Srd.Items.Find("ring_of_protection");
            hero.Pack.Take(ring);

            Assert.False(hero.Wear(ring));
            Assert.Contains("level", hero.Equipment.Refuses(ring, hero.Actor, "fighter"));
        }

        [Fact]
        public void AClassLockedItemIsRefusedToAnybodyElse()
        {
            Hero hero = Fighter();

            Item symbol = Srd.Items.Find("holy_symbol");
            hero.Pack.Take(symbol);

            Assert.False(hero.Wear(symbol));
        }
    }

    public class CreationTests
    {
        static readonly Library Srd = Library.Srd();

        static Creation.Creation Fresh() => new Creation.Creation(Srd, Srd.Backgrounds);

        [Fact]
        public void ItAsksForAClassFirst() => Assert.Equal(Step.Class, Fresh().Next);

        [Fact]
        public void TheStandardArrayIsExactlyTheBudget()
        {
            foreach (CharacterClass cls in Srd.Classes)
            {
                AbilityScores scores = Creation.Creation.Standard(cls);

                Assert.True(scores.IsLegalPointBuy(out string problem), $"{cls.Id}: {problem}");
                Assert.Equal(Abilities.PointBuyBudget, scores.PointBuySpend);
            }
        }

        [Fact]
        public void TheGuidedArrayPutsTheBestScoreWhereTheClassWantsIt()
        {
            AbilityScores rogue = Creation.Creation.Standard(Srd.Class("rogue"));

            Assert.Equal(15, rogue.Base(Ability.Dexterity));

            AbilityScores cleric = Creation.Creation.Standard(Srd.Class("cleric"));

            Assert.Equal(15, cleric.Base(Ability.Wisdom));
        }

        [Fact]
        public void PickingAnElfAsksForALineage()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("rogue"));
            making.Pick(Srd.Kind("elf"));

            Assert.True(making.NeedsLineage);
            Assert.Equal(Step.Lineage, making.Next);

            making.PickLineage(Srd.Kind("elf_wood"));

            Assert.Equal(Step.Background, making.Next);
        }

        [Fact]
        public void PickingAHumanDoesNot()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("fighter"));
            making.Pick(Srd.Kind("human"));

            Assert.False(making.NeedsLineage);
            Assert.Equal(Step.Background, making.Next);
        }

        [Fact]
        public void ALineageOfAnotherSpeciesIsRefused()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("fighter"));
            making.Pick(Srd.Kind("human"));

            Assert.False(making.PickLineage(Srd.Kind("elf_wood")));
        }

        [Fact]
        public void ASkillOffTheClassListIsRefused()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("fighter"));

            Assert.False(making.Train(Skill.Arcana));
            Assert.True(making.Train(Skill.Athletics));
        }

        [Fact]
        public void YouCannotPickMoreSkillsThanTheClassOffers()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("fighter"));

            Assert.True(making.Train(Skill.Athletics));
            Assert.True(making.Train(Skill.Perception));
            Assert.False(making.Train(Skill.Insight));
        }

        [Fact]
        public void ExpertiseOnlyDoublesSomethingYouAreAlreadyTrainedIn()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("rogue"));
            making.Pick(Srd.Kind("human"));
            making.Pick(Srd.Background("criminal"));

            Assert.False(making.Master(Skill.Arcana));

            making.Train(Skill.Stealth);

            Assert.True(making.Master(Skill.Stealth));
        }

        [Fact]
        public void ASpellOffTheClassListIsRefused()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("cleric"));

            Assert.False(making.Learn(Srd.Spells.Find("fireball")));
            Assert.True(making.Learn(Srd.Spells.Find("sacred_flame")));
        }

        [Fact]
        public void ASpellAboveYourLevelIsRefused()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("mage"));

            Assert.Equal(1, making.HighestSpellLevel);
            Assert.False(making.Learn(Srd.Spells.Find("fireball")));
            Assert.True(making.Learn(Srd.Spells.Find("magic_missile")));
        }

        [Fact]
        public void AMartialClassIsNeverAskedAboutSpells()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("barbarian"));

            Assert.Equal(0, making.SpellPicks);
            Assert.Empty(making.SpellChoices);
        }

        [Fact]
        public void TheWholeGuidedRunMakesAPlayableHero()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("cleric"));
            making.Pick(Srd.Kind("dwarf"));
            making.Pick(Srd.Background("acolyte"));

            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft))
                making.Train(skill);

            while (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0)
            {
                Spell next = making.SpellChoices.FirstOrDefault(
                    s => s.IsCantrip ? making.CantripPicksLeft > 0 : making.SpellPicksLeft > 0);

                if (next == null) break;

                making.Learn(next);
            }

            making.Call("Hild");

            Assert.True(making.Ready, string.Join("; ", making.Problems));

            Hero hero = making.Finish();

            Assert.NotNull(hero);
            Assert.True(hero.Actor.Health.Maximum > 0);
            Assert.True(hero.Casts);
            Assert.NotEmpty(hero.Spells);
            Assert.NotEmpty(hero.Attacks);
            Assert.Equal(Defense.Resistant, hero.Actor.DefenseAgainst(DamageType.Poison));
        }

        [Fact]
        public void AnUnfinishedCreationSaysWhatIsMissing()
        {
            Creation.Creation making = Fresh();

            making.Pick(Srd.Class("cleric"));

            Assert.Null(making.Finish());
            Assert.Contains(making.Problems, p => p.Contains("species"));
        }
    }
}
