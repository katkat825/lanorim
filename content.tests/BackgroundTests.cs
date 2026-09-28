using System.Linq;
using Content.Classes;
using Content.Schema;
using Core.Characters;

namespace Content.Tests
{
    // Kathleen's backgrounds (REVIEW_backgrounds.md, 2026-09-25): the four SRD 5.2.1 ones (SRD p.83)
    // and Recluse, Lanorim's own
    public class BackgroundTests
    {
        static readonly Library Srd = Library.Srd();

        [Fact]
        public void TheFourSrdBackgroundsAndRecluseAreAllThereIs()
        {
            Assert.Equal(new[] { "acolyte", "criminal", "recluse", "sage", "soldier" },
                         Srd.Backgrounds.Select(b => b.Id).OrderBy(i => i));
        }

        [Fact]
        public void RecluseIsKathleensAndMarkedAsNotTheSrds()
        {
            Background recluse = Srd.Background("recluse");

            Assert.True(recluse.NotInSrd);
            Assert.Equal(new[] { Skill.Medicine, Skill.Nature }, recluse.Skills);
            Assert.Equal(new[] { Ability.Constitution, Ability.Wisdom, Ability.Intelligence }, recluse.Abilities);
            Assert.Equal(new[] { "quarterstaff" }, recluse.Gear);
            Assert.Equal(16, recluse.Gold);

            Assert.All(Srd.Backgrounds.Where(b => b.Id != "recluse"), b => Assert.False(b.NotInSrd));
        }

        [Fact]
        public void CreationOffersRecluse()
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);

            Assert.True(making.Pick(Srd.Background("recluse")));
            Assert.Equal("recluse", making.Background.Id);
        }
    }
}
