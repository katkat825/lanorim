using System.Collections.Generic;
using System.Linq;
using Content.Encounters;
using Content.Monsters;
using Content.Workshop.Tables;
using Core.Tables;

namespace Content.Tests
{
    // cc_task_n: the budget meter matches FightBudget.Check on a table of cases; "how often" round trips; Roll it rolls
    // what the game would; where-used finds the story's cards, the fights and the tables
    public class WorkshopFightMeterTests
    {
        static readonly Bestiary Monsters = Bestiary.Srd();

        static EncounterDraft Fight(int level, string difficulty, params (string Monster, string Count)[] bands) => new()
        {
            Id = "test_fight",
            Kind = EntryKind.Fight,
            Level = level,
            Difficulty = difficulty,
            Monsters = bands.Select(b => new BandDraft { Monster = b.Monster, Count = b.Count }).ToList(),
        };

        public static IEnumerable<object[]> Cases()
        {
            // level, difficulty, monsters, the colour
            yield return new object[] { 1, "low", new[] { ("giant_rat", "1") }, MeterColour.Green };
            yield return new object[] { 1, "moderate", new[] { ("giant_rat", "2") }, MeterColour.Green };
            yield return new object[] { 1, "low", new[] { ("goblin", "1") }, MeterColour.Green };
            yield return new object[] { 1, "low", new[] { ("goblin", "2") }, MeterColour.Red };
            yield return new object[] { 1, "low", new[] { ("bandit", "1"), ("giant_rat", "1") }, MeterColour.Green };
            yield return new object[] { 1, "low", new[] { ("goblin", "1"), ("giant_rat", "1") }, MeterColour.Amber };
            yield return new object[] { 2, "low", new[] { ("giant_rat", "1d4") }, MeterColour.Red };
            yield return new object[] { 3, "high", new[] { ("goblin_boss", "") }, MeterColour.Green };
            yield return new object[] { 3, "moderate", new[] { ("wolf", "3") }, MeterColour.Green };
            yield return new object[] { 3, "moderate", new[] { ("wolf", "4") }, MeterColour.Red };
            yield return new object[] { 5, "low", new[] { ("ogre", "1"), ("wolf", "1") }, MeterColour.Green };
            yield return new object[] { 5, "low", new[] { ("ogre", "1"), ("wolf", "2") }, MeterColour.Amber };
            yield return new object[] { 5, "low", new[] { ("ogre", "2") }, MeterColour.Red };
            yield return new object[] { 11, "high", new[] { ("wolf", "6") }, MeterColour.Red };
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheMeterIsGreenExactlyWhenTheCampaignCheckIsQuiet(int level, string difficulty, (string, string)[] bands, MeterColour colour)
        {
            EncounterDraft fight = Fight(level, difficulty, bands);
            FightMeter meter = FightMeter.Of(fight, Monsters.Find);

            List<Schema.ContentProblem> check = FightBudget.Check(FightMeter.Entry(fight), Monsters.Find, "", fight.Id).ToList();

            Assert.Equal(colour, meter.Colour);
            Assert.Equal(check.Count == 0, meter.Colour == MeterColour.Green);
            Assert.Equal(check.Count(c => c.What.Contains("XP")), meter.OverBudget ? 1 : 0);
            Assert.Equal(check.Count(c => c.What.Contains("enemies")), meter.OverCap ? 1 : 0);
            Assert.Equal(check.Count, meter.Warnings.Count);
        }

        [Fact]
        public void TheSentenceSaysWhichWayItIsOff()
        {
            Assert.Equal(TableWords.MeterRightKey, FightMeter.Of(Fight(1, "low", ("giant_rat", "1")), Monsters.Find).Sentence.Key);
            Assert.Equal(TableWords.MeterCapKey, FightMeter.Of(Fight(2, "low", ("giant_rat", "3")), Monsters.Find).Sentence.Key);
            Assert.Equal(TableWords.MeterOverKey, FightMeter.Of(Fight(1, "low", ("goblin", "1"), ("giant_rat", "1")), Monsters.Find).Sentence.Key);
            Assert.Equal(TableWords.MeterFarOverKey, FightMeter.Of(Fight(5, "low", ("ogre", "2")), Monsters.Find).Sentence.Key);
        }

        [Theory]
        [InlineData(HowOften.Always)]
        [InlineData(HowOften.Most)]
        [InlineData(HowOften.Sometimes)]
        [InlineData(HowOften.Rarely)]
        public void HowOftenRoundTripsThroughTheTrigger(HowOften how)
        {
            TriggerDraft trigger = Oftenness.Trigger(how, null);

            Assert.Equal(how, Oftenness.Of(trigger));
            Assert.Equal(how == HowOften.Always, trigger == null);
        }

        [Fact]
        public void TheSamplesTriggerIsMostOfTheTimeAndAHandWrittenOneIsOther()
        {
            Assert.Equal(HowOften.Most, Oftenness.Of(new TriggerDraft { Roll = "1d20", AtLeast = 4 }));
            Assert.Equal(HowOften.Other, Oftenness.Of(new TriggerDraft { Roll = "1d20", AtLeast = 15 }));
            Assert.Equal(HowOften.Other, Oftenness.Of(new TriggerDraft { Roll = "2d6", AtLeast = 4 }));

            // Other keeps the author's numbers
            var own = new TriggerDraft { Roll = "2d6", AtLeast = 9 };
            Assert.Same(own, Oftenness.Trigger(HowOften.Other, own));
        }

        [Fact]
        public void RollItThrowsTheTableTwentyTimesAsTheGameWould()
        {
            var table = new EncounterTableDraft
            {
                Id = "road",
                Trigger = Oftenness.Trigger(HowOften.Sometimes, null),
                Entries =
                {
                    new EncounterDraft { Id = "crows", Kind = EntryKind.Line, Weight = 3 },
                    new EncounterDraft { Id = "quiet", Kind = EntryKind.Nothing },
                },
            };

            IReadOnlyList<(string Entry, int Count)> rolled = TableSamples.Encounters(table, 7);

            Assert.Equal(TableSamples.Times, rolled.Sum(r => r.Count));
            Assert.All(rolled, r => Assert.Contains(r.Entry, new[] { "", "crows", "quiet" }));
            Assert.Equal(rolled, TableSamples.Encounters(table, 7));
        }

        [Fact]
        public void RollItOnLootCountsAllThreeCoinsAndItems()
        {
            var chest = new LootTableDraft
            {
                Id = "chest",
                Entries =
                {
                    new LootDraft
                    {
                        Id = "all",
                        Gold = new CoinDraft { Roll = "1" },
                        Silver = new CoinDraft { Roll = "2" },
                        Copper = new CoinDraft { Roll = "1d4", Times = 10 },
                        Items = { new LotDraft { Item = "torch", Count = "2" } },
                    },
                },
            };

            LootSample sample = TableSamples.Loot(chest, new[] { chest }, 3);

            Assert.Equal(TableSamples.Times, sample.Openings.Count);
            Assert.All(sample.Openings, o => Assert.InRange(o.Copper, 100 + 20 + 10, 100 + 20 + 40));
            Assert.Equal(("torch", 40), sample.Items.Single());
        }

        [Fact]
        public void WhereUsedFindsTheStoryTheFightsAndTheTables()
        {
            var tables = new TablesDraft();
            tables.Encounters.Add(new EncounterTableDraft
            {
                Id = "fights",
                Entries = { new EncounterDraft { Id = "rats", Kind = EntryKind.Fight, Loot = "nest" } },
            });
            tables.Loot.Add(new LootTableDraft { Id = "nest" });
            tables.Loot.Add(new LootTableDraft { Id = "big", Entries = { new LootDraft { Id = "more", Kind = LootKind.Table, Table = "nest" } } });

            var story = new Dictionary<string, string>
            {
                ["cellar"] = "<<fight rats>>\n<<loot nest>>",
                ["road"] = "<<loot nest>>",
                ["camp"] = "<<loot nestling>>",
            };

            var used = new WhereUsed(tables, story);

            Assert.Equal(new[] { "cellar" }, used.OfFight("rats").Cards);
            WhereUsed.Use loot = used.OfLoot("nest");
            Assert.Equal(new[] { "cellar", "road" }, loot.Cards);
            Assert.Equal(new[] { "rats" }, loot.Fights);
            Assert.Equal(new[] { "big" }, loot.Tables);
            Assert.False(used.OfLoot("big").Any);
        }
    }
}
