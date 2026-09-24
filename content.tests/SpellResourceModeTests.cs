using System.Linq;
using Content.Classes;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Core.Magic;
using Xunit;

namespace Content.Tests
{
    // The resource mode where it meets the rest of the game: a class hands out the right one, a
    // character keeps the one it was created with, and - the claim the whole design rests on - the
    // same spell cast either way does the same thing.
    public class SpellResourceModeTests
    {
        static Library Srd => Library.Srd();

        static Hero Caster(SpellResourceMode mode, int level = 5, string cls = "mage")
        {
            CharacterClass made = Srd.Class(cls);

            var hero = new Hero("Pell", made, Srd.Kind("human"), Srd.Backgrounds.First(),
                                Content.Creation.Creation.Standard(made), level, null, mode);

            hero.Build(null, null, null, Srd.Items,
                       Srd.Spells.All.Where(s => !s.IsCantrip).Take(4).ToList());

            return hero;
        }

        [Fact]
        public void AClassHandsOutWhicheverResourceTheCharacterChose()
        {
            Assert.IsType<SpellSlots>(Caster(SpellResourceMode.Slots).Caster.Resource);
            Assert.IsType<SpellPoints>(Caster(SpellResourceMode.Points).Caster.Resource);
        }

        [Fact]
        public void ANonCasterGetsNoResourceAtAll()
        {
            CharacterClass fighter = Srd.Class("fighter");

            Assert.Equal(CasterProgression.None, fighter.Progression);
            Assert.Null(fighter.ResourceAt(5, SpellResourceMode.Slots));
        }

        [Fact]
        public void TheClassDataSaysWhichProgressionEachCasterIs()
        {
            Assert.Equal(CasterProgression.Full, Srd.Class("mage").Progression);
            Assert.Equal(CasterProgression.Full, Srd.Class("cleric").Progression);
            Assert.Equal(CasterProgression.Full, Srd.Class("druid").Progression);
            Assert.Equal(CasterProgression.Half, Srd.Class("paladin").Progression);
        }

        // SLOTS IS PRE-SELECTED because it is the SRD's own answer, and a player with no opinion
        // should end up holding the faithful one
        [Fact]
        public void CreationOffersSlotsFirst()
        {
            var making = new Content.Creation.Creation(Srd, Srd.Backgrounds);

            Assert.Equal(SpellResourceMode.Slots, making.Resource);
        }

        [Fact]
        public void OnlyACasterIsAskedWhichModeTheyWant()
        {
            var making = new Content.Creation.Creation(Srd, Srd.Backgrounds);

            making.Pick(Srd.Class("fighter"));
            Assert.False(making.ChoosesResource);
            Assert.False(making.Pick(SpellResourceMode.Points));

            var caster = new Content.Creation.Creation(Srd, Srd.Backgrounds);

            caster.Pick(Srd.Class("mage"));
            Assert.True(caster.ChoosesResource);
            Assert.True(caster.Pick(SpellResourceMode.Points));
            Assert.Equal(SpellResourceMode.Points, caster.Resource);
        }

        [Fact]
        public void BothLabelsAreKeysAndBothAreWellFormed()
        {
            Assert.All(Content.Creation.Creation.ResourceKeys(),
                       key => Assert.True(Core.Localization.KeyConventions.IsWellFormed(key), key));
        }

        // THE CLAIM THE WHOLE DESIGN RESTS ON. Both modes ride the same cast-at-level engine, so
        // only the accounting differs - a spell cast at the same level does the same thing either
        // way, and if that ever stops being true the two modes are two games.
        [Fact]
        public void TheSameSpellCastEitherWayDoesTheSameThing()
        {
            Spell spell = Srd.Spells.All.First(s => !s.IsCantrip && s.Level == 1);

            Hero withSlots = Caster(SpellResourceMode.Slots);
            Hero withPoints = Caster(SpellResourceMode.Points);

            withSlots.Caster.Learn(spell);
            withPoints.Caster.Learn(spell);

            // cast at the same level, and the effects are the spell's own - the resource is not
            // consulted by anything downstream of paying for it
            Assert.Equal(withSlots.Caster.Find(spell.Id).Effects.Count,
                         withPoints.Caster.Find(spell.Id).Effects.Count);

            Assert.True(withSlots.Caster.CanCast(spell, 3));
            Assert.True(withPoints.Caster.CanCast(spell, 3));

            Assert.True(withSlots.Caster.Pay(spell, 3));
            Assert.True(withPoints.Caster.Pay(spell, 3));

            // and each paid in its own currency
            Assert.Equal(1, ((SpellSlots)withSlots.Caster.Resource).Remaining(3));
            Assert.Equal(SpellPoints.PoolFor(CasterProgression.Full, 5) - 5,
                         ((SpellPoints)withPoints.Caster.Resource).Remaining);
        }

