using System.Linq;
using Content.Schema;
using Core.Magic;
using Xunit;

namespace Content.Tests
{
    // SPARE THE DYING ISN'T AN OPTION FOR A PARTY OF ONE (cc_task_ui-issues-10-01.md 2.2). Kathleen: "if we
    // can't cast Spare the Dying on self, then it should not be a spell option". It stays in the data,
    // marked "solo": "unavailable", and is never offered to a solo hero
    public class SoloSpellTests
    {
        static readonly Library Srd = Library.Srd();

        // RAISE DEAD JOINS IT (cc_task_open-questions-answers.md 3.5). Kathleen: "ditch any spells that can't be used
        // by a party of one unless they can be used out of combat". Both have an out-of-combat use on an NPC, but a
        // campaign has no way to ask the hero to cast a spell outside a fight (no <<cast>> request; 2026-10-03), so
        // both are off the solo lists until it does. Every other spell was checked and works alone
        [Fact]
        public void SpareTheDyingAndRaiseDeadAreTheSpellsAPartyOfOneCantCast()
        {
            Assert.Equal(new[] { "raise_dead", "spare_the_dying" },
                         Srd.Spells.All.Where(s => s.Solo == Solo.Unavailable).Select(s => s.Id).OrderBy(id => id));
        }

        [Theory]
        [InlineData("cleric", 9)]
        [InlineData("paladin", 17)]
        [InlineData("mage", 9)]
        public void CreationNeverOffersRaiseDead(string cls, int level)
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);
            making.StartAt(level);
            making.Pick(Srd.Class(cls));

            Assert.DoesNotContain(making.SpellChoices, s => s.Id == "raise_dead");
        }

        [Theory]
        [InlineData("cleric", 1)]
        [InlineData("cleric", 4)]
        [InlineData("cleric", 10)]
        [InlineData("druid", 1)]
        [InlineData("druid", 10)]
        [InlineData("druid", 20)]
        public void CreationNeverOffersItAndStillFinishes(string cls, int level)
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);
            making.StartAt(level);
            making.Pick(Srd.Class(cls));

            Assert.DoesNotContain(making.SpellChoices, s => s.Id == "spare_the_dying");

            foreach (Spell spell in making.SpellChoices.Where(s => s.IsCantrip).ToList()) making.Learn(spell);

            // the SRD's column is capped at what is on offer, so the cantrips page can always be finished
            Assert.Equal(0, making.CantripPicksLeft);
            Assert.True(making.CantripPicks > 0);
        }

        [Fact]
        public void ALevelTenClericLearnsFourCantripsNotFive()
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);
            making.StartAt(10);
            making.Pick(Srd.Class("cleric"));

            Assert.Equal(5, Srd.Class("cleric").Spellcasting.CantripsAt(10));
            Assert.Equal(4, making.CantripPicks);
        }
    }
}
