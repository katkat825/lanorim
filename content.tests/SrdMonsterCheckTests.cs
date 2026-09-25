using System.Linq;
using Content.Monsters;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    // the SRD 5.2.1 check of 2026-09-25, the monsters: what the statblock fixes need the engine
    // to do, each held to its page
    public class SrdMonsterCheckTests
    {
        static readonly Library Srd = Library.Srd();

        const string Hall = @"
+-+-+-+-+
|@ . . .|
+-+-+-+-+";

        static Encounter Clawed(out Actor ghoul, out Actor hero, bool elf = false)
        {
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            // every die a 5: the claw hits armor class 5, and a Constitution save of 5 fails DC 10
            var fight = new Encounter(new StandardResolver(new ScriptedRng(Enumerable.Repeat(5, 400).ToArray())),
                                      new Battlefield(map), new CombatLog());

            ghoul = Srd.Bestiary.Find("ghoul").Spawn();
            hero = new Actor("hero", 1, new AbilityScores(), Allegiance.Hero);
            hero.SetHealth(new Health(100));
            hero.Armor = new ArmorProfile(ArmorWeight.Heavy, 5);
            hero.Tag("humanoid");
            if (elf) hero.Tag("elf");

            fight.Enlist(ghoul, new Cell(0, 0));
            fight.Enlist(hero, new Cell(1, 0));
            fight.Begin();

            return fight;
        }

        static Turn TurnOf(Encounter fight, Actor actor)
        {
            Turn turn = fight.Next();

            while (!ReferenceEquals(turn.Actor, actor))
            {
                fight.EndTurn();
                turn = fight.Next();
            }

            return turn;
        }

        [Fact]
        public void TheGhoulsParalysisEndsWithItsTargetsNextTurn()
        {
            Encounter fight = Clawed(out Actor ghoul, out Actor hero);
            Attack claw = Srd.Bestiary.Find("ghoul").Attacks.First(a => a.Id == "ghoul_claw");

            fight.Hit(TurnOf(fight, ghoul), hero, claw);
            Assert.True(hero.Has(Condition.Paralyzed));

            fight.EndTurn();

            // "until the end of its next turn" (SRD 5.2.1 p.288): still paralysed through it
            Turn held = fight.Next();
            Assert.Same(hero, held.Actor);
            Assert.True(hero.Has(Condition.Paralyzed));

            fight.EndTurn();
            Assert.False(hero.Has(Condition.Paralyzed));
        }

        [Fact]
        public void TheGhoulsClawDoesNotParalyseAnElf()
        {
            Encounter fight = Clawed(out Actor ghoul, out Actor hero, elf: true);
            Attack claw = Srd.Bestiary.Find("ghoul").Attacks.First(a => a.Id == "ghoul_claw");

            Assert.True(fight.Hit(TurnOf(fight, ghoul), hero, claw).Hit);
            Assert.False(hero.Has(Condition.Paralyzed));
        }

        [Fact]
        public void AStatblocksDoubledSkillIsExpertise()
        {
            Assert.Equal(Training.Expert, Srd.Bestiary.Find("wolf").Spawn().TrainingIn(Skill.Perception));
            Assert.Equal(Training.Proficient, Srd.Bestiary.Find("wolf").Spawn().TrainingIn(Skill.Stealth));
            Assert.Equal(Training.Expert, Srd.Bestiary.Find("priest").Spawn().TrainingIn(Skill.Religion));
        }

        [Fact]
        public void MeleeOrRangedAttacksAreOneThrownAttack()
        {
            foreach ((string monster, string attack) in new[]
                     { ("kobold_warrior", "kobold_dagger"), ("guard", "guard_spear"), ("ogre", "javelin"),
                       ("mage", "arcane_burst") })
            {
                Monster statblock = Srd.Bestiary.Find(monster);
                Attack thrown = statblock.Attacks.First(a => a.Id == attack);

                Assert.True(thrown.Thrown, monster);
                Assert.False(thrown.IsRanged, monster);
                Assert.NotNull(statblock.Opportunity);
            }

            // the mage's Arcane Burst is its opportunity attack now: "reach 5 ft. or range 120 ft."
            Assert.Equal("arcane_burst", Srd.Bestiary.Find("mage").Opportunity.Id);
        }

        [Fact]
        public void GoblinsAreFeyAndTheWerewolfAMonstrosity()
        {
            Assert.True(Srd.Bestiary.Find("goblin").Spawn().Is("fey"));
            Assert.False(Srd.Bestiary.Find("goblin").Spawn().Is("humanoid"));
            Assert.True(Srd.Bestiary.Find("werewolf").Spawn().Is("monstrosity"));
            Assert.DoesNotContain(Srd.Bestiary.Find("imp").Attacks, a => a.Id == "imp_bolt");
        }
    }
}
