using System.Collections.Generic;
using Content.Schema;
using Content.Sheet;
using Core.Characters;

namespace Content.Tests
{
    // milestone levelling, held to what building the same hero fresh at the new level gives -
    // which is the only sensible definition of "levelled up correctly", and the one a save's
    // rebuild relies on
    public class LevelUpTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero WoodElfRogue(int level)
        {
            var hero = new Hero("Tamsin", Srd.Class("rogue"), Srd.Kind("elf"),
                                Srd.Background("criminal"),
                                Creation.Creation.Standard(Srd.Class("rogue")), level,
                                Srd.Kind("elf_wood"));

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Ability.Dexterity] = 2,
                           [Ability.Constitution] = 1,
                       },
                       new[] { Skill.Stealth, Skill.Acrobatics, Skill.Perception,
                               Skill.Investigation },
                       null, Srd.Items);

            return hero;
        }

        [Fact]
        public void LevellingUpDoesNotGrantTheSameFeatureTwice()
        {
            Hero climbed = WoodElfRogue(1);

            climbed.LevelTo(3);
            climbed.LevelTo(7);
            climbed.LevelTo(11);

            Hero fresh = WoodElfRogue(11);

            // Fleet of Foot's five feet, once
            Assert.Equal(fresh.Actor.Speed, climbed.Actor.Speed);

            // Evasion's +2 to Dexterity saves (level 7), once
            Assert.Equal(fresh.Actor.SaveModifier(Ability.Dexterity),
                         climbed.Actor.SaveModifier(Ability.Dexterity));

            // Reliable Talent's +2 to Stealth (level 11), once
            Assert.Equal(fresh.Actor.CheckModifier(Skill.Stealth),
                         climbed.Actor.CheckModifier(Skill.Stealth));
        }

        [Fact]
        public void AFeatureArrivesAtItsLevelAndNotBefore()
        {
            Hero rogue = WoodElfRogue(1);

            Assert.False(rogue.Actor.Is("evasion"));

            rogue.LevelTo(6);
            Assert.False(rogue.Actor.Is("evasion"));

            // SRD 5.2.1 Evasion (p.63) lands at 7
            rogue.LevelTo(7);
            Assert.True(rogue.Actor.Is("evasion"));
        }
    }
}
