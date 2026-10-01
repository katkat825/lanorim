using System.Collections.Generic;
using System.Linq;
using Content.Screens;
using Core.Characters;
using Core.Space;
using Game.Access;
using Game.Screens;

namespace Game.Play
{
    public partial class CombatDirector
    {
        // WHAT TAB REACHES IN A FIGHT (cc_task_open-questions-answers.md 4.2; AccessDesk): the hero first, then every
        // piece still up, nearest first (then the turn order), then each button on the bar. A piece is said with its
        // hit points and how far off it is, its square lights, and Enter clicks it - aiming the picked option at it,
        // or walking there with nothing picked, exactly as the mouse would. A button takes the focus, so Enter
        // presses it as Godot always does
        public IReadOnlyList<Reachable> Reachables()
        {
            var reach = new List<Reachable>();

            if (Session == null || Battle == null) return reach;

            Actor hero = Session.Hero.Actor;
            var field = Battle.Fight.Field;

            IEnumerable<Actor> pieces = Battle.Fight.Actors
                .Where(a => !a.IsDown && field.Where(a).HasValue)
                .OrderBy(a => ReferenceEquals(a, hero) ? 0 : 1)
                .ThenBy(a => field.Distance(hero, a))
                .ThenBy(Battle.Fight.InitiativeRank);

            foreach (Actor piece in pieces)
            {
                Cell at = field.Where(piece).Value;

                string says = ReferenceEquals(piece, hero)
                    ? Ui.Say(AccessWords.YouKey, piece.Health.Current, piece.Health.Maximum)
                    : Ui.Say(field.Distance(hero, piece) <= 1 ? AccessWords.BesideKey : AccessWords.PieceKey,
                             Ui.Say(Battle.StatblockOf(piece)?.NameKey ?? piece.Id),
                             piece.Health.Current, piece.Health.Maximum, field.Distance(hero, piece));

                reach.Add(new Reachable(says, () => Board.Pulse(at), () => { if (HerosMove) Click(at); }));
            }

            foreach (Godot.Button button in _hud?.BarButtons ?? Enumerable.Empty<Godot.Button>())
            {
                Godot.Button b = button;
                reach.Add(new Reachable(Ui.Say(AccessWords.ButtonKey, b.Text), () => b.GrabFocus()));
            }

            return reach;
        }
    }
}