        // cantrips are at-will in BOTH modes, which means they never reach a resource at all
        [Theory]
        [InlineData(SpellResourceMode.Slots)]
        [InlineData(SpellResourceMode.Points)]
        public void ACantripCostsNothingInEitherMode(SpellResourceMode mode)
        {
            Spell cantrip = Srd.Spells.All.First(s => s.IsCantrip);

            Hero hero = Caster(mode);
            hero.Caster.Learn(cantrip);

            string before = hero.Caster.Resource.Describe();

            Assert.True(hero.Caster.Pay(cantrip, 0));
            Assert.Equal(before, hero.Caster.Resource.Describe());
        }

        [Theory]
        [InlineData(SpellResourceMode.Slots)]
        [InlineData(SpellResourceMode.Points)]
        public void ALongRestGivesTheDaysMagicBackInEitherMode(SpellResourceMode mode)
        {
            Hero hero = Caster(mode);

            hero.Caster.Resource.Pay(1);
            hero.Caster.Resource.Pay(2);

            string spent = hero.Caster.Resource.Describe();

            hero.LongRest();

            Assert.NotEqual(spent, hero.Caster.Resource.Describe());
            Assert.Equal(3, hero.Caster.Resource.Highest);
        }


        // --- the save ----------------------------------------------------------------------------

        [Fact]
        public void ASaveRoundTripsSlotsAndWhatIsLeftOfThem()
        {
            var save = new SaveGame
            {
                Hero = new SavedHero { Name = "Pell", Resource = SpellResourceMode.Slots },
            };

            foreach (int left in new[] { 3, 1, 2 }) save.Hero.Slots.Add(left);

            SavedHero back = SaveReader.Parse(SaveWriter.Write(save)).Value.Hero;

            Assert.Equal(SpellResourceMode.Slots, back.Resource);
            Assert.Equal(new[] { 3, 1, 2 }, back.Slots);
        }

        [Fact]
        public void ASaveRoundTripsPointsAndWhichHighSpellsWentOffToday()
        {
            var save = new SaveGame
            {
                Hero = new SavedHero
                {
                    Name = "Pell", Resource = SpellResourceMode.Points, Points = 41,
                },
            };

            save.Hero.SpentHighLevels.Add(6);
            save.Hero.SpentHighLevels.Add(8);

            SavedHero back = SaveReader.Parse(SaveWriter.Write(save)).Value.Hero;

            Assert.Equal(SpellResourceMode.Points, back.Resource);
            Assert.Equal(41, back.Points);
            Assert.Equal(new[] { 6, 8 }, back.SpentHighLevels);
        }

        // a save written before there was a choice, or by a build that grew a third mode
        [Fact]
        public void ASaveThatSaysNothingIsReadAsSlots()
        {
            Read<SaveGame> read = SaveReader.Parse(
                "{ \"format\": 1, \"hero\": { \"name\": \"Pell\" } }");

            Assert.Equal(SpellResourceMode.Slots, read.Value.Hero.Resource);
        }

        [Fact]
        public void AModeThisBuildDoesNotKnowIsACautionAndTheSaveStillReads()
        {
            Read<SaveGame> read = SaveReader.Parse(
                "{ \"format\": 1, \"hero\": { \"name\": \"Pell\", \"spell_resource\": \"vancian\" } }");

            Assert.True(read.Any);
            Assert.Equal("Pell", read.Value.Hero.Name);
            Assert.Equal(SpellResourceMode.Slots, read.Value.Hero.Resource);
            Assert.Contains(read.Problems, p => p.IsACaution && p.What.Contains("vancian"));
        }
    }
}
