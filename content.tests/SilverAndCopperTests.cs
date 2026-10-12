using System.Collections.Generic;
using Content.Dialogue;
using Content.Inventory;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Tables;

namespace Content.Tests
{
    // SILVER AND COPPER FOR AUTHORS (cc_task_k). the purse has always been copper; now a story can pay and take in
    // any of the three coins, and a <<loot>> says what it found in copper as well as in whole gold
    public class SilverAndCopperTests
    {
        static readonly Library Srd = Library.Srd();

        internal static Hero Fighter()
        {
            var hero = new Hero("Brenna", Srd.Class("fighter"), Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(Srd.Class("fighter")), 3);

            hero.Build(new Dictionary<Ability, int> { [Ability.Strength] = 2, [Ability.Constitution] = 1 },
                       new[] { Skill.Athletics, Skill.Perception }, null, Srd.Items);

            return hero;
        }

        static Referee Referee(Hero hero) =>
            new Referee(hero, new StandardResolver(new ScriptedRng(10)), new GmScreen(new ScriptedRng(1)), Srd);

        static void Settle(Hero hero, string command)
        {
            Request request = Request.Parse(command, out string problem);

            Assert.True(problem == null, problem);

            Referee(hero).Settle(request);
        }

        [Theory]
        [InlineData("gold 3", 300)]
        [InlineData("silver 12", 120)]
        [InlineData("copper 7", 7)]
        public void EachCoinPaysIntoTheCopperPurse(string command, int copper)
        {
            Hero hero = Fighter();
            int had = hero.Pack.Copper;

            Settle(hero, command);

            Assert.Equal(had + copper, hero.Pack.Copper);
        }

        [Theory]
        [InlineData("gold -3", 300)]
        [InlineData("silver -12", 120)]
        [InlineData("copper -7", 7)]
        public void EachCoinTakesFromThePurse(string command, int copper)
        {
            Hero hero = Fighter();
            hero.Pack.Earn(Coins.FromGold(10));
            int had = hero.Pack.Copper;

            Settle(hero, command);

            Assert.Equal(had - copper, hero.Pack.Copper);
        }

        [Theory]
        [InlineData("gold")]
        [InlineData("silver")]
        [InlineData("copper")]
        public void TakingMoreThanThePurseHoldsTakesWhatThereIs(string verb)
        {
            Hero hero = Fighter();
            hero.Pack.Spend(hero.Pack.Copper);
            hero.Pack.Earn(5);

            Settle(hero, $"{verb} -100000");

            Assert.Equal(0, hero.Pack.Copper); // a toll does not go into debt
        }

        [Theory]
        [InlineData("silver x", "<<silver 5>>, or <<silver -5>> to take it")]
        [InlineData("silver", "<<silver 5>>")]
        [InlineData("silver 5 6", "<<silver 5>>")]
        [InlineData("copper lots", "<<copper 5>>, or <<copper -5>> to take it")]
        public void ASmallCoinWrittenWrongIsNamed(string command, string said)
        {
            Assert.Null(Request.Parse(command, out string problem));
            Assert.Contains(said, problem);
            Assert.Contains(command, problem);
        }

        [Fact]
        public void TheVerbsAreTheEnginesOwn()
        {
            Assert.Equal(RequestKind.Silver, Request.Parse("silver 9", out _).Kind);
            Assert.Equal(RequestKind.Copper, Request.Parse("copper -25", out _).Kind);
            Assert.Equal(-25, Request.Parse("copper -25", out _).Amount);
        }

        const string Pockets = @"title: pockets
speaker: dm
---
<<loot goblin_pockets>>
<<if $loot_gold == 2 && $loot_copper == 253>>
    A fat purse. #line:right
<<else>>
    Nothing like it. #line:wrong
<<endif>>
===
";

        [Fact]
        public void LootCopperIsAllOfItAndLootGoldIsTheWholeGold()
        {
            // the book declares $loot_copper, so the campaign does not
            DialogueBook book = DialogueBook.Of("pockets", ("pockets.yarn", Pockets));

            Assert.True(book.Problems.Count == 0, string.Join("; ", book.Problems));

            var talk = new Conversation(book);

            Assert.True(talk.Start("pockets"));

            talk.Advance();

            Assert.True(talk.IsWaiting);
            Assert.Equal(RequestKind.Loot, talk.Pending.Kind);

            talk.Answer(new Answer { Copper = 253 });

            Assert.EndsWith("right", talk.Saying.Key);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(99, 0)]   // "a few coppers" is not a gold piece
        [InlineData(100, 1)]
        [InlineData(253, 2)]
        public void LootGoldRoundsDown(int copper, int gold)
        {
            Assert.Equal(gold, new Answer { Copper = copper }.Gold);
        }
    }
}
