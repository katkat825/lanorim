using System.Collections.Generic;
using System.Linq;
using Content.Monsters;
using Content.Schema;
using Core.Characters;

namespace Content.Tests
{
    public class BestiaryTests
    {
        static readonly Library Srd = Library.Srd();

        [Fact]
        public void TheMonsterFileLoadsWithoutAProblem() =>
            Assert.True(Srd.Bestiary.Sound, string.Join("\n", Srd.Bestiary.Problems));

        // CUSTOM MONSTERS, OUT OF THE CC-BY FILE (Kathleen, 2026-10-01, OPEN_QUESTIONS 2): Lanorim's own statblocks
        // live in lanorim.json and say so; the SRD's file holds only the SRD's
        [Fact]
        public void LanorimsOwnStatblocksAreMarkedAndOutOfTheSrdFile()
        {
            Assert.True(MonsterReader.TryRead(Content.Schema.Srd.Read("monsters/monsters.json"), out IReadOnlyList<Monster> srd, out _));
            Assert.True(MonsterReader.TryRead(Content.Schema.Srd.Read("monsters/lanorim.json"), out IReadOnlyList<Monster> ours, out _));

            Assert.All(srd, m => Assert.False(m.NotInSrd, m.Id));
            Assert.All(ours, m => Assert.True(m.NotInSrd, m.Id));
            Assert.Equal(new[] { "death_knight", "goblin_archer" }, ours.Select(m => m.Id).OrderBy(id => id));
            Assert.NotNull(Srd.Bestiary.Find("death_knight"));
        }

        [Fact]
        public void TheGoblinOfTheFirstSliceIsThere()
        {
            Monster goblin = Srd.Bestiary.Find("goblin");

            Assert.NotNull(goblin);
            // SRD 5.2.1 Goblin Warrior, p.290: HP 10 (3d6)
            Assert.Equal(10, goblin.HitPoints);
            Assert.Equal(15, goblin.ArmorClass);
            Assert.Equal(0.2, goblin.Challenge);
        }

        [Fact]
        public void EachMultiattackIsTheSrdStatblocks()
        {
            // SRD 5.2.1, checked 2026-09-25. a monster not listed here has no Multiattack, and
            // attacks once a turn (decisions_checklist.md section 1: the two actions are the hero's)
            var srd = new Dictionary<string, string>
            {
                ["goblin_boss"] = "any + any",
                ["werewolf"] = "claw|longbow + claw|longbow|werewolf_bite",
                ["ghoul"] = "ghoul_bite + ghoul_bite",
                ["brown_bear"] = "bear_bite + bear_claw",
                ["owlbear"] = "owlbear_rend + owlbear_rend",
                ["priest"] = "priest_mace|radiant_flame + priest_mace|radiant_flame",
                ["mage"] = "any + any + any",
                // not an SRD 5.2.1 statblock at all - see the 2026-09-25 run log
                ["death_knight"] = "any + any",
            };

            foreach (Monster monster in Srd.Bestiary.All)
            {
                if (srd.TryGetValue(monster.Id, out string listed))
                    Assert.Equal(listed, monster.Multiattack?.ToString());
                else
                    Assert.True(monster.Multiattack == null, $"{monster.Id} has a Multiattack");
            }
        }

        [Fact]
        public void AMultiattackOfThreeLoads()
        {
            Monster mage = Srd.Bestiary.Find("mage");

            Assert.Equal(3, mage.AttacksPerTurn);
            Assert.Equal(1, mage.Budget().ActionsFor(2));
        }

