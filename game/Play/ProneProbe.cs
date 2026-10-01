using System.Linq;
using Content.Combat;
using Core.Characters;
using Godot;

namespace Game.Play
{
    // DOES STAND UP STAND THE MINI UP? (cc_task_working-notes-10-01.md 1.2) Headless, on a real fight:
    //
    //   godot --headless --path game res://launch.tscn -- --begin sample_millbrook fighter --start cellar
    //         --autodice --autostory --prone-probe
    //
    // On the hero's first turn it knocks the hero Prone through the fight (as a Shove or a fall would, so the
    // board hears it the way it hears any condition), waits for the board to show it, and measures the figure:
    // lying, and inside its own square. Then it takes Stand Up the way the More Actions menu does
    // (CombatDirector.Pick), waits again, and measures: upright, in exactly the pose Mini.Stand gave it, the piece
    // on the middle of its square (a standing figure's own box can sit off its feet - a cloak trails). Prints
    // "prone   ... check passed" or "prone   FAILED ..." and quits. Developer diagnostics, not localized.
    public partial class ProneProbe : Node
    {
        public CombatDirector Combat { get; set; }

        enum Stage { Waiting, Lying, Standing, Done }

        Stage _stage;
        double _settle;

        // the standing pose Mini.Stand gave the figure, before anything laid it down
        Transform3D _standing;

        public const string Flag = "--prone-probe";

        public override void _Process(double delta)
        {
            if (Combat?.Session == null || _stage == Stage.Done) return;

            CombatSession session = Combat.Session;
            Actor hero = session.Hero.Actor;
            Game.Board.Mini mini = Combat.Board.Of(hero);

            if (mini == null || !Combat.CanAct) { _settle = 0; return; }

            // a beat for the step to play
            _settle += delta;
            if (_settle < 0.5) return;
            _settle = 0;

            switch (_stage)
            {
                case Stage.Waiting:
                    _standing = mini.GetNode<Node3D>(mini.FigurePath).Transform;
                    session.Fight.Afflict(hero, Condition.Prone);
                    _stage = Stage.Lying;
                    break;

                case Stage.Lying:
                    if (!Report("lying", mini, lying: true)) return;

                    ActionOption stand = session.Options().FirstOrDefault(o => o.Kind == OptionKind.StandUp);
                    if (stand == null) { Fail("no Stand Up on offer while Prone"); return; }

                    Combat.Pick(stand);
                    _stage = Stage.Standing;
                    break;

                case Stage.Standing:
                    if (hero.Has(Condition.Prone)) { Fail("Stand Up was picked and the hero is still Prone"); return; }
                    if (!Report("standing", mini, lying: false)) return;

                    Vector3 square = Combat.Board.Metrics.Centre(session.Fight.Field.Where(hero).Value);
                    if (!mini.GetNode<Node3D>(mini.FigurePath).Transform.IsEqualApprox(_standing))
                    {
                        Fail("the figure didn't go back to the pose it stood in");
                        return;
                    }

                    if (mini.Position.DistanceTo(square) > 0.001f)
                    {
                        Fail("the piece isn't on the middle of its square");
                        return;
                    }

                    GD.Print("prone   check passed");
                    _stage = Stage.Done;
                    GetTree().Quit(0);
                    break;
            }
        }

        // the figure's box on the board against the square the piece stands on
        bool Report(string what, Game.Board.Mini mini, bool lying)
        {
            Node3D figure = mini.GetNodeOrNull<Node3D>(mini.FigurePath);
            if (figure == null) return Fail("the mini has no figure");

            Transform3D toMini = figure.Transform;
            Aabb box = Game.Board.Mini.Footprint(figure, toMini);

            float half = mini.CellSize * 0.5f;
            bool inside = box.Position.X >= -half && box.End.X <= half && box.Position.Z >= -half && box.End.Z <= half;
            bool upright = figure.Transform.Basis.Y.Normalized().Dot(Vector3.Up) > 0.99f;
            Vector3 centre = box.GetCenter();

            GD.Print($"prone   {what}: box {box.Size.X * 1000f:0} x {box.Size.Z * 1000f:0} mm, {box.Size.Y * 1000f:0} high, " +
                     $"in a {mini.CellSize * 1000f:0} mm square: {(!lying ? "-" : inside ? "inside" : "OUTSIDE")}, " +
                     $"{(upright ? "upright" : "lying")}, centre off by {new Vector2(centre.X, centre.Z).Length() * 1000f:0} mm, " +
                     $"the piece {(mini.IsProne ? "prone" : "standing")}");

            if (lying)
            {
                if (!mini.IsProne || upright) return Fail("the hero is Prone and the mini isn't lying down");
                if (!inside) return Fail("the lying mini spills out of its square");
                return true;
            }

            if (mini.IsProne || !upright) return Fail("the hero stood up and the mini didn't");

            return true;
        }

        bool Fail(string why)
        {
            GD.PrintErr("prone   FAILED - " + why);
            _stage = Stage.Done;
            GetTree().Quit(1);
            return false;
        }
    }
}
