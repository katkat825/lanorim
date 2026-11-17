using System.Linq;
using Content.Encounters;
using Content.Monsters;
using Core.Characters;
using Xunit;

namespace Content.Tests
{
    // THE REST OF SRD 5.2.1'S HUMANOIDS (cc_task_ca Part 2; Kathleen 2026-11-16, "add them now"), each number held to the
    // SRD text through the engine's own maths. What the engine can't play is left out, not approximated (the run log's
    // "can't express" column): Parry and Riposte, Pirate's Panache and Captain's Charm, the Tough Boss's push, the
    // Assassin's Poisoned rider, the Archmage's levelled spells and Protective Magic, the Acolyte's Divine Aid
    public partial class ArenaStatblockTests
    {
        [Theory]
        // id, AC, HP, XP by CR, the SRD's name, attacks a turn
        [InlineData("warrior_infantry", 13, 9, 25, "Warrior Infantry", 1)]
        [InlineData("priest_acolyte", 13, 11, 50, "Priest Acolyte", 1)]
        [InlineData("tough", 12, 32, 100, "Tough", 1)]
        [InlineData("pirate", 14, 33, 200, "Pirate", 2)]
        [InlineData("spy", 12, 27, 200, "Spy", 1)]
        [InlineData("bandit_captain", 15, 52, 450, "Bandit Captain", 2)]
        [InlineData("cultist_fanatic", 13, 44, 450, "Cultist Fanatic", 1)]
        [InlineData("druid", 13, 44, 450, "Druid", 2)]
        [InlineData("warrior_veteran", 17, 65, 700, "Warrior Veteran", 2)]
        [InlineData("knight", 18, 52, 700, "Knight", 2)]
        [InlineData("guard_captain", 18, 75, 1100, "Guard Captain", 2)]
        [InlineData("tough_boss", 16, 82, 1100, "Tough Boss", 2)]
        [InlineData("pirate_captain", 17, 84, 2300, "Pirate Captain", 3)]
        [InlineData("assassin", 16, 97, 3900, "Assassin", 3)]
        // the Archmage's own line says "XP 8,000", against the SRD's CR table (CR 12 is 8,400, as the Erinyes' line says);
        // the engine prices by the table
        [InlineData("archmage", 17, 170, 8400, "Archmage", 4)]
        public void TheNewHumanoidIsTheSrds(string id, int armor, int hitPoints, int xp, string name, int attacks)
        {
            TheStatblockIsTheSrds(id, armor, hitPoints, xp, name);
            Assert.Equal(attacks, Srd.Bestiary.Find(id).AttacksPerTurn);
        }

        [Theory]
        // monster, attack, the SRD's to-hit and damage (the on-hit dice are their own rider)
        [InlineData("warrior_infantry", "warrior_infantry_spear", 3, "1d6+1")]
        [InlineData("priest_acolyte", "priest_acolyte_mace", 4, "1d6+2")]
        [InlineData("priest_acolyte", "priest_acolyte_radiant_flame", 4, "2d6")]
        [InlineData("tough", "tough_mace", 4, "1d6+2")]
        [InlineData("tough", "tough_heavy_crossbow", 3, "1d10+1")]
        [InlineData("pirate", "pirate_dagger", 5, "1d4+3")]
        [InlineData("spy", "spy_shortsword", 4, "1d6+2")]
        [InlineData("spy", "spy_hand_crossbow", 4, "1d6+2")]
        [InlineData("bandit_captain", "bandit_captain_scimitar", 5, "1d6+3")]
        [InlineData("bandit_captain", "bandit_captain_pistol", 5, "1d10+3")]
        [InlineData("cultist_fanatic", "cultist_fanatic_pact_blade", 4, "1d8+2")]
        [InlineData("druid", "druid_vine_staff", 5, "1d8+3")]
        [InlineData("druid", "druid_verdant_wisp", 5, "3d6")]
        [InlineData("warrior_veteran", "warrior_veteran_greatsword", 5, "2d6+3")]
        [InlineData("warrior_veteran", "warrior_veteran_heavy_crossbow", 3, "2d10+1")]
        [InlineData("knight", "knight_greatsword", 5, "2d6+3")]
        [InlineData("knight", "knight_heavy_crossbow", 2, "2d10")]
        [InlineData("guard_captain", "guard_captain_javelin", 6, "3d6+4")]
        [InlineData("guard_captain", "guard_captain_longsword", 6, "2d10+4")]
        [InlineData("tough_boss", "tough_boss_warhammer", 5, "2d8+3")]
        [InlineData("tough_boss", "tough_boss_heavy_crossbow", 4, "2d10+2")]
        [InlineData("pirate_captain", "pirate_captain_rapier", 7, "2d8+4")]
        [InlineData("pirate_captain", "pirate_captain_pistol", 7, "2d10+4")]
        [InlineData("assassin", "assassin_shortsword", 7, "1d6+4")]
        [InlineData("assassin", "assassin_light_crossbow", 7, "1d8+4")]
        [InlineData("archmage", "archmage_arcane_burst", 9, "4d10+5")]
        public void TheirAttacksAreTheSrds(string monster, string attack, int toHit, string damage) =>
            Assert.Equal((toHit, damage), Swing(monster, attack));

