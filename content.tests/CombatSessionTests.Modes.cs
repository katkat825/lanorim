using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Combat;
using Core.Space;

namespace Content.Tests
{
    // A SPELL'S MODE IS THE PLAYER'S (cc_task_m3 Part 2): Command offers its five words, and the one picked is the one
    // cast. Picking the spell takes the first word until another is chosen; the bar shows the choice (CombatHudUi)
    public partial class CombatSessionTests
    {
        [Theory]
        [InlineData("grovel")]
        [InlineData("halt")]
        public void CommandOffersItsWordsAndCastsTheOneChosen(string word)
        {
            CombatSession session = Session(Made("mage", 1, "command"), new[] { Goblin(1, 0) });
            Actor goblin = session.Fight.Actors.First(a => !ReferenceEquals(a, session.Hero.Actor));

            ActionOption command = session.Options().First(o => o.Spell?.Id == "command");
            Assert.Equal(new[] { "approach", "drop", "flee", "grovel", "halt" }, command.Modes);

            // picked, it would be Approach; the word chosen replaces it
            Assert.True(session.Select(command));
            Assert.Equal("approach", session.Mode);
            Assert.True(session.Select(session.Selected, word));
            Assert.Equal(word, session.Mode);

            int before = session.Hero.Actor.Health.Current;
            Cell stood = session.Fight.Field.Where(goblin).Value;

            Assert.True(session.Confirm(goblin).Done);
            session.EndTurn();

            // the GM's dice are 1s: the goblin fails its save and spends its turn on the word, not on the hero
            Assert.Equal(word == "grovel", goblin.Has(Condition.Prone));
            Assert.Equal(stood, session.Fight.Field.Where(goblin));
            Assert.Equal(before, session.Hero.Actor.Health.Current);
        }
    }
}
