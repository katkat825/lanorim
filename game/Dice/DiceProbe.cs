using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Statistics;
using Game.Tray;
using Godot;

namespace Game.Dice
{
    // ARE THE PHYSICS DICE FAIR?
    //
    // This is the check that has to exist, because in this game THE TRAY IS THE RANDOM NUMBER
    // GENERATOR (Table.Ask). SeededRng is proved uniform by a test in core.tests; that proves
    // nothing about a d20 tumbling in a wooden box. A tray that favours its low faces would be a
    // game that favours them, and no amount of correct arithmetic downstream would notice.
    //
    // Headless only, and it takes over the tray: it throws, tallies, throws again, and quits with
    // the verdict. Nothing else may be driving the tray while it runs.
    //
    //   godot --headless --path game res://table.tscn -- --dice d20 --throws 400
    //
    // A SUSPICIOUS verdict happens to a fair die one run in twenty. Throw it again before
    // believing it; BIASED twice running is a real fault in the solid, the tray or the felt.
    public partial class DiceProbe : Node
    {
        public DiceTray Tray { get; set; }

        public Die Shape { get; set; } = Die.D20;

        // how many times the handful goes up, not how many faces are counted - a tray of eight
        // gathers eight samples a throw
        public int Throws { get; set; } = 200;

        // how many dice of that shape go up at once; capped at what the tray seats
        public int Handful { get; set; } = 8;

        FaceTally _tally;
        int _thrown;
        Die[] _handful;

        public override void _Ready()
        {
            if (Tray == null)
            {
                GD.PushError("dice probe: no tray to throw in");
                GetTree().Quit(1);
                return;
            }

            _tally = new FaceTally(Shape.Sides());

            _handful = Enumerable.Repeat(Shape, Math.Clamp(Handful, 1, Tray.Seats)).ToArray();

            Tray.Rolled += Landed;

            GD.Print($"probe   {Throws} throws of {_handful.Length} x {Shape.Label()} " +
                     $"= {Throws * _handful.Length} faces, tray {Tray.SkinName}");

            // how hard this die's numbering pulls in one direction. 0 is balanced; a cube cannot
            // get below 0.657 and a d12 can reach 0.078, so it is only comparable within a shape.
            // A high number here plus a high drift below is a die that rolls high BY DESIGN, and
            // the fix is the numbering rather than the tray.
            GD.Print($"probe   {Shape.Label()} numbering pulls " +
                     $"{DieSolid.For(Shape).Lopsidedness:0.000} toward one side");

            Tray.Throw(_handful);
        }

        void Landed(TrayRoll roll)
        {
            foreach (Felt felt in roll.Felt)
            {
                // a face outside the die is a bug in the solid or the face table, not a result:
                // counting it would hide exactly the thing this is here to find
                if (felt.Value >= 1 && felt.Value <= Shape.Sides())
                {
                    _tally.Add(felt.Value);
                    continue;
                }

                GD.PushError($"dice probe: a {Shape.Label()} read {felt.Value}, which is not a " +
                             "face on it - the solid and its face table disagree");

                GetTree().Quit(1);
                return;
            }

            _thrown++;

            if (_thrown % 25 == 0)
                GD.Print($"probe   {_thrown} of {Throws} throws, {_tally.Total} faces");

            if (_thrown < Throws)
            {
                Tray.Throw(_handful);
                return;
            }

            Report();
        }

        void Report()
        {
            GD.Print("");

            foreach (string line in _tally.DebugLines()) GD.Print(line);

            GD.Print("");

            bool fair = _tally.Verdict != Fairness.Biased &&
                        _tally.DriftVerdict != Fairness.Biased;

            GD.Print(fair
                         ? $"probe   {Shape.Label()} in the {Tray.SkinName} tray: " +
                           $"{_tally.Verdict}, drift {_tally.MeanDriftZ:+0.00;-0.00}"
                         : $"probe   {Shape.Label()} in the {Tray.SkinName} tray is BIASED - " +
                           $"{_tally.Verdict}, drift {_tally.MeanDriftZ:+0.00;-0.00}");

            GetTree().Quit(fair ? 0 : 1);
        }


        // --- asked for on the command line ---------------------------------------------------

        public const string Flag = "--dice";

        // "--dice d20 --throws 400 --handful 8". Returns false when nothing asked, which is every
        // ordinary run of the game.
        public static bool RequestedFrom(string[] args, out Die shape, out int throws,
                                         out int handful)
        {
            shape = Die.D20;
            throws = 200;
            handful = 8;

            if (args == null || args.Length == 0) return false;

            int at = Array.IndexOf(args, Flag);

            if (at < 0) return false;

            if (at + 1 < args.Length && DieExtensions.TryParse(args[at + 1], out Die asked))
                shape = asked;

            throws = Number(args, "--throws", throws);
            handful = Number(args, "--handful", handful);

            return true;
        }

        static int Number(string[] args, string flag, int fallback)
        {
            int at = Array.IndexOf(args, flag);

            return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int value)
                ? value
                : fallback;
        }
    }
}
