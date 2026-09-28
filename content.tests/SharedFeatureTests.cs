using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;

namespace Content.Tests
{
    // cc_task_godfiles-dupes-efficiency.md #9: a feature more than one class or species has is
    // written once in srd/features/shared.json and named by id and level
    public class SharedFeatureTests
    {
        static readonly Library Srd = Library.Srd();

        [Theory]
        [InlineData("barbarian", "extra_attack", 5)]
        [InlineData("fighter", "extra_attack", 5)]
        [InlineData("paladin", "extra_attack", 5)]
        [InlineData("barbarian", "weapon_mastery", 1)]
        [InlineData("rogue", "weapon_mastery", 1)]
        [InlineData("paladin", "weapon_mastery", 1)]
        public void AClassNamesASharedFeatureAtItsOwnLevel(string cls, string id, int level)
        {
            Feature named = Srd.Class(cls).Features.Single(f => f.Id == id);

            Assert.Equal(level, named.Level);
        }

        [Fact]
        public void ExtraAttackIsTheSameGrantForEveryClassThatHasIt()
        {
            Feature[] all = Srd.Classes.SelectMany(c => c.Features).Where(f => f.Id == "extra_attack").ToArray();

            Assert.Equal(3, all.Length);
            Assert.All(all, f => Assert.Equal(Trait.ActionGrant, f.Trait));
            Assert.All(all, f => Assert.Equal(1, f.Count));
        }

        [Fact]
        public void TheRoguesExpertiseComesTwiceAtOneAndAtSix()
        {
            int[] levels = Srd.Class("rogue").Features.Where(f => f.Id == "expertise").Select(f => f.Level).ToArray();

            Assert.Equal(new[] { 1, 6 }, levels);
            Assert.All(Srd.Class("rogue").Features.Where(f => f.Id == "expertise"),
                       f => Assert.Equal(2, f.Count));
        }

        [Theory]
        [InlineData("dwarf", "darkvision_120")]
        [InlineData("orc", "darkvision_120")]
        [InlineData("dragonborn", "darkvision_60")]
        [InlineData("tiefling", "darkvision_60")]
        public void ASpeciesNamesASharedFeatureToo(string species, string id)
        {
            Assert.Contains(Srd.Kind(species).Features, f => f.Id == id && f.Level == 1);
        }

        [Fact]
        public void EverySharedFeatureIsNamedAndTheFileReadsClean()
        {
            Assert.Empty(SharedFeatures.Srd.Problems);
            Assert.Empty(SharedFeatures.Srd.Unused(Srd.Classes.SelectMany(c => c.Features)
                                                      .Concat(Srd.Species.SelectMany(s => s.Features))));
        }

        [Fact]
        public void AFeatureWithNoTraitThatIsNotSharedIsRefused()
        {
            ClassReader.TryRead(@"{""classes"": [{""id"": ""tester"", ""hit_die"": ""d8"", ""saves"": [""str"", ""con""],
              ""features"": [{""id"": ""second_wind_twice"", ""level"": 2}]}]}", out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.Contains("second_wind_twice") && p.Contains(SharedFeatures.File));
        }

        [Fact]
        public void ANameTakesOnlyTheIdAndTheLevel()
        {
            ClassReader.TryRead(@"{""classes"": [{""id"": ""tester"", ""hit_die"": ""d8"", ""saves"": [""str"", ""con""],
              ""features"": [{""id"": ""extra_attack"", ""level"": 5, ""count"": 2}]}]}", out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.Contains("'count' is not a key here"));
        }

        [Fact]
        public void ASharedFeatureHasNoLevelOfItsOwn()
        {
            SharedFeatures read = SharedFeatures.Read(@"{""features"": [
              {""id"": ""a"", ""trait"": ""narrate"", ""level"": 3}, {""id"": ""b""},
              {""id"": ""c"", ""trait"": ""narrate""}, {""id"": ""c"", ""trait"": ""narrate""}]}");

            Assert.Contains(read.Problems, p => p.Contains("a has a level"));
            Assert.Contains(read.Problems, p => p.Contains("b has no trait"));
            Assert.Contains(read.Problems, p => p.Contains("c is defined twice"));
            Assert.Equal(new[] { "c" }, read.Ids);
        }
    }
}
