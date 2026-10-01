using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Characters;

namespace Content.Tests
{
    // ONE "KIND SPELL" (cc_task_d-seams-and-duplication.md §4): the Charmed check and a hero's targeting both ask
    // Spell.Kindly, so a charmed hero is never offered a target the cast then refuses
    public partial class CombatSessionTests
    {
        static CombatSession Charmed(string spell, out Actor charmer, out Actor other)
        {
            CombatSession session = Session(Made("mage", 9, spell), new[] { Goblin(2, 0), Goblin(3, 0) }, new Loaded(10));
            var goblins = session.Fight.Actors.Where(a => a.Side != session.Hero.Actor.Side)
                                 .OrderBy(a => a.Id, System.StringComparer.Ordinal).ToList();

            charmer = goblins[0];
            other = goblins[1];
            session.Hero.Actor.Apply(Condition.Charmed, charmer);

            return session;
        }

        static IReadOnlyList<Actor> TargetsOf(CombatSession session, string spell)
        {
            session.Select(session.Options().First(o => o.Id == "spell:" + spell));

            return session.LegalTargets();
        }

        // every target offered is one the cast takes: try each on a session of its own (Hex would need an ability
        // chosen too; it is in the next test)
        [Theory]
        [InlineData("fire_bolt")]
        [InlineData("invisibility")]
        [InlineData("haste")]
        public void ACharmedHeroIsOfferedNothingTheCastRefuses(string spell)
        {
            int offered = TargetsOf(Charmed(spell, out _, out _), spell).Count;

            Assert.True(offered > 0, spell);

            for (int i = 0; i < offered; i++)
            {
                CombatSession session = Charmed(spell, out _, out _);
                Actor target = TargetsOf(session, spell)[i];

                ActionResult done = session.Confirm(target);
                Assert.True(done.Done, $"{spell} at {target.Id}: {done.WhyNotKey} {done.Casting?.Refusal}");
            }
        }

        [Theory]
        [InlineData("fire_bolt")]
        [InlineData("hex")]
        public void AHarmfulSpellIsNotOfferedAtTheCharmer(string spell)
        {
            CombatSession session = Charmed(spell, out Actor charmer, out Actor other);
            var targets = TargetsOf(session, spell);

            Assert.DoesNotContain(charmer, targets);
            Assert.Contains(other, targets);
        }

        // Hex and Hunter's Mark were "kindly" (a sway with no save) and so could only be aimed at friends
        [Theory]
        [InlineData("hex")]
        [InlineData("hunters_mark")]
        public void AMarkGoesOnAFoe(string spell)
        {
            CombatSession session = Session(Made(spell == "hex" ? "mage" : "druid", 5, spell),
                                            new[] { Goblin(2, 0) }, new Loaded(10));

            Assert.False(Srd.Spells.Find(spell).Kindly());
            Assert.DoesNotContain(session.Hero.Actor, TargetsOf(session, spell));
            Assert.NotEmpty(TargetsOf(session, spell));
        }
    }
}
