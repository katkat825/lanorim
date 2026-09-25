using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Sheet;

namespace Content.Tests
{
    // Tier 2.4 of the 2026-09-24 run: every class to level 20, every feature v1_class_roster.md
    // keeps, and the SRD 5.2.1 levels that differ from 2014's
    public class ClassRosterTests
    {
        static readonly Library Srd = Library.Srd();

        [Theory]
        [InlineData("barbarian", "rage", "barbarian_unarmored_defense", "reckless_attack", "barbarian_weapon_mastery", "danger_sense")]
        [InlineData("fighter", "fighting_style", "second_wind", "fighter_weapon_mastery", "improved_critical", "action_surge")]
        [InlineData("rogue", "expertise", "sneak_attack", "cunning_action", "uncanny_dodge", "evasion", "thieves_cant")]
        [InlineData("mage", "arcane_spellcasting", "ritual_adept")]
        [InlineData("cleric", "divine_spellcasting", "channel_divinity", "turn_undead", "preserve_life", "divine_strike")]
        [InlineData("paladin", "lay_on_hands", "oath_spellcasting", "paladin_channel_divinity", "paladin_weapon_mastery", "paladins_smite")]
        [InlineData("druid", "primal_spellcasting", "wild_shape")]
        public void EveryFeatureTheRosterKeepsIsThere(string cls, params string[] features)
        {
            CharacterClass made = Srd.Class(cls);

            foreach (string id in features)
                Assert.Contains(made.Features, f => f.Id == id);
        }

        [Fact]
        public void EveryClassGoesToTwenty()
        {
            foreach (string cls in new[] { "barbarian", "fighter", "rogue", "mage", "cleric", "paladin", "druid" })
            {
                CharacterClass made = Srd.Class(cls);

                Assert.Equal(20, made.Features.Max(f => f.Level));
                Assert.True(made.Features.Count >= 12, $"{cls} has {made.Features.Count} features");
            }
        }

        [Fact]
        public void TheFighterAndTheRogueHaveTheirOwnImprovementLevels()
        {
            Assert.Equal(new[] { 4, 6, 8, 12, 14, 16, 19 }, Srd.Class("fighter").ImprovementLevels);
            Assert.Equal(new[] { 4, 8, 10, 12, 16, 19 }, Srd.Class("rogue").ImprovementLevels);
            Assert.Equal(CharacterClass.UsualImprovementLevels, Srd.Class("cleric").ImprovementLevels);

            Assert.Equal(7, Hero.AbilityScoreImprovements(20, Srd.Class("fighter")));
            Assert.Equal(3, Hero.AbilityScoreImprovements(10, Srd.Class("rogue")));
            Assert.Equal(2, Hero.AbilityScoreImprovements(10, Srd.Class("mage")));
        }

        [Theory]
        [InlineData("rogue", "reliable_talent", 7)]
        [InlineData("cleric", "disciple_of_life", 3)]
        [InlineData("cleric", "divine_strike", 7)]
        [InlineData("mage", "potent_cantrip", 3)]
        [InlineData("druid", "natural_recovery", 6)]
        [InlineData("druid", "archdruid", 20)]
        public void FeaturesSitAtTheirSrd521Levels(string cls, string feature, int level)
        {
            Assert.Equal(level, Srd.Class(cls).Features.Single(f => f.Id == feature).Level);
        }
    }
}
