using System.Linq;
using Content.Schema;
using Core.Magic;

namespace Content.Tests
{
    // SPELL.KINDLY'S SHORTCUT (cc_task_e-shop-species-and-ui-notes.md Part 3): a spell that heals, wards, relieves or
    // stabilizes at all is kind, whatever else it does - right for Greater Restoration, wrong for a future spell that
    // burns a foe and heals its caster, which would then only be aimed at friends. this fails on that spell
    public class KindlyGuardTests
    {
        // kind, and also hurts someone other than its caster: each one here was looked at and is right to be kind
        static readonly string[] Allowed = { "greater_restoration" };

        [Fact]
        public void NoKindSpellHurtsAnyoneButItsCaster()
        {
            var wrong = Library.Srd().Spells.All
                .Where(s => !Allowed.Contains(s.Id))
                .Where(s => (s.Modes.Count == 0 ? new string[] { null } : s.Modes.ToArray()).Any(s.Kindly))
                // what lands on the cast: Haste's lethargy, on end, is the price of the gift (Spell.Kindly), and
                // Invisible is the condition a creature is glad of
                .Where(s => s.Effects.Any(e => e.AimKind != AimKind.Caster && e.Lands != Lands.OnEnd &&
                                               (e.Kind == Primitive.Damage ||
                                                e.Handler.Inflicts(e).HasValue && !e.Handler.Helps(e))))
                .Select(s => s.Id)
                .ToList();

            Assert.True(wrong.Count == 0, "kind spells that hurt someone: " + string.Join(", ", wrong));
        }

        // a mode in which nothing lands on the cast (all of it on end) is not kind: All() of nothing is true
        [Fact]
        public void AModeWithNothingLandingIsNotKind()
        {
            var spell = new Spell("probe_only_on_end", 1, School.Enchantment, new[]
            {
                new SpellEffect(Primitive.Sway, AimKind.Creature) { Lands = Lands.OnEnd },
            });

            Assert.False(spell.Kindly());
        }
    }
}
