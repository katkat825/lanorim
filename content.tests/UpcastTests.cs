using System.Linq;
using Content.Schema;
using Content.Spells;
using Core.Characters;
using Core.Magic;

namespace Content.Tests
{
    // cc_task_dedupe-effects.md, Phase 4: what a higher slot adds is one 'upcast' record, and the
    // spell card reads it - which is what made Fog Cloud and Globe of Invulnerability offer their
    // higher levels at last (the gap in the 2026-09-25 run log: their zones grew in play, but the
    // card only looked at dice and targets and never offered a higher slot)
    public class UpcastTests
    {
        static readonly Library Srd = Library.Srd();

        static Caster Wizard()
        {
            var actor = new Actor("wizard", 17, new AbilityScores(10, 10, 14, 18, 10, 10), Allegiance.Hero);
            actor.SetHealth(new Health(80));

            var caster = new Caster(actor, Ability.Intelligence, SpellSlots.For(CasterProgression.Full, 17));

            foreach (Spell spell in Srd.Spells.All) caster.Learn(spell);

            return caster;
        }

        [Theory]
        [InlineData("fog_cloud")]
        [InlineData("globe_of_invulnerability")]
        public void AZoneThatGrowsWithTheSlotIsOfferedAtAHigherLevel(string id)
        {
            Spell spell = Srd.Spells.Find(id);

            Assert.True(spell.Upcastable);

            SpellCard card = SpellCard.Of(spell, Wizard());

            Assert.True(card.HighestLevel > spell.Level, $"{id} offers only level {card.HighestLevel}");
        }

        [Fact]
        public void EverySpellThatGrowsWithTheSlotSaysSoInItsUpcast()
        {
            // the Globe's "one higher per slot level" used to be written only in SpellZone
            SpellEffect globe = Srd.Spells.Find("globe_of_invulnerability").Effects.Single(e => e.Kind == Primitive.Zone);

            Assert.Equal(1, globe.Upcast.BlocksSpellsUpTo);

            SpellEffect fog = Srd.Spells.Find("fog_cloud").Effects.Single(e => e.Kind == Primitive.Zone);

            Assert.Equal(4, fog.Upcast.Radius);
        }

        [Fact]
        public void ACantripIsNeverOfferedHigher()
        {
            Assert.All(Srd.Spells.All.Where(s => s.IsCantrip), s => Assert.False(s.Upcastable, s.Id));
        }
    }
}
