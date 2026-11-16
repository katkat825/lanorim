using System.Linq;
using Content.Encounters;
using Content.Monsters;
using Content.Schema;
using Core.Characters;

namespace Content.Tests
{
    // THE ARENA'S STATBLOCKS (cc_task_cg, Kathleen 2026-11-09): SRD 5.2.1's Scout (p.322), Berserker (p.263) and
    // Gladiator (p.289), each number held to its page through the engine's own maths, as the 2026-10-03 audit did
    public class ArenaStatblockTests
    {
        static readonly Library Srd = Library.Srd();

        static (int ToHit, string Damage) Swing(string monster, string attack)
        {
            Monster statblock = Srd.Bestiary.Find(monster);
            Actor actor = statblock.Spawn();
            Attack swing = statblock.Attacks.Single(a => a.Id == attack);

            return (swing.Modifier(actor), swing.DamageFor(actor).ToString());
        }

        [Theory]
        // id, AC, HP, XP by CR, the name the SRD gives it
        [InlineData("scout", 13, 16, 100, "Scout")]
        [InlineData("berserker", 13, 67, 450, "Berserker")]
        [InlineData("gladiator", 16, 112, 1800, "Gladiator")]
        public void TheStatblockIsTheSrds(string id, int armor, int hitPoints, int xp, string name)
        {
            Monster monster = Srd.Bestiary.Find(id);

            Assert.NotNull(monster);
            Assert.False(monster.NotInSrd);
            Assert.Equal((armor, hitPoints), (monster.ArmorClass, monster.HitPoints));
            Assert.Equal(xp, FightBudget.Xp(monster));
            Assert.True(monster.Spawn().Is("humanoid"));

            string csv = System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "game.csv"));
            Assert.Equal(name, Locale.Read(csv)[monster.NameKey]);
        }

        [Fact]
        public void TheirAttacksHitAndHurtAsTheSrdSays()
        {
            // Scout: Shortsword +4, 1d6 + 2; Longbow +4, 1d8 + 2, 150/600 ft; two attacks in any combination
            Assert.Equal((4, "1d6+2"), Swing("scout", "scout_shortsword"));
            Assert.Equal((4, "1d8+2"), Swing("scout", "scout_longbow"));
            Assert.Equal((30, 120), (Srd.Bestiary.Find("scout").Attacks.Single(a => a.Id == "scout_longbow").Range,
                                     Srd.Bestiary.Find("scout").Attacks.Single(a => a.Id == "scout_longbow").LongRange));
            Assert.Equal(2, Srd.Bestiary.Find("scout").AttacksPerTurn);

            // Berserker: Greataxe +5, 1d12 + 3
            Assert.Equal((5, "1d12+3"), Swing("berserker", "berserker_greataxe"));
            Assert.Equal(1, Srd.Bestiary.Find("berserker").AttacksPerTurn);

            // Gladiator: Spear +7, 2d6 + 4, reach 5 ft. or 20/60 ft.; three of them
            Assert.Equal((7, "2d6+4"), Swing("gladiator", "gladiator_spear"));
            Assert.True(Srd.Bestiary.Find("gladiator").Attacks.Single().Thrown);
            Assert.Equal(3, Srd.Bestiary.Find("gladiator").AttacksPerTurn);
        }

        [Fact]
        public void TheirSkillsAndSavesAreTheSrds()
        {
            // Scout: Nature +4, Perception +5, Stealth +6, Survival +5 - each the ability and twice the PB
            Actor scout = Srd.Bestiary.Find("scout").Spawn();
            foreach (Skill skill in new[] { Skill.Nature, Skill.Perception, Skill.Stealth, Skill.Survival })
                Assert.Equal(Training.Expert, scout.TrainingIn(skill));

            // Gladiator: Athletics +10 (expertise), Performance +5; Str, Dex and Con saves
            Actor gladiator = Srd.Bestiary.Find("gladiator").Spawn();
            Assert.Equal(Training.Expert, gladiator.TrainingIn(Skill.Athletics));
            Assert.Equal(Training.Proficient, gladiator.TrainingIn(Skill.Performance));
            Assert.Equal(new[] { Ability.Strength, Ability.Dexterity, Ability.Constitution },
                         Srd.Bestiary.Find("gladiator").Saves.OrderBy(a => a));
        }
    }
}
