using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Inventory;
using Content.Items;
using Content.Schema;
using Content.Spells;
using Core.Characters;
using Core.Dice;
using Core.Magic;

namespace Content.Tests
{
    // cc_task_dedupe-effects.md, Phase 3: one BoonSpec, read by one reader, for spells, stances and
    // items - and the old words refused by name
    public class BoonVocabularyTests
    {
        static readonly Library Srd = Library.Srd();

        static IReadOnlyList<string> SpellProblems(string json)
        {
            SpellReader.TryRead(json, out _, out IReadOnlyList<string> problems);
            return problems;
        }

        [Fact]
        public void ANegativeDiceRollCanNeitherBeWrittenNorMade()
        {
            // why Reduce's -1d4 is a flag beside the dice (SignedDice), not a negative count
            Assert.False(DiceRoll.TryParse("-1d4", out _, out _));
            Assert.Equal(0, new DiceRoll(-1, Die.D4).Count);
        }

        [Fact]
        public void SignedDiceReadALeadingMinusAsTakingThemOff()
        {
            Assert.True(SignedDice.TryParse("-1d4", out SignedDice less, out _));
            Assert.True(less.Less);
            Assert.Equal(DiceRoll.Parse("1d4"), less.Dice);

            Assert.True(SignedDice.TryParse("1d4", out SignedDice more, out _));
            Assert.False(more.Less);
        }

        [Fact]
        public void EnlargeAddsItsWeaponDiceAndReduceTakesThemOff()
        {
            Spell spell = Srd.Spells.Find("enlarge_reduce");

            SignedDice enlarge = spell.Effects.Single(e => e.Mode == "enlarge").Boon.WeaponDice;
            SignedDice reduce = spell.Effects.Single(e => e.Mode == "reduce").Boon.WeaponDice;

            Assert.False(enlarge.Less);
            Assert.True(reduce.Less);
            Assert.Equal(enlarge.Dice, reduce.Dice);
        }

        [Fact]
        public void AChosenWordIsRefusedForAListOfChoices()
        {
            IReadOnlyList<string> problems = SpellProblems(@"
            {""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""sway"",""dice"":""1d4"",""touches"":""checks"",""skill"":""chosen""}]}]}");

            Assert.Contains(problems, p => p.Contains("skill_choices"));
        }

        [Fact]
        public void ABoonKeyOnSomethingThatIsNotASwayIsRefused()
        {
            IReadOnlyList<string> problems = SpellProblems(@"
            {""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""damage"",""amount"":""1d6"",""damage_type"":""fire"",""leans"":""advantage_against""}]}]}");

            Assert.Contains(problems, p => p.Contains("'leans'") && p.Contains("belongs on a sway"));
        }

        [Fact]
        public void RageIsWrittenInTheSameWordsAsASpellsBoon()
        {
            Feature rage = Srd.Class("barbarian").Features.Single(f => f.Id == "rage");

            Assert.Equal(Leans.AdvantageOnChecks | Leans.AdvantageOnSaves, rage.Boons.Single().Leans);
            Assert.Equal(Ability.Strength, rage.Boons.Single().Ability);
            Assert.Equal(Sways.Damage, rage.Boons.Single().Touches);
            Assert.Equal(3, rage.Boons.Single().Defenses.Count);

            Feature reckless = Srd.Class("barbarian").Features.Single(f => f.Id == "reckless_attack");

            Assert.Equal(Leans.AdvantageOnAttacks | Leans.AdvantageAgainst, reckless.Boons.Single().Leans);
            Assert.Equal(Ability.Strength, reckless.Boons.Single().Ability);
        }

        [Fact]
        public void AnItemBoonKeepsEverythingItSaysWhenItIsWorn()
        {
            // Equipment used to rebuild an item's boon from sixteen of its constructor's arguments
            // and drop the rest: an item with leans or resistances lost them when worn
            const string json = @"{""items"": [
              {""id"": ""charm"", ""kind"": ""trinket"", ""slot"": ""trinket"",
               ""boons"": [{""id"": ""charm"", ""leans"": ""advantage_on_saves"", ""ability"": ""wis"",
                           ""defenses"": {""fire"": ""resistant""}, ""duration"": ""rest""}]}]}";

            Assert.True(ItemReader.TryRead(json, out IReadOnlyList<Item> items, out IReadOnlyList<string> problems),
                        string.Join("\n", problems));

            var actor = new Actor("bearer", 5, new AbilityScores(), Allegiance.Hero);
            new Equipment().Wear(items[0], actor);

            Assert.True(actor.Boons.AdvantageOnSave(Ability.Wisdom));
            Assert.False(actor.Boons.AdvantageOnSave(Ability.Strength));
            Assert.Contains(Defense.Resistant, actor.Boons.DefensesAgainst(DamageType.Fire));
            Assert.Equal(Equipment.Source, actor.Boons.All.Single().Source);
        }
    }
}
