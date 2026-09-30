using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Combat;
using Core.Space;

namespace Content.Tests
{
    // EVERY CAST SAYS WHAT IT WAS AND WHAT IT CHANGED (cc_task_ui-issues-9-30.md 1.2). Mage Armor was
    // cast at the table and nothing on screen said so: the armor class changed silently. Now a cast
    // writes "Tess casts Mage Armor on Tess." before its dice and "Tess's Armor Class is now 14" after,
    // for every aim shape
    public partial class CombatSessionTests
    {
        [Fact]
        public void MageArmorOnYourselfSaysSoAndSaysTheNewArmorClass()
        {
            CombatSession session = Session(Made("mage", 3, "mage_armor"), new[] { Goblin(9, 3) }, new Loaded(10));
            FightLog log = Heard(session);
            int lines = log.Lines.Count;
            int before = session.Hero.Actor.ArmorClass;

            session.Select(session.Options().First(o => o.Id == "spell:mage_armor"));
            Assert.Contains(session.Hero.Actor, session.LegalTargets());
            Assert.True(session.Confirm(session.Hero.Actor).Done);

            Assert.Equal(new[] { "cast_on", "changed_armor_class" }, Said(log, lines));

            LogLine changed = log.Lines.Last();
            Assert.Same(session.Hero.Actor, changed.Args[0]);
            Assert.Equal(before, changed.Args[1]);
            Assert.Equal(13 + session.Hero.Actor.Scores.Modifier(Core.Characters.Ability.Dexterity), changed.Args[2]);
            Assert.Equal(session.Hero.Actor.ArmorClass, changed.Args[2]);
        }

        public static IEnumerable<object[]> OneOfEachAim() => new[]
        {
            new object[] { "disguise_self", 3, Targeting.None },
            new object[] { "fire_bolt", 3, Targeting.Creature },
            new object[] { "faerie_fire", 3, Targeting.Square },
            new object[] { "burning_hands", 3, Targeting.Direction },
            new object[] { "lightning_bolt", 5, Targeting.Direction },
        };

        [Theory]
        [MemberData(nameof(OneOfEachAim))]
        public void EveryAimShapeWritesACastLine(string spell, int level, Targeting aim)
        {
            // the goblin two squares east of the hero, in the line and the cone
            CombatSession session = Session(Made("mage", level, spell), new[] { Goblin(2, 0) }, new Loaded(15));
            FightLog log = Heard(session);
            int lines = log.Lines.Count;

            ActionOption option = session.Options().First(o => o.Id == "spell:" + spell);
            Assert.Equal(aim, option.Targeting);
            session.Select(option);

            ActionResult cast = aim switch
            {
                Targeting.Creature => session.Confirm(session.LegalTargets().First()),
                Targeting.Square => session.Confirm(new Cell(2, 0)),
                _ => session.Confirm(),
            };

            Assert.True(cast.Done, spell + ": " + cast.WhyNotKey);
            Assert.StartsWith("cast", Said(log, lines).First());
        }
    }
}
