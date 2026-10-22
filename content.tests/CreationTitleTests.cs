using Core.Localization;

namespace Content.Tests
{
    // THE CREATOR'S TITLE SAYS WHO YOU'RE MAKING (cc_task_m2 Part 1): a key with placeholders and the names that fill
    // it, never a glued string, in all four states
    public class CreationTitleTests
    {
        static Creation.Creation Fresh() => CreationTests.Fresh();

        [Fact]
        public void BeforeAnythingItIsANewCharacter()
        {
            (string key, var names) = Fresh().Title;

            Assert.Equal(Creation.Creation.TitleKey, key);
            Assert.Empty(names);
        }

        [Fact]
        public void AClassPickedIsANewFighter()
        {
            Creation.Creation making = Fresh();
            making.Pick(making.Library.Class("fighter"));

            (string key, var names) = making.Title;

            Assert.Equal(Creation.Creation.TitleClassKey, key);
            Assert.Equal(new[] { KeyConventions.ClassName("fighter") }, names);
        }

        [Fact]
        public void ASpeciesTooIsANewElfFighterWithTheSpeciesFirst()
        {
            Creation.Creation making = Fresh();
            making.Pick(making.Library.Class("fighter"));
            making.Pick(making.Library.Kind("elf"));
            making.PickLineage(making.Library.Kind("elf_wood"));

            (string key, var names) = making.Title;

            // the species, not the lineage: "A new Elf Fighter"
            Assert.Equal(Creation.Creation.TitleSpeciesClassKey, key);
            Assert.Equal(new[] { KeyConventions.SpeciesName("elf"), KeyConventions.ClassName("fighter") }, names);
        }

        [Fact]
        public void ASpeciesWithNoClassIsANewElf()
        {
            Creation.Creation making = Fresh();
            making.Pick(making.Library.Kind("fairy"));

            (string key, var names) = making.Title;

            Assert.Equal(Creation.Creation.TitleSpeciesKey, key);
            Assert.Equal(new[] { KeyConventions.SpeciesName("fairy") }, names);
        }

        [Fact]
        public void TheTitlesAreWordsWithPlaceholders()
        {
            string[] rows = System.IO.File.ReadAllLines("game.csv");

            Assert.Contains(Creation.Creation.TitleSpeciesClassKey + ",A new {0} {1}", rows);
            Assert.Contains(Creation.Creation.TitleClassKey + ",A new {0}", rows);
        }
    }
}
