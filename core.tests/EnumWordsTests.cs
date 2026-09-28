using Core.Characters;
using Core.Combat;
using Core.Magic;
using Core.Words;

namespace Core.Tests
{
    // the one word <-> enum helper (cc_task_dedupe-methods.md #1). the words themselves are what the
    // data files already say; these pin the rules that make them
    public class EnumWordsTests
    {
        [Fact]
        public void AWordIsTheNameInSnakeCase()
        {
            Assert.Equal("animal_handling", Skill.AnimalHandling.Id());
            Assert.Equal("caster_next_turn_end", Duration.CasterNextTurnEnd.Id());
            Assert.Equal("fire", DamageType.Fire.Id());
        }

        [Fact]
        public void AWordAttributeIsTheWordInstead()
        {
            Assert.Equal("str", Ability.Strength.Id());
            Assert.Equal("bonus_action", Spend.Bonus.Id());
            Assert.Equal("ends_if_caster_side", OnDamage.EndsIfCastersSide.Id());
            Assert.Equal("won", Outcome.HeroesWon.Id());

            Assert.True(EnumWords.TryParse("cha", out Ability cha));
            Assert.Equal(Ability.Charisma, cha);
            Assert.False(EnumWords.TryParse("charisma", out Ability _));
        }

        [Fact]
        public void ReadingIgnoresCaseAndTheSpaceAround()
        {
            Assert.True(EnumWords.TryParse(" Next_Turn_End ", out Duration lasts));
            Assert.Equal(Duration.NextTurnEnd, lasts);
        }

        [Fact]
        public void AnUnreadValueIsNamedButRefused()
        {
            Assert.Equal("none", Condition.None.Id());
            Assert.False(EnumWords.TryParse("none", out Condition _));

            Assert.Equal("targeted", Trigger.Targeted.Id());
            Assert.False(EnumWords.TryParse("targeted", out Trigger _));
            Assert.DoesNotContain("targeted", EnumWords.Ids<Trigger>());
        }

        [Fact]
        public void AFailedParseLeavesTheFallback()
        {
            Assert.False(EnumWords.TryParse("colossal", out Size size));
            Assert.Equal(Size.Medium, size);

            Assert.False(EnumWords.TryParse("", out Primitive primitive));
            Assert.Equal(Primitive.Narrate, primitive);

            Assert.False(EnumWords.TryParse(null, out Ability ability));
            Assert.Equal(Ability.Strength, ability);
        }

        [Fact]
        public void DigitsAreNeverAWord()
        {
            Assert.False(EnumWords.TryParse("3", out Condition _));
            Assert.False(EnumWords.TryName("3", out Condition _));
        }

        [Fact]
        public void AFlagsSetIsJoinedWithPipesInTheEnumsOrder()
        {
            Assert.Equal("attacks|armor_class", (Sways.ArmorClass | Sways.Attacks).Id());
            Assert.Equal("none", Sways.None.Id());

            Assert.True(EnumWords.TryParse("saves | attacks", out Sways sways));
            Assert.Equal(Sways.Attacks | Sways.Saves, sways);

            Assert.False(EnumWords.TryParse("attacks|luck", out Sways bad));
            Assert.Equal(Sways.None, bad);
        }

        [Fact]
        public void AnEmptySetIsReadOnlyWhereTheDataMayNameIt()
        {
            Assert.True(EnumWords.TryParse("none", out Pulses pulses));
            Assert.Equal(Pulses.None, pulses);
            Assert.True(EnumWords.TryParse("", out Leans _));

            Assert.False(EnumWords.TryParse("none", out Manoeuvre _));
            Assert.False(EnumWords.TryParse("", out Forbid _));
        }

        [Fact]
        public void AFlagsListOffersItsWordsWithoutTheEmptySet()
        {
            Assert.Equal(new[] { "attacks", "saves", "checks", "damage", "armor_class" }, EnumWords.Ids<Sways>());
        }

        [Fact]
        public void TheSaveFilesWordIsTheLowercaseName()
        {
            Assert.Equal("strength", EnumWords.Name(Ability.Strength));
            Assert.Equal("animalhandling", EnumWords.Name(Skill.AnimalHandling));

            Assert.True(EnumWords.TryName("SleightOfHand", out Skill skill));
            Assert.Equal(Skill.SleightOfHand, skill);
            Assert.False(EnumWords.TryName("sleight_of_hand", out Skill _));
        }
    }
}
