using System.Collections.Generic;
using Content.Companions;
using Core.Characters;
using Core.Space;
using Game.Board;
using Godot;

namespace Game.Table
{
    public partial class Table
    {
        // --- the look test's line-up (cc_task_g-kaykit-heroes-and-monster-companions.md, the screenshots) --------
        //
        // On the demonstration table only, from the command line:
        //   --class mage               the demonstration hero is that class (a rogue otherwise), and stands as its mini
        //   --lineup                   a goblin, a bandit and a giant rat in a row beside it, as their statblocks' minis
        //   --companion [look]         the class's companion beside the GM screen, or any look by name (CompanionModels)
        //   --mood alert [--mood-after 2.5]   it feels that, so many seconds in (pleased, alert, quiet, watching)
        // with HeroMinis' own --old-hero, --plain-pose and --empty-handed for the comparisons.

        static string Flag(string flag) => Shot.Arg(OS.GetCmdlineUserArgs(), flag) is { } value && !value.StartsWith("--")
            ? value
            : null;

        static bool Has(string flag) => System.Array.IndexOf(OS.GetCmdlineUserArgs(), flag) >= 0;

        static string DemoClass => Flag("--class") ?? "rogue";

        // the hero as its class's mini, and the line-up's monsters as theirs; the demonstration's own goblin stays
        // the board's EnemyFigure, as it always was
        void FigureTheLineup(Content.Sheet.Hero hero, Dictionary<Actor, string> minis)
        {
            Board.FigureFor = actor => ReferenceEquals(actor, hero.Actor)
                ? HeroMinis.For(hero.Class.Id, hero.Figure)
                : minis.TryGetValue(actor, out string mini) ? MiniModels.For(mini) : null;
        }

        // the monsters, a square apart to the hero's right; true when there was a line-up to stand
        bool StandTheLineup(Cell hero, Dictionary<Actor, string> minis)
        {
            if (!Has("--lineup")) return false;

            int east = 1;

            foreach (string id in new[] { "goblin", "bandit", "giant_rat" })
            {
                Content.Monsters.Monster monster = Content.Schema.Library.Srd().Bestiary.Find(id);
                Actor actor = monster.Spawn();
                minis[actor] = monster.Mini;

                var at = new Cell(hero.X + east++, hero.Y);
                _field.Place(actor, at);
                Board.Place(actor, at);
            }

            return true;
        }

        // the companion by the GM screen, feeling what it was told to, when it was told to
        void ShowTheCompanion(Content.Sheet.Hero hero)
        {
            if (!Has("--companion") || Companion == null) return;

            Companion.Wear(Flag("--companion") ?? hero.Class.Companion, Board?.Paint);
            Companion.StandBy(Board, GmScreen);

            if (Flag("--mood") is not { } named || !System.Enum.TryParse(named, true, out Mood mood)) return;

            double after = double.TryParse(Flag("--mood-after"), System.Globalization.NumberStyles.Float,
                                           System.Globalization.CultureInfo.InvariantCulture, out double s) ? s : 2.0;

            GetTree().CreateTimer(after).Timeout += () => Companion.Feel(mood);
        }
    }
}
