using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Monsters;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Resolution;

namespace Content.Tests
{
    // cc_task_dedupe-leftovers.md: the string advantages as leans, a feature's boons, and the keys
    // that meant one idea in two places, read by their one name
    public class LeftoverKeysTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Made(string cls, int level)
        {
            CharacterClass made = Srd.Class(cls);

            var hero = new Hero("Tess", made, Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(made), level);

            hero.Build(new Dictionary<Ability, int> { [Ability.Strength] = 2, [Ability.Constitution] = 1 },
                       made.SkillChoices.Take(made.SkillPicks).ToList(), null, Srd.Items,
                       new List<Core.Magic.Spell>());

            return hero;
        }

        [Fact]
        public void DangerSenseIsADexterityLeanThatAnIncapacitatedBarbarianLoses()
        {
            Actor barbarian = Made("barbarian", 2).Actor;

            Assert.Equal(Advantage.Advantage, barbarian.SaveAdvantage(Ability.Dexterity));
            Assert.Equal(Advantage.Flat, barbarian.SaveAdvantage(Ability.Strength));

            barbarian.Apply(Condition.Incapacitated);

            Assert.Equal(Advantage.Flat, barbarian.SaveAdvantage(Ability.Dexterity));
        }

        [Fact]
        public void AFeaturesBoonsAreTheCreaturesForGoodAndNotItsSpells()
        {
            Actor barbarian = Made("barbarian", 2).Actor;

            Boon sense = barbarian.Boons.All.Single(b => b.Id == "danger_sense");

            Assert.Equal(Duration.Permanent, sense.Duration);
            Assert.Equal("feature:danger_sense", sense.Source);
        }

        [Fact]
        public void TheInitiativeLeanIsAdvantageOnInitiativeAndNothingElse()
        {
            var actor = new Actor("scout", 5, new AbilityScores(), Allegiance.Hero);

            Assert.False(actor.InitiativeAdvantage);

            actor.Boons.Add(Boon.Of(new BoonSpec { Duration = Duration.Permanent, Leans = Leans.AdvantageOnInitiative },
                                    "alert"));

            Assert.True(actor.InitiativeAdvantage);
            Assert.Equal(Advantage.Flat, actor.SaveAdvantage(Ability.Dexterity));
        }

        [Fact]
        public void ASaveLeanAgainstAConditionIsOnlyForThatCondition()
        {
            var actor = new Actor("elf", 5, new AbilityScores(), Allegiance.Hero);

            actor.Boons.Add(Boon.Of(new BoonSpec
            {
                Duration = Duration.Permanent,
                Leans = Leans.AdvantageOnSaves,
                Against = Condition.Charmed
            }, "fey_ancestry"));

            Assert.True(actor.AdvantageOnSaveAgainst(Condition.Charmed));
            Assert.False(actor.AdvantageOnSaveAgainst(Condition.Frightened));

            // not a blanket advantage on Wisdom or Charisma saves
            Assert.Equal(Advantage.Flat, actor.SaveAdvantage(Ability.Wisdom));
        }

        [Fact]
        public void ANaturesDefensesAreOneStoreWithTheBoons()
        {
            var actor = new Actor("golem", 5, new AbilityScores(), Allegiance.Enemy);

            actor.SetDefense(DamageType.Fire, Defense.Resistant);
            actor.SetDefense(DamageType.Fire, Defense.Immune);

            Assert.Equal(Defense.Immune, actor.DefenseAgainst(DamageType.Fire));

            // resistance and vulnerability cancel, SRD 5.2.1
            actor.SetDefense(DamageType.Cold, Defense.Vulnerable);
            actor.Boons.Add(Boon.Of(new BoonSpec
            {
                Duration = Duration.Rest,
                Defenses = new Dictionary<DamageType, Defense> { [DamageType.Cold] = Defense.Resistant },
            }, "warmth"));

            Assert.Equal(Defense.Normal, actor.DefenseAgainst(DamageType.Cold));
        }

        [Fact]
        public void AStatblockReadsTheNewWords()
        {
            Monster spider = Srd.Bestiary.Find("giant_spider");

            Assert.Equal(5, spider.Actions[0].RechargeOn);
            Assert.Equal(13, spider.Actions[0].Dc);

            const string json = @"{""monsters"": [{""id"": ""sprite"", ""hit_points"": 3, ""immune"": [""charmed""],
              ""manoeuvres"": [""hide""],
              ""attacks"": [{""id"": ""sting"", ""damage"": ""1"", ""damage_type"": ""piercing"",
                ""on_hit"": {""amount"": ""1d4"", ""damage_type"": ""poison"", ""condition"": ""poisoned"",
                            ""duration"": ""next_turn_end""}}]}]}";

            Assert.True(MonsterReader.TryRead(json, out IReadOnlyList<Monster> read, out IReadOnlyList<string> problems),
                        string.Join("\n", problems));
            Assert.Contains(Condition.Charmed, read[0].ImmuneTo);
        }

        [Fact]
        public void AnOnHitDurationIsOnlyTheEndOfTheTargetsNextTurn()
        {
            const string json = @"{""monsters"": [{""id"": ""rat"", ""hit_points"": 1,
              ""attacks"": [{""id"": ""bite"", ""damage"": ""1"", ""damage_type"": ""piercing"",
                ""on_hit"": {""condition"": ""prone"", ""duration"": ""rest""}}]}]}";

            MonsterReader.TryRead(json, out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.Contains("'next_turn_end'"));
        }
    }
}
