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

        // UNARMED STRIKE IN MORE ACTIONS WITH A WEAPON IN HAND (cc_task_e-shop-species-and-ui-notes.md 2.6); with none, a button
        [Fact]
        public void UnarmedStrikeIsInMoreActionsWhileAWeaponIsInHand()
        {
            CombatSession armed = Session(Made("fighter"), new[] { Goblin(8, 3) });
            var hud = new CombatHud(armed);

            Assert.True(armed.Hero.HoldsAWeapon);
            Assert.Contains(hud.Manoeuvres, o => o.Kind == OptionKind.Attack && o.Attack == Content.Sheet.Hero.UnarmedStrike);
            Assert.DoesNotContain(hud.Buttons, o => o.Kind == OptionKind.Attack && o.Attack == Content.Sheet.Hero.UnarmedStrike);
            Assert.Contains(hud.Buttons, o => o.Kind == OptionKind.Attack);

            // the bar's numbers still run buttons first, then the menus
            List<int> buttons = Keys(hud.Buttons);
            Assert.Equal(Enumerable.Range(1, buttons.Count), buttons);

            Content.Sheet.Hero bare = Made("fighter");
            bare.TakeOff(Content.Items.Slot.TwoHand);
            bare.TakeOff(Content.Items.Slot.MainHand);

            CombatSession unarmed = Session(bare, new[] { Goblin(8, 3) });
            var empty = new CombatHud(unarmed);

            Assert.False(bare.HoldsAWeapon);
            Assert.Contains(empty.Buttons, o => o.Kind == OptionKind.Attack && o.Attack == Content.Sheet.Hero.UnarmedStrike);
            Assert.DoesNotContain(empty.Manoeuvres, o => o.Kind == OptionKind.Attack && o.Attack == Content.Sheet.Hero.UnarmedStrike);
        }

        // THE STRIP FOLLOWS THE TABLE (cc_task_e-shop-species-and-ui-notes.md 2.7): the session is on the hero's next turn
        // while the board still plays a goblin's back; the strip and F2 say the goblin's
        [Fact]
        public void WhileAMonstersTurnIsPlayedBackTheStripSaysItsTurnNotYours()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(8, 3) });
            Core.Characters.Actor goblin = session.Fight.Actors.First(a => a != session.Hero.Actor);

            Assert.True(new CombatHud(session).ShowsHerosTurn);

            var played = new CombatHud(session, null, goblin);

            Assert.False(played.ShowsHerosTurn);
            Assert.Equal(Core.Localization.KeyConventions.MonsterName("goblin"), played.TurnShownNameKey);
            Assert.Same(goblin, played.Order.Single(c => c.Current).Actor);

            Assert.Contains(Whereabouts.Of(null, session, goblin), s => s.Key == Whereabouts.TheirTurnKey);
            Assert.DoesNotContain(Whereabouts.Of(null, session, goblin), s => s.Key == Whereabouts.YourTurnKey);
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
