using System.Collections.Generic;
using Content.Companions;
using Core.Characters;
using Core.Combat;
using Core.Space;
using Game.Board;
using Godot;

namespace Game.Dev
{
    // --- the look test's line-up (cc_task_g-kaykit-heroes-and-monster-companions.md, the screenshots) ----------------
    //
    // On the demonstration table only (DemoTable), from the command line:
    //   --class mage               the demonstration hero is that class (a rogue otherwise), and stands as its mini
    //   --lineup                   a goblin, a bandit and a giant rat in a row beside it, as their statblocks' minis
    //   --companion [look]         the class's companion beside the GM screen, or any look by name (CompanionModels)
    //   --mood alert [--mood-after 2.5]   it feels that, so many seconds in (pleased, alert, quiet, watching)
    // with HeroMinis' own --old-hero, --plain-pose and --empty-handed for the comparisons.
    //
    // Was Table.Lineup.cs, a partial of the table; the demonstration's helper since cc_task_j Part 2
    public sealed class DemoLineup
    {
        readonly Game.Board.Board _board;
        readonly Battlefield _field;

        // the line-up's monsters' minis, by the actor standing as each
        readonly Dictionary<Actor, string> _minis = new Dictionary<Actor, string>();

        public DemoLineup(Game.Board.Board board, Battlefield field)
        {
            _board = board;
            _field = field;
        }

        // the hero as its class's mini, and the line-up's monsters as theirs; the demonstration's own goblin stays
        // the board's EnemyFigure, as it always was
        public void Figure(Content.Sheet.Hero hero)
        {
            _board.FigureFor = actor => ReferenceEquals(actor, hero.Actor)
                ? HeroMinis.For(hero.Class.Id, hero.Figure)
                : _minis.TryGetValue(actor, out string mini) ? MiniModels.For(mini) : null;
        }

        // the monsters, a square apart to the hero's right; true when there was a line-up to stand
        public bool Stand(Cell hero)
        {
            if (!DevArgs.Lineup) return false;

            int east = 1;

            foreach (string id in new[] { "goblin", "bandit", "giant_rat" })
            {
                Content.Monsters.Monster monster = Content.Schema.Library.Srd().Bestiary.Find(id);
                Actor actor = monster.Spawn();
                _minis[actor] = monster.Mini;

                var at = new Cell(hero.X + east++, hero.Y);
                _field.Place(actor, at);
                _board.Place(actor, at);
            }

            return true;
        }

        // the companion by the GM screen, feeling what it was told to, when it was told to
        public void ShowTheCompanion(Game.Table.Table table, Content.Sheet.Hero hero)
        {
            Game.Table.CompanionFigure companion = table.Companion;

            if (!DevArgs.Companion || companion == null) return;

            companion.Wear(DevArgs.CompanionLook ?? hero.Class.Companion, _board?.Paint);
            companion.StandBy(_board, table.GmScreen);

            if (DevArgs.Mood is not { } named || !System.Enum.TryParse(named, true, out Mood mood)) return;

            table.GetTree().CreateTimer(DevArgs.MoodAfter).Timeout += () => companion.Feel(mood);
        }
    }
}
