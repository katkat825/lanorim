using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Magic;

namespace Content.Tests
{
    // the SRD 5.2.1 check of 2026-09-25, the species (p.84-86): each trait held to its page
    public class SrdSpeciesCheckTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Made(string species, int level, string lineage = null, string cls = "fighter")
        {
            CharacterClass made = Srd.Class(cls);

            var hero = new Hero("Tess", made, Srd.Kind(species), Srd.Background("soldier"),
                                Creation.Creation.Standard(made), level,
                                lineage == null ? null : Srd.Kind(lineage));

            hero.Build(new Dictionary<Ability, int> { [Ability.Strength] = 2, [Ability.Constitution] = 1 },
                       made.SkillChoices.Take(made.SkillPicks).ToList(), null, Srd.Items);

            return hero;
        }

        static Feature Of(Hero hero, string id) => hero.Features.First(f => f.Id == id);

        [Fact]
        public void TheWoodElfMovesThirtyFiveFeetNotForty()
        {
            Assert.Equal(35, Made("elf", 1, "elf_wood").Actor.Speed);
            Assert.DoesNotContain(Srd.Kind("elf_wood").Features, f => f.Id == "fleet_of_foot");
        }

        [Fact]
        public void FeyAncestryIsAdvantageAgainstCharmNotABonusToCharisma()
        {
            Hero elf = Made("elf", 1, "elf_high");

            Assert.True(elf.Actor.HasAdvantage("save_vs:charmed"));
            Assert.Equal(Made("human", 1).Actor.SaveModifier(Ability.Charisma),
                         elf.Actor.SaveModifier(Ability.Charisma));
        }

        [Fact]
        public void BraveAndDwarvenResilienceAreAdvantageOnTheirConditions()
        {
            Assert.True(Made("halfling", 1).Actor.HasAdvantage("save_vs:frightened"));

            Hero dwarf = Made("dwarf", 1);

            Assert.True(dwarf.Actor.HasAdvantage("save_vs:poisoned"));
            Assert.Equal(Defense.Resistant, dwarf.Actor.DefenseAgainst(DamageType.Poison));
        }

        [Fact]
        public void DwarvenToughnessIsOneHitPointALevel()
        {
            Assert.Equal(Made("human", 1).Actor.Health.Maximum + 1, Made("dwarf", 1).Actor.Health.Maximum);
            Assert.Equal(Made("human", 6).Actor.Health.Maximum + 6, Made("dwarf", 6).Actor.Health.Maximum);
        }

        [Fact]
        public void TheDragonbornBreathesItsAncestrysDamageAsManyTimesAsItsProficiencyBonus()
        {
            Hero red = Made("dragonborn", 1, "dragonborn_fire");

            Spell breath = red.Caster.Find("breath_weapon_fire");

            Assert.NotNull(breath);
            Assert.Equal(Ability.Constitution, breath.DcAbility);
            Assert.All(breath.Effects, e => Assert.Equal(DamageType.Fire, e.DamageType));
            Assert.True(red.Caster.CanCast(breath, 0));
            Assert.Equal(2, red.Caster.UseOf("breath_weapon_fire").PerDay);
            Assert.Equal(Defense.Resistant, red.Actor.DefenseAgainst(DamageType.Fire));

            Hero white = Made("dragonborn", 5, "dragonborn_cold");

            Assert.Equal(3, white.Caster.UseOf("breath_weapon_cold").PerDay);
            Assert.Equal(Defense.Resistant, white.Actor.DefenseAgainst(DamageType.Cold));
            Assert.NotEqual(Defense.Resistant, white.Actor.DefenseAgainst(DamageType.Fire));
        }

        [Fact]
        public void TheInfernalTieflingKnowsFireBoltAndThaumaturgyAndGetsItsSpellsAtThreeAndFive()
        {
            Hero first = Made("tiefling", 1);

            Assert.True(first.Caster.IsPrepared("fire_bolt"));
            Assert.True(first.Caster.IsPrepared("thaumaturgy"));
            Assert.False(first.Caster.IsPrepared("hellish_rebuke"));
            Assert.Equal(0, first.Caster.FreeLeft("hellish_rebuke"));

            Hero fifth = Made("tiefling", 5);

            Assert.True(fifth.Caster.IsPrepared("darkness"));
            Assert.Equal(1, fifth.Caster.FreeLeft("hellish_rebuke"));
            Assert.Equal(1, fifth.Caster.FreeLeft("darkness"));
        }

        [Fact]
        public void TheElvenLineagesGetTheirSpellsByLevel()
        {
            Hero drow = Made("elf", 3, "elf_drow");

            Assert.True(drow.Caster.IsPrepared("faerie_fire"));
            Assert.False(drow.Caster.IsPrepared("darkness"));

            Hero high = Made("elf", 5, "elf_high");

            Assert.True(high.Caster.IsPrepared("prestidigitation"));
            Assert.True(high.Caster.IsPrepared("detect_magic"));
            Assert.Equal(1, high.Caster.FreeLeft("misty_step"));

            Assert.True(Made("elf", 5, "elf_wood").Caster.IsPrepared("pass_without_trace"));
        }

        [Fact]
        public void AdrenalineRushIsProficiencyBonusUsesOfTemporaryHitPoints()
        {
            Hero orc = Made("orc", 1);
            Feature rush = Of(orc, "adrenaline_rush");

            Assert.Equal(2, orc.UsesLeft(rush));
            Assert.True(orc.Invoke(rush));
            Assert.Equal(orc.Actor.ProficiencyBonus, orc.Actor.Health.Temporary);
            Assert.Equal(1, orc.UsesLeft(rush));
            Assert.Equal(Recharge.Long, Of(orc, "relentless_endurance").Recharge);
        }

        [Fact]
        public void ResourcefulIsNoLongerASavingThrowBonus() =>
            Assert.Equal(Made("dwarf", 1).Actor.SaveModifier(Ability.Wisdom),
                         Made("human", 1).Actor.SaveModifier(Ability.Wisdom));
    }
}
