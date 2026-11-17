using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Rules;
using Xunit;

namespace Content.Tests
{
    // Lanorim's solo Sneak Attack (cc_task_sa, Kathleen 2026-11-16): "sneak attack works if they don't have
    // disadvantage". A Finesse or Ranged hit, once a turn, whenever the attack roll isn't at Disadvantage - the
    // SRD's "an ally next to the target" can't happen with no party
    public partial class CombatSessionTests
    {
        static (CombatSession session, Actor goblin) RogueBesideAGoblin(int goblinX = 1)
        {
            CombatSession session = Session(Made("rogue", 1), new[] { Goblin(goblinX, 0) });
            return (session, session.Battle.Monsters.Single());
        }

        // the rogue's finesse weapon from the action bar, or a shortbow from the item list (the starting kit
        // has none, and a rider asks only about the weapon)
        static Attack Wielded(CombatSession session, bool ranged) =>
            ranged ? Srd.Items.Find("shortbow").Attack
                   : session.Options().First(o => o.Kind == OptionKind.Attack && o.Attack != null &&
                                                  o.Attack.Finesse && !o.Attack.IsRanged).Attack;

        static bool Sneaks(CombatSession session, Actor goblin, Attack attack) =>
            session.Hero.RidersAt(session.Fight, session.Turn, goblin, attack).Any(r => r.Id == "sneak_attack");

        [Fact]
        public void SneakAttackFiresOnAFlatRollAndThePreviewCountsIt()
        {
            (CombatSession session, Actor goblin) = RogueBesideAGoblin();
            Attack dagger = Wielded(session, ranged: false);

            Assert.Equal(Advantage.Flat, session.Fight.SwingLean(session.Hero.Actor, goblin, dagger));
            Assert.True(Sneaks(session, goblin, dagger));

            ActionOption option = session.Options().First(o => o.Kind == OptionKind.Attack && o.Attack == dagger);
            Assert.True(session.Select(option));

            Preview preview = session.Preview(goblin);
            double sneak = new Core.Dice.DiceRoll(1, Core.Dice.Die.D6).Average;
            Assert.Equal(preview.HitChance * (dagger.DamageFor(session.Hero.Actor).Average + sneak),
                         preview.ExpectedDamage, 3);

            Blow blow = session.Hero.Hit(session.Fight, session.Turn, goblin, dagger);
            Assert.Contains(blow.Riders, r => r.Id == "sneak_attack");

            // still once a turn
            Assert.False(Sneaks(session, goblin, dagger));
        }

        [Fact]
        public void SneakAttackDoesNotFireAtDisadvantage()
        {
            // a shot with an enemy beside you (SRD 5.2.1 Ranged Attacks in Close Combat)
            (CombatSession session, Actor goblin) = RogueBesideAGoblin();
            Attack bow = Wielded(session, ranged: true);

            Assert.Equal(Advantage.Disadvantage, session.Fight.SwingLean(session.Hero.Actor, goblin, bow));
            Assert.False(Sneaks(session, goblin, bow));

            // the same bow from across the room is a flat roll, and it fires
            (CombatSession far, Actor distant) = RogueBesideAGoblin(6);
            Assert.True(Sneaks(far, distant, Wielded(far, ranged: true)));

            // poisoned, in melee
            session.Hero.Actor.Apply(Condition.Poisoned);
            Assert.False(Sneaks(session, goblin, Wielded(session, ranged: false)));
        }

        [Fact]
        public void AdvantageAndDisadvantageCancelAndSneakAttackStillFires()
        {
            (CombatSession session, Actor goblin) = RogueBesideAGoblin();
            Attack dagger = Wielded(session, ranged: false);

            session.Hero.Actor.Apply(Condition.Poisoned);
            goblin.Apply(Condition.Blinded);

            Assert.Equal(Advantage.Cancelled, session.Fight.SwingLean(session.Hero.Actor, goblin, dagger));
            Assert.True(Sneaks(session, goblin, dagger));
        }

        [Fact]
        public void SneakAttackNeverRidesAPlainMeleeWeapon()
        {
            (CombatSession session, Actor goblin) = RogueBesideAGoblin();
            Attack club = Srd.Items.Find("club").Attack;

            Assert.False(Sneaks(session, goblin, club));
        }
    }
}
