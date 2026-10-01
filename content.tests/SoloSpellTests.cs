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

        [Fact]
        public void SpareTheDyingIsTheOneSpellAPartyOfOneCantCast()
        {
            Assert.Equal(new[] { "spare_the_dying" },
                         Srd.Spells.All.Where(s => s.Solo == Solo.Unavailable).Select(s => s.Id));
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
