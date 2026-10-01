using System.Linq;
using Content.Combat;
using Content.Screens;

namespace Content.Tests
{
    // F2 "WHERE ARE WE?" (cc_task_open-questions-answers.md 4.2): whole keyed sentences, never glued from parts
    public partial class CombatSessionTests
    {
        [Fact]
        public void InAFightWhereAreWeSaysTheRoundTheTurnTheHitPointsAndTheFoes()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(8, 1), Goblin(8, 3) });

            var said = Whereabouts.Of(null, session);

            Assert.Equal(new[] { Whereabouts.RoundKey, Whereabouts.YourTurnKey, Whereabouts.HealthKey, Whereabouts.FoesKey },
                         said.Select(s => s.Key));
            Assert.Equal(1, said[0].Args[0]);
            Assert.Equal(2, said[3].Args[0]);
            Assert.Equal(session.Hero.Actor.Health.Current, said[2].Args[1]);
        }

        [Fact]
        public void OnTheBookItIsTheBook() =>
            Assert.Equal(new[] { Whereabouts.BookKey }, Whereabouts.AtTheBook().Select(s => s.Key));

        [Fact]
        public void EveryWordTheKeysSayIsAKey() =>
            Assert.All(Whereabouts.Keys().Concat(HelpCard.Keys()).Concat(AccessWords.Keys()),
                       k => Assert.True(Core.Localization.KeyConventions.IsWellFormed(k), k));
    }
}
