using System.Linq;
using Content.Combat;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Dice;
using Core.Resolution;

namespace Content.Tests
{
    // cc_task_ui-issues-9-30.md 5: a spell that outlasts a fight, cast on yourself before it, paid for
    // as normal, and still on you when the fight begins
    public class BeforeTheFightTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Mage(params string[] spells)
        {
            var made = Srd.Class("mage");
            var hero = new Hero("Tess", made, Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(made), 3);
            hero.Build(null, made.SkillChoices.Take(made.SkillPicks).ToList(), null, Srd.Items,
                       spells.Select(s => Srd.Spells.Find(s)).ToList());
            return hero;
        }

        [Fact]
        public void MageArmorIsOfferedAndFireBoltAndShieldAreNot()
        {
            Hero tess = Mage("mage_armor", "fire_bolt", "shield", "burning_hands", "charm_person");

            Assert.Equal(new[] { "mage_armor" }, BeforeTheFight.Offered(tess).Select(s => s.Id));
        }

        [Fact]
        public void CastBeforeTheFightItPaysASlotAndLastsIntoTheFight()
        {
            Hero tess = Mage("mage_armor");
            int before = tess.Actor.ArmorClass;
            string slots = tess.Caster.Resource.Describe();

            var cast = BeforeTheFight.Cast(tess, Srd.Spells.Find("mage_armor"), new StandardResolver(new SeededRng(1)),
                                           out var changed);

            Assert.True(cast.Cast, cast.Refusal);
            Assert.Equal(Core.Combat.Stat.ArmorClass, Assert.Single(changed).What);
            Assert.Equal(13 + tess.Actor.Scores.Modifier(Ability.Dexterity), tess.Actor.ArmorClass);
            Assert.NotEqual(before, tess.Actor.ArmorClass);
            Assert.NotEqual(slots, tess.Caster.Resource.Describe());

            // not offered twice, and a fight's end doesn't take it off; a long rest does
            Assert.Empty(BeforeTheFight.Offered(tess));
            tess.Actor.Boons.FightOver();
            Assert.Equal(13 + tess.Actor.Scores.Modifier(Ability.Dexterity), tess.Actor.ArmorClass);
            tess.Actor.Boons.LongRested();
            Assert.Equal(before, tess.Actor.ArmorClass);
        }
    }
}
