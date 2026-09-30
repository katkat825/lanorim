using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Screens;
using Core.Space;

namespace Content.Tests
{
    // THE BAR'S NUMBERS (cc_task_table-ui-minis-zoom-damage.md 3.6, docs/combat_ux.md Keys): the buttons
    // from the left, then the spells, then the manoeuvres; an option counts whether or not it can be
    // taken, so walking into reach renumbers nothing. And the pips' totals.
    public partial class CombatSessionTests
    {
        static List<int> Keys(IEnumerable<ActionOption> options) => options.Select(o => o.Hotkey).ToList();

        [Fact]
        public void TheBarsButtonsAreNumberedFirstAndTheMenusAfterThem()
        {
            CombatSession session = Session(Made("mage", 3, "fire_bolt", "burning_hands"), new[] { Goblin(8, 3) });
            var hud = new CombatHud(session);

            List<int> buttons = Keys(hud.Buttons);
            List<int> spells = Keys(hud.Spells);
            List<int> manoeuvres = Keys(hud.Manoeuvres);

            Assert.Equal(Enumerable.Range(1, buttons.Count), buttons);
            Assert.Equal(Enumerable.Range(buttons.Count + 1, spells.Count), spells);
            Assert.All(manoeuvres, k => Assert.True(k == 0 || k > buttons.Count + spells.Count));

            // a key picks the option that shows it
            ActionOption first = hud.Buttons.First();
            Assert.Same(first, hud.Hotkey(1));
        }

        [Fact]
        public void AnOptionKeepsItsNumberWhenItBecomesAvailable()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(4, 1) });

            Dictionary<string, int> before = new CombatHud(session).Bar.ToDictionary(o => o.Id, o => o.Hotkey);
            Assert.Contains(new CombatHud(session).Buttons, o => o.Kind == OptionKind.Attack && !o.Enabled);

            session.Move(new Cell(3, 1));

            var after = new CombatHud(session);
            Assert.Contains(after.Buttons, o => o.Kind == OptionKind.Attack && o.Enabled);
            Assert.All(after.Bar.Where(o => before.ContainsKey(o.Id)), o => Assert.Equal(before[o.Id], o.Hotkey));
        }

        [Fact]
        public void ThePipsCountWhatIsLeftAgainstWhatTheTurnHad()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(8, 3) });

            session.Move(new Cell(2, 0));
            var hud = new CombatHud(session);

            Assert.Equal(2, hud.SquaresMoved);
            Assert.Equal(6, hud.SquaresGiven);
            Assert.Equal(hud.Actions, hud.ActionsGiven);
            Assert.Equal("●○", CombatHud.Pips(1, 2));
        }
    }
}
