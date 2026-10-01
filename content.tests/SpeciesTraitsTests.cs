using System;
using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Magic;
using Core.Resolution;
using Core.Rules;

namespace Content.Tests
{
    // THE SPECIES' OWN CHOICES (cc_task_e-shop-species-and-ui-notes.md 1.3, SRD 5.2.1 pp.84-86): Skillful, Keen Senses,
    // a lineage's spellcasting ability, Medium or Small; each in the creator's Traits step, on the hero, in a save
    // and on the sheet. And the Wood Elf whole, and Resourceful as the reroll Indomitable already was
    public class SpeciesTraitsTests
    {
        static readonly Library Srd = Library.Srd();

        // as far as the Traits step, the class's own picks made, the traits left to the test
        static Creation.Creation Making(string cls, string species, string lineage = null, int level = 1)
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);

            making.StartAt(level);
            making.Pick(Srd.Class(cls));
            making.Pick(Srd.Kind(species));
            if (lineage != null) making.PickLineage(Srd.Kind(lineage));
            making.Pick(Srd.Background("soldier"));

            return making;
        }

        static Hero Finish(Creation.Creation making)
        {
            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList()) making.Train(skill);
            foreach (Skill skill in making.Skills.Take(making.ExpertisePicksLeft).ToList()) making.Master(skill);
            foreach (Spell spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);
            while (making.ImprovementPicksLeft > 0 && making.Improve(making.SuggestedImprovement())) { }
            making.Call("Tess");

            Assert.True(making.Ready, string.Join("; ", making.Problems));
            return making.Finish();
        }

        static Hero RoundTrip(Hero hero)
        {
            var save = new SaveGame { Campaign = "ash_yard", Hero = HeroSaves.Capture(hero) };
            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(save));
            Hero back = HeroSaves.Restore(read.Value.Hero, Srd, out IReadOnlyList<ContentProblem> problems);

            Assert.Empty(problems);
            return back;
        }

        [Fact]
        public void AHumanPicksAnySkillAndTheCreatorWaitsForIt()
        {
            Creation.Creation making = Making("fighter", "human");

            Assert.Equal(Step.Traits, making.Next);
            Assert.Equal(1, making.TraitSkillPicks);

            // any skill but the soldier's two (Athletics, Intimidation) and Versatile's Perception
            Assert.Equal(18 - 2 - 1, making.TraitSkillChoices.Count());
            Assert.DoesNotContain(Skill.Perception, making.TraitSkillChoices);
            Assert.False(making.PickTraitSkill(Skill.Athletics));
            Assert.True(making.PickTraitSkill(Skill.Arcana));
            Assert.NotEqual(Step.Traits, making.Next);

            // the class's own list can't take it again
            Assert.DoesNotContain(Skill.Arcana, making.SkillChoices);

            Hero hero = Finish(making);

            Assert.Equal(Training.Proficient, hero.Actor.TrainingIn(Skill.Arcana));
            Assert.Equal(Training.Proficient, RoundTrip(hero).Actor.TrainingIn(Skill.Arcana));
        }

        [Fact]
        public void AnElfsKeenSensesIsOneOfThree()
        {
            Creation.Creation making = Making("fighter", "elf", "elf_wood");

            Assert.Equal(new[] { Skill.Insight, Skill.Perception, Skill.Survival }, making.TraitSkillChoices);

            making.PickTraitSkill(Skill.Survival);
            making.PickSpellAbility(Ability.Wisdom);

            Hero hero = Finish(making);

            Assert.Equal(Training.Proficient, hero.Actor.TrainingIn(Skill.Survival));
            Assert.Equal(Training.Untrained, hero.Actor.TrainingIn(Skill.Perception));
        }

        // "Intelligence, Wisdom, or Charisma is your spellcasting ability for the spells you cast with this trait"
        [Fact]
        public void ALineagesSpellsAreCastWithTheAbilityChosenWhateverTheClassCastsWith()
        {
            Creation.Creation making = Making("mage", "elf", "elf_high", level: 3);

            making.PickTraitSkill(Skill.Perception);
            Assert.Equal(Step.Traits, making.Next);
            Assert.Equal(new[] { Ability.Intelligence, Ability.Wisdom, Ability.Charisma }, making.SpellAbilityChoices);
            Assert.True(making.PickSpellAbility(Ability.Charisma));

            Hero hero = Finish(making);

            Assert.Equal(Ability.Intelligence, hero.Caster.Ability);
            Assert.Equal(Ability.Charisma, hero.Caster.AbilityFor(Srd.Spells.Find("detect_magic")));
            Assert.Equal(Ability.Intelligence, hero.Caster.AbilityFor(Srd.Spells.Find("magic_missile")));

            Hero back = RoundTrip(hero);
            Assert.Equal(Ability.Charisma, back.SpellAbility);
            Assert.Equal(Ability.Charisma, back.Caster.AbilityFor(Srd.Spells.Find("detect_magic")));

            Assert.Equal(Ability.Charisma.NameKey(), SheetView.Of(back).SpeciesSpellAbilityKey);
        }

        [Fact]
        public void AFighterTieflingCastsItsLegacyWithTheAbilityChosen()
        {
            Creation.Creation making = Making("fighter", "tiefling");

            Assert.True(making.PickSpellAbility(Ability.Wisdom));
            Assert.False(making.PickSpellAbility(Ability.Strength));

            Hero hero = Finish(making);

            Assert.Equal(Ability.Wisdom, hero.Caster.Ability);
        }

        [Fact]
        public void AHumanOrATieflingMayBeSmallAndTheRestAreNotAsked()
        {
            Creation.Creation human = Making("fighter", "human");

            Assert.True(human.ChoosesSize);
            Assert.Equal(Size.Medium, human.Size);
            Assert.True(human.PickSize(Size.Small));
            Assert.False(human.PickSize(Size.Large));
            human.PickTraitSkill(Skill.Arcana);

            Hero hero = Finish(human);

            Assert.Equal(Size.Small, hero.Actor.Size);
            Assert.Equal(Size.Small, RoundTrip(hero).Actor.Size);
            Assert.Equal(Size.Small.UiNameKey("size"), SheetView.Of(hero).SizeKey);

            Assert.True(Making("fighter", "tiefling").ChoosesSize);
            Assert.False(Making("fighter", "dwarf").ChoosesSize);
            Assert.Equal(Size.Small, Making("fighter", "halfling").Size);
            Assert.False(Making("fighter", "dwarf").HasTraits);
        }

        // a save from before the choices: the best ability allowed, the species' first size
        [Fact]
        public void AnOldSaveReadsWithTheBestAbilityAndTheSpeciesSize()
        {
            Creation.Creation making = Making("fighter", "tiefling");
            making.SuggestTraits();
            Hero hero = Finish(making);

            SavedHero saved = HeroSaves.Capture(hero);
            saved.SpellAbility = "";
            saved.Size = "";

            Hero back = HeroSaves.Restore(saved, Srd, out _);

            Assert.Null(back.SpellAbility);
            Assert.Equal(Size.Medium, back.Actor.Size);
            Assert.NotNull(back.SpeciesSpellAbility);
        }

        [Fact]
        public void AWoodElfShipsWhole()
        {
            Creation.Creation making = Making("fighter", "elf", "elf_wood", level: 5);
            making.SuggestTraits();
            Hero hero = Finish(making);

            foreach (string spell in new[] { "druidcraft", "longstrider", "pass_without_trace" })
                Assert.True(hero.Caster.Knows(spell), spell);

            Assert.True(hero.Caster.HasFree("longstrider"));
            Assert.Equal(Solo.Usable, Srd.Spells.Find("longstrider").Solo);

            // Longstrider: ten feet more
            int before = hero.Actor.Moves;
            Casting cast = new Incantation(new StandardResolver(new ScriptedRng(10)))
                .Cast(hero.Caster, Srd.Spells.Find("longstrider"), Aim.At(hero.Actor));

            Assert.True(cast.Cast, cast.Refusal);
            Assert.Equal(before + 10, hero.Actor.Moves);
        }

        // Heroic Inspiration, spent as Indomitable is: one failed save a long rest is rolled again
        [Fact]
        public void AHumansResourcefulRerollsTheFirstFailedSaveAfterARest()
        {
            Creation.Creation making = Making("mage", "human");
            making.SuggestTraits();
            Hero hero = Finish(making);

            Assert.Equal(new[] { 0 }, hero.Actor.SaveRerolls);

            Attempt save = Checks.Save(new StandardResolver(new ScriptedRng(1, 20)), hero.Actor, Ability.Wisdom, 15);

            Assert.True(save.Succeeded);
            Assert.Empty(hero.Actor.SaveRerolls);

            hero.LongRest();
            Assert.Single(hero.Actor.SaveRerolls);
        }
    }
}
