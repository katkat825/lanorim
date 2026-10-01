using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Space;

namespace Content.Tests
{
    // A PARTY OF ONE (cc_task_ui-issues-10-01.md 2.1): "when selecting a healing spell, it should
    // automatically heal self since there's no other party members". The rule is general: a
    // creature-aimed option whose only legal target is the caster goes on the caster
    public partial class CombatSessionTests
    {
        [Theory]
        [InlineData("cleric", "cure_wounds")]
        [InlineData("cleric", "healing_word")]
        [InlineData("mage", "mage_armor")]
        public void AKindlySpellWithNoOneElseToAimAtIsAimedAtYou(string cls, string spell)
        {
            CombatSession session = Session(Made(cls, 3, spell), new[] { Goblin(9, 3) }, new Loaded(10));

            session.Select(session.Options().First(o => o.Id == "spell:" + spell));

            Assert.True(session.OnlyTargetIsYou, spell);
            Assert.True(session.Confirm(session.Hero.Actor).Done);
        }

        // ON YOURSELF BY ACCIDENT NO MORE (cc_task_open-questions-answers.md 3.5): Invisibility (a condition a creature is
        // glad of), Haste (whose lethargy only lands when it ends) and Enlarge (saved against only by the unwilling)
        // could only be aimed at foes, so a party of one could never cast them on themselves
        [Theory]
        [InlineData("invisibility")]
        [InlineData("greater_invisibility")]
        [InlineData("haste")]
        public void AGiftASpellGivesGoesOnYou(string spell)
        {
            CombatSession session = Session(Made("mage", 9, spell), new[] { Goblin(9, 3) }, new Loaded(10));

            session.Select(session.Options().First(o => o.Id == "spell:" + spell));

            Assert.True(session.OnlyTargetIsYou, spell);
            Assert.True(session.Confirm(session.Hero.Actor).Done, spell);
        }

        [Fact]
        public void EnlargeGoesOnYouAndReduceOnAFoe()
        {
            CombatSession session = Session(Made("mage", 5, "enlarge_reduce"), new[] { Goblin(2, 0) }, new Loaded(10));
            ActionOption option = session.Options().First(o => o.Id == "spell:enlarge_reduce");

            session.Select(option, "enlarge");
            Assert.True(session.OnlyTargetIsYou);

            session.Select(option, "reduce");
            Assert.DoesNotContain(session.Hero.Actor, session.LegalTargets());
            Assert.NotEmpty(session.LegalTargets());
        }

        [Fact]
        public void AHarmfulSpellIsNeverAimedAtYou()
        {
            CombatSession session = Session(Made("mage", 3, "fire_bolt"), new[] { Goblin(2, 0) }, new Loaded(10));

            session.Select(session.Options().First(o => o.Id == "spell:fire_bolt"));

            Assert.False(session.OnlyTargetIsYou);
        }

        [Fact]
        public void AnAllyOnTheBoardMeansYouAimAsUsual()
        {
            CombatSession session = Session(Made("cleric", 3, "cure_wounds"), new[] { Goblin(9, 3) }, new Loaded(10));
            Cell here = session.Fight.Field.Where(session.Hero.Actor).Value;

            Assert.True(session.Fight.Join(new Actor("summoned", 1, null, Allegiance.Hero),
                                           new Cell(here.X + 1, here.Y)));

            session.Select(session.Options().First(o => o.Id == "spell:cure_wounds"));

            Assert.Equal(2, session.LegalTargets().Count);
            Assert.False(session.OnlyTargetIsYou);
        }

        [Fact]
        public void NothingSelectedIsNotAimedAtAnyone()
        {
            CombatSession session = Session(Made("cleric", 3, "cure_wounds"), new[] { Goblin(9, 3) });

            Assert.False(session.OnlyTargetIsYou);
        }

        // a hero who learned Spare the Dying before it was taken off the solo lists (an old save) isn't
        // offered it alone, and is offered it again once someone else is on their side
        [Fact]
        public void SpareTheDyingIsOnlyOfferedWithAnAllyOnTheBoard()
        {
            CombatSession session = Session(Made("cleric", 3, "spare_the_dying"), new[] { Goblin(9, 3) });

            Assert.DoesNotContain(session.Options(), o => o.Id == "spell:spare_the_dying");

            Cell here = session.Fight.Field.Where(session.Hero.Actor).Value;
            Assert.True(session.Fight.Join(new Actor("summoned", 1, null, Allegiance.Hero),
                                           new Cell(here.X + 1, here.Y)));

            Assert.Contains(session.Options(), o => o.Id == "spell:spare_the_dying");
        }
    }
}