        [Fact]
        public void AMultiattackHasNoCapAndNamesOnlyItsOwnAttacks()
        {
            const string four = @"{""monsters"": [{""id"": ""hydra_ish"", ""multiattack"": 4,
                ""attacks"": [{""id"": ""bite"", ""damage"": ""1d10"", ""damage_type"": ""piercing""}]}]}";

            Assert.True(MonsterReader.TryRead(four, out IReadOnlyList<Monster> read, out var problems),
                        string.Join("\n", problems));
            Assert.Equal(4, read[0].AttacksPerTurn);

            const string wrong = @"{""monsters"": [{""id"": ""bear_ish"", ""multiattack"": [""bite"", ""tail""],
                ""attacks"": [{""id"": ""bite"", ""damage"": ""1d10"", ""damage_type"": ""piercing""}]}]}";

            Assert.False(MonsterReader.TryRead(wrong, out _, out problems));
            Assert.Contains(problems, p => p.Contains("tail"));
        }

        [Fact]
        public void AMonsterHasABonusActionOnlyIfItsStatblockDoes()
        {
            // the goblin's Nimble Escape; a wolf has none
            Monster goblin = Srd.Bestiary.Find("goblin");

            Assert.Equal(Manoeuvre.Disengage | Manoeuvre.Hide, goblin.BonusManoeuvres);
            Assert.Equal(1, goblin.Budget().BonusActionsFor(2));
            Assert.Equal(Manoeuvre.Disengage | Manoeuvre.Hide, goblin.Spawn().QuickOnBonus);

            Assert.Equal(0, Srd.Bestiary.Find("wolf").Budget().BonusActionsFor(2));

            // the mage's Misty Step is a bonus-action spell
            Monster mage = Srd.Bestiary.Find("mage");
            Assert.Equal(1, mage.Budget(mage.CasterFor(mage.Spawn(), Srd.Spells)).BonusActionsFor(2));
        }

        [Fact]
        public void EveryMonsterHasSomethingToAttackWith() =>
            Assert.All(Srd.Bestiary.All, m => Assert.NotEmpty(m.Attacks));

        [Fact]
        public void EveryMonstersMiniIsNamed()
        {
            // v1_minis_map.md: a small Quaternius pack stretched across many statblocks, so the
            // model is named on the statblock and several share one
            foreach (Monster monster in Srd.Bestiary.All)
                Assert.False(string.IsNullOrEmpty(monster.Mini), monster.Id);

            Assert.True(Srd.Bestiary.All.Select(m => m.Mini).Distinct().Count() <
                        Srd.Bestiary.Count,
                        "no mini is reused - the point of the map is that several statblocks share one");
        }

        [Fact]
        public void ASpawnedMonsterHasTheStatblocksNumbers()
        {
            Actor goblin = Srd.Bestiary.Find("goblin").Spawn();

            Assert.Equal(10, goblin.Health.Maximum);
            Assert.Equal(15, goblin.ArmorClass);
            Assert.Equal(Allegiance.Enemy, goblin.Side);

            // Stealth +6: Expertise (SRD 5.2.1 p.290)
            Assert.Equal(Training.Expert, goblin.TrainingIn(Skill.Stealth));
        }

        [Fact]
        public void TwoSpawnsAreTwoDifferentCreatures()
        {
            Monster goblin = Srd.Bestiary.Find("goblin");

            Actor one = goblin.Spawn("goblin_1");
            Actor two = goblin.Spawn("goblin_2");

            one.Suffer(5, DamageType.Slashing);

            Assert.Equal(10, two.Health.Current);
        }

        [Fact]
        public void ASkeletonShrugsOffPoisonAndFearsAMace()
        {
            Actor skeleton = Srd.Bestiary.Find("skeleton").Spawn();

            Assert.Equal(0, skeleton.Suffer(10, DamageType.Poison));

            // doubled to 20, but a skeleton only has 13 to give: Suffer reports what actually
            // came off the hit points, which is what the damage line on screen says
            Assert.Equal(13, skeleton.Suffer(10, DamageType.Bludgeoning));
            Assert.True(skeleton.IsDown);
        }

        [Fact]
        public void TheSrd521WerewolfHasNoResistanceToSteel()
        {
            // SRD 5.2.1 p.339 prints no Resistances line for the werewolf (the older SRD's
            // nonmagical-weapon resistance is gone)
            Actor werewolf = Srd.Bestiary.Find("werewolf").Spawn();

            Assert.Equal(10, werewolf.Suffer(10, DamageType.Slashing));
            Assert.Equal(71, Srd.Bestiary.Find("werewolf").HitPoints);
        }

        [Fact]
        public void TheBestiaryCanBeAskedForSomethingOfARoughSize()
        {
            Assert.Contains(Srd.Bestiary.Around(0.2, 0.1), m => m.Id == "goblin");
            Assert.DoesNotContain(Srd.Bestiary.Around(0.2, 0.1), m => m.Id == "ogre");
        }

        [Fact]
        public void TagsAreThereForTurnUndeadToRead()
        {
            string[] undead = Srd.Bestiary.All.Where(m => m.Tags.Contains("undead")).Select(m => m.Id).ToArray();

            Assert.Contains("skeleton", undead);
            Assert.Contains("zombie", undead);
            Assert.DoesNotContain("goblin", undead);
        }

        // cc_task_godfiles-dupes-efficiency.md #10: a statblock's attack names the weapon and
        // writes only what differs
        [Fact]
        public void AStatblockAttackThatNamesAWeaponIsThatWeapon()
        {
            Attack bow = Srd.Bestiary.Find("goblin").Attacks.Single(a => a.Id == "goblin_shortbow");
            Attack shelf = Srd.Items.Find("shortbow").Attack;

            Assert.Equal(shelf.Damage, bow.Damage);
            Assert.Equal(shelf.DamageType, bow.DamageType);
            Assert.Equal(shelf.Range, bow.Range);
            Assert.Equal(shelf.LongRange, bow.LongRange);
            Assert.Equal(Ability.Dexterity, bow.Ability);
            Assert.Equal(Hand.Main, bow.Hand);

            Attack scimitar = Srd.Bestiary.Find("goblin").Attacks.Single(a => a.Id == "scimitar");
            Assert.True(scimitar.Finesse);
        }

        [Fact]
        public void WhatAStatblockWritesBeatsTheWeapon()
        {
            const string json = @"{""monsters"": [{""id"": ""hill_brute"", ""hit_points"": 30,
              ""attacks"": [{""weapon"": ""javelin"", ""damage"": ""2d6"", ""held"": true}]}]}";

            Assert.True(MonsterReader.TryRead(json, out IReadOnlyList<Monster> read, out IReadOnlyList<string> problems),
                        string.Join("\n", problems));

            Attack javelin = read[0].Attacks.Single();
            Assert.Equal("javelin", javelin.Id);
            Assert.Equal("2d6", javelin.Damage.ToString());
            Assert.Equal(Srd.Items.Find("javelin").Attack.Range, javelin.Range);
        }

        [Fact]
        public void AWeaponThatIsNoSrdWeaponIsRefused()
        {
            const string json = @"{""monsters"": [{""id"": ""rat"", ""hit_points"": 1,
              ""attacks"": [{""weapon"": ""laser_sword""}]}]}";

            MonsterReader.TryRead(json, out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.Contains("'laser_sword' isn't one"));
        }
    }
}