        [Fact]
        public void TheirRangesRidersAndDefensesAreTheSrds()
        {
            Attack Of(string monster, string attack) => Srd.Bestiary.Find(monster).Attacks.Single(a => a.Id == attack);

            // ranges in squares: Pistol 30/90 ft, Hand Crossbow 30/120, Light Crossbow 80/320, Heavy Crossbow 100/400,
            // Verdant Wisp 90, Arcane Burst 150, Javelin 30/120
            Assert.Equal((6, 18), (Of("bandit_captain", "bandit_captain_pistol").Range, Of("bandit_captain", "bandit_captain_pistol").LongRange));
            Assert.Equal((6, 24), (Of("spy", "spy_hand_crossbow").Range, Of("spy", "spy_hand_crossbow").LongRange));
            Assert.Equal((16, 64), (Of("assassin", "assassin_light_crossbow").Range, Of("assassin", "assassin_light_crossbow").LongRange));
            Assert.Equal((20, 80), (Of("tough", "tough_heavy_crossbow").Range, Of("tough", "tough_heavy_crossbow").LongRange));
            Assert.Equal(18, Of("druid", "druid_verdant_wisp").Range);
            Assert.Equal(30, Of("archmage", "archmage_arcane_burst").Range);
            Assert.Equal((6, 24), (Of("guard_captain", "guard_captain_javelin").Range, Of("guard_captain", "guard_captain_javelin").LongRange));

            // the riders: poison, radiant, necrotic
            Assert.Equal("2d6", Of("spy", "spy_shortsword").OnHit.Single().Amount.ToString());
            Assert.Equal("5d6", Of("assassin", "assassin_shortsword").OnHit.Single().Amount.ToString());
            Assert.Equal("6d6", Of("assassin", "assassin_light_crossbow").OnHit.Single().Amount.ToString());
            Assert.Equal("1d8", Of("knight", "knight_greatsword").OnHit.Single().Amount.ToString());
            Assert.Equal("2d6", Of("cultist_fanatic", "cultist_fanatic_pact_blade").OnHit.Single().Amount.ToString());
            Assert.Equal("1d4", Of("druid", "druid_vine_staff").OnHit.Single().Amount.ToString());

            // Assassin: Evasion, poison resistance; Archmage: Magic Resistance, psychic and Charmed immunity; Knight: Frightened
            Actor assassin = Srd.Bestiary.Find("assassin").Spawn();
            Assert.True(assassin.Is("evasion"));
            Assert.Equal(5, assassin.Suffer(10, DamageType.Poison));

            Actor archmage = Srd.Bestiary.Find("archmage").Spawn();
            Assert.Equal(0, archmage.Suffer(10, DamageType.Psychic));
            Assert.Contains(Condition.Charmed, Srd.Bestiary.Find("archmage").ImmuneTo);
            Assert.Contains(Condition.Frightened, Srd.Bestiary.Find("knight").ImmuneTo);

            // the casters' spells, the ones v1 has
            Assert.Equal(new[] { "command", "hold_person", "spiritual_weapon" },
                         Srd.Bestiary.Find("cultist_fanatic").Spellcasting.Uses.Keys.OrderBy(k => k));
            Assert.Contains("moonbeam", Srd.Bestiary.Find("druid").Spellcasting.Uses.Keys);
            Assert.Equal(3, Srd.Bestiary.Find("archmage").Spellcasting.Uses["misty_step"]);
        }

        [Fact]
        public void TheirSavesAndSkillsAreTheSrds()
        {
            // Spy: Investigation +5, Perception +6, Stealth +6 (expertise); Deception +5 (trained)
            Actor spy = Srd.Bestiary.Find("spy").Spawn();
            foreach (Skill skill in new[] { Skill.Investigation, Skill.Perception, Skill.Stealth })
                Assert.Equal(Training.Expert, spy.TrainingIn(skill));
            Assert.Equal(Training.Proficient, spy.TrainingIn(Skill.Deception));

            // Archmage: Arcana +13 (expertise), Int and Wis saves; Pirate Captain: four saves
            Assert.Equal(Training.Expert, Srd.Bestiary.Find("archmage").Spawn().TrainingIn(Skill.Arcana));
            Assert.Equal(new[] { Ability.Intelligence, Ability.Wisdom }, Srd.Bestiary.Find("archmage").Saves.OrderBy(a => a));
            Assert.Equal(4, Srd.Bestiary.Find("pirate_captain").Saves.Count);
            Assert.Equal(new[] { Ability.Strength, Ability.Constitution, Ability.Charisma },
                         Srd.Bestiary.Find("tough_boss").Saves.OrderBy(a => a));
        }
    }
}
