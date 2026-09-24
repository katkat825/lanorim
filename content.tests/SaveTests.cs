using System.Linq;
using Content.Items;
using Content.Saves;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Magic;
using Xunit;

namespace Content.Tests
{
    // A save is the one file in the game written by the game and read by the game, so the test
    // that matters is that it survives the round trip - and that a save this build only half
    // understands still comes back rather than being refused.
    public class SaveTests
    {
        static SaveGame Afternoon()
        {
            var save = new SaveGame
            {
                Campaign = "ash_yard",
                CampaignFormat = ContentFormat.Current,
                Chapter = "the_yard",
                Map = "yard",
                Round = 3,
                Turn = 1,
                ActionsLeft = 2,
                Hero = new SavedHero
                {
                    Name = "Pell",
                    Class = "rogue",
                    Species = "halfling",
                    Background = "criminal",
                    Level = 4,
                    HitPoints = 17,
                    TemporaryHitPoints = 3,
                    HitDice = 2,
                    // a points caster, so the points state is the state this save HAS. A slots
                    // caster writes a slot grid instead and no points at all, which is the whole
                    // point of writing only the active mode
                    Resource = SpellResourceMode.Points,
                    Points = 5,
                    Gold = 16,
                    X = 1,
                    Y = 4,
                },
            };

            save.Hero.Scores[Ability.Dexterity] = 17;
            save.Hero.Scores[Ability.Constitution] = 13;
            save.Hero.Skills.Add(Skill.Stealth);
            save.Hero.Expertise.Add(Skill.Stealth);
            save.Hero.Conditions.Add(Condition.Poisoned);
            save.Hero.Known.Add("magic_missile");
            save.Hero.Worn[Slot.Body] = "leather_armor";
            save.Hero.Pack.Add(new SavedStack("rope", 2));

            save.Foes.Add(new SavedActor
            {
                Id = "goblin", Ordinal = 2, HitPoints = 4, Seat = 1, Initiative = 12, X = 8, Y = 1,
            });

            save.Felt.Add(new SavedDie(Die.D20, 17));

            return save;
        }

        [Fact]
        public void ASaveRoundTripsThroughItsOwnJson()
        {
            SaveGame written = Afternoon();

            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(written));

            Assert.True(read.Any);
            Assert.Empty(read.Problems);

            SaveGame back = read.Value;

            Assert.Equal(written.Campaign, back.Campaign);
            Assert.Equal(written.Chapter, back.Chapter);
            Assert.Equal(written.Map, back.Map);
            Assert.Equal(written.Round, back.Round);
            Assert.Equal(written.ActionsLeft, back.ActionsLeft);

            Assert.Equal("Pell", back.Hero.Name);
            Assert.Equal(4, back.Hero.Level);
            Assert.Equal(17, back.Hero.HitPoints);
            Assert.Equal(3, back.Hero.TemporaryHitPoints);
            Assert.Equal(5, back.Hero.Points);
            Assert.Equal(17, back.Hero.Scores[Ability.Dexterity]);
            Assert.Equal(new[] { Skill.Stealth }, back.Hero.Skills);
            Assert.Equal(new[] { Condition.Poisoned }, back.Hero.Conditions);
            Assert.Equal("leather_armor", back.Hero.Worn[Slot.Body]);
            Assert.Equal(2, back.Hero.Pack.Single().Count);
            Assert.Equal((1, 4), (back.Hero.X, back.Hero.Y));

            SavedActor foe = Assert.Single(back.Foes);
            Assert.Equal("goblin", foe.Id);
            Assert.Equal(2, foe.Ordinal);
            Assert.Equal(1, foe.Seat);

            SavedDie die = Assert.Single(back.Felt);
            Assert.Equal(Die.D20, die.Die);
            Assert.Equal(17, die.Value);
        }

        // THE SAME STATE WRITES THE SAME BYTES. Dictionaries iterate in fill order, so without the
        // ordinal sort two saves of one afternoon would differ and no diff would be readable.
        [Fact]
        public void TheSameStateWritesTheSameBytes()
        {
            Assert.Equal(SaveWriter.Write(Afternoon()), SaveWriter.Write(Afternoon()));
        }

        // A SAVE IS READ AS FAR AS IT CAN BE, which is the opposite of how a campaign is read. A
        // campaign with a fault is refused because the author is there to fix it; a save is
        // somebody's afternoon and there is nobody to fix it.
        [Fact]
        public void AConditionThisBuildDoesNotKnowIsACautionAndTheSaveStillReads()
        {
            string json = SaveWriter.Write(Afternoon())
                                    .Replace("\"poisoned\"", "\"bewildered\"");

            Read<SaveGame> read = SaveReader.Parse(json);

            Assert.True(read.Any);
            Assert.NotEmpty(read.Problems);

            // Ok means NOTHING WAS WRONG, and a caution is a sentence about a save that read - so
            // a save carrying only cautions is still Ok, and Any is what says there is one at all
            Assert.True(read.Ok);
            Assert.All(read.Problems, p => Assert.True(p.IsACaution));
            Assert.Equal("Pell", read.Value.Hero.Name);
            Assert.Empty(read.Value.Hero.Conditions);
        }

        [Fact]
        public void ASaveFromALaterBuildIsReadAsFarAsItGoes()
        {
            string json = SaveWriter.Write(Afternoon())
                                    .Replace($"\"format\": {SaveFormat.Current}", "\"format\": 99");

            Read<SaveGame> read = SaveReader.Parse(json);

            Assert.True(read.Any);
            Assert.Contains(read.Problems, p => p.What.Contains("read as far as it can be"));
            Assert.Equal("ash_yard", read.Value.Campaign);
        }

        // and the one case that IS refused: there is nothing there to read at all
        [Fact]
        public void SomethingThatIsNotJsonIsRefused()
        {
            Read<SaveGame> read = SaveReader.Parse("not a save at all");

            Assert.False(read.Any);
            Assert.Contains(read.Problems, p => p.What.Contains("not JSON"));
        }

        [Fact]
        public void AnEmptySaveIsStillASave()
        {
            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(new SaveGame()));

            Assert.True(read.Any);
            Assert.False(read.Value.MidFight);
            Assert.False(read.Value.HasAHero);
        }
    }
}
