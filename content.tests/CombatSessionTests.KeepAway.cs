using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Space;
using Xunit;

namespace Content.Tests
{
    // the AutoPlayer's ranged caster steps out of melee when that pays (cc_task_cb Part 1.2, AutoPlayer.KeepAway.cs):
    // the shot without disadvantage has to be worth more than the opportunity attack the step draws
    public partial class CombatSessionTests
    {
        static (CombatSession session, Actor goblin) MageBesideAGoblin()
        {
            CombatSession session = Session(Made("mage", 1, "fire_bolt"), new[] { Goblin(1, 0) });
            return (session, session.Battle.Monsters.Single());
        }

        static bool Beside(CombatSession session, Actor goblin) =>
            session.Fight.Field.Distance(session.Hero.Actor, goblin) <= 1;

        [Fact]
        public void AMageStepsOutOfMeleeWhenTheGoblinCannotPunishIt()
        {
            (CombatSession session, Actor goblin) = MageBesideAGoblin();

            // the goblin has spent its reaction: walking away costs nothing
            Assert.True(session.Fight.TakeReaction(goblin));

            new AutoPlayer().TakeTurn(session);

            Assert.False(Beside(session, goblin));
        }

        [Fact]
        public void AMageStaysPutWhenTheOpportunityAttackCostsMoreThanTheClearShotGains()
        {
            (CombatSession session, Actor goblin) = MageBesideAGoblin();

            Cell? was = session.Fight.Field.Where(session.Hero.Actor);
            new AutoPlayer().TakeTurn(session);

            Assert.Equal(was, session.Fight.Field.Where(session.Hero.Actor));
        }

        [Fact]
        public void ThePlainBotNeverSteps()
        {
            (CombatSession session, Actor goblin) = MageBesideAGoblin();
            Assert.True(session.Fight.TakeReaction(goblin));

            new AutoPlayer { KeepsAway = false }.TakeTurn(session);

            Assert.True(Beside(session, goblin));
        }
    }
}
