using Core.Characters;
using Core.Dice;
using Core.Localization;
using Game.Access;
using Game.Dice;
using Game.Localization;
using Game.Tray;
using Godot;

namespace Game.Table
{
    // THE TABLE. Everything the player looks at stands on it, and this is the node that owns the
    // wiring between the things on it and the rules underneath.
    //
    // It is deliberately thin. The tray throws dice and says what they came up; core decides what
    // that means; this is the seam, and the seam is about ten lines long - Ask() below.
    //
    // TO LAY OUT: the tray's place on the table, the lamp, the felt, the GM screen, the companion
    // and the help button are all yours. What is here is one tray, one camera and one light, so
    // the scene opens and something happens.
    public partial class Table : Node3D
    {
        [Export] public NodePath TrayPath { get; set; } = "DiceTray";

        [Export] public NodePath CameraPath { get; set; } = "TableCamera";

        [Export] public NodePath BoardPath { get; set; } = "Board";

        // the captions card. null until the settings page turns it on
        [Export] public NodePath CaptionsPath { get; set; }

        public DiceTray Tray { get; private set; }

        public TableCamera Camera { get; private set; }

        public Game.Board.Board Board { get; private set; }

        public Captioned Captions { get; private set; }

        // the GM's screen on the far side of the board (Tier 3d); placed whenever a map is laid
        public GmScreen GmScreen { get; private set; }

        readonly ILocalizer _text = new GodotLocalizer();

        // what the table is waiting on: the question asked of the dice, and who to tell
        Ask _asking;

        public override void _Ready()
        {
            Tray = GetNodeOrNull<DiceTray>(TrayPath);
            Camera = GetNodeOrNull<TableCamera>(CameraPath);
            Board = GetNodeOrNull<Game.Board.Board>(BoardPath);

            Captions = CaptionsPath == null || CaptionsPath.IsEmpty
                ? null
                : GetNodeOrNull<Captioned>(CaptionsPath);

            // one in the scene is used as it is (its dials are Kathleen's); otherwise the blank one
            GmScreen = GetNodeOrNull<GmScreen>("GmScreen");

            if (GmScreen == null)
            {
                GmScreen = new GmScreen { Name = "GmScreen" };
                AddChild(GmScreen);
            }

            if (Tray == null)
            {
                GD.PushError($"table: no dice tray at '{TrayPath}' - there is nothing to roll");
                return;
            }

            // the captions card tells the things that make noise about itself, rather than hunting
            // them; one walk of the tree at boot
            Captions?.Tells(this);

            // THE LOCALE PROBE ASKS ONE QUESTION AND LEAVES. Nothing else is wired for it,
            // because it is about the words and not about the table.
            if (LocaleProbe.RequestedFrom(OS.GetCmdlineUserArgs()))
            {
                AddChild(new LocaleProbe { Name = "LocaleProbe" });
                return;
            }

            // THE FAIRNESS SWEEP TAKES OVER THE TRAY. Handed the dice before anything below is
            // wired, because the probe drives the tray itself and a second listener would throw
            // on top of it.
            if (DiceProbe.RequestedFrom(OS.GetCmdlineUserArgs(),
                                        out Die shape, out int throws, out int handful))
            {
                AddChild(new DiceProbe
                {
                    Name = "DiceProbe",
                    Tray = Tray,
                    Shape = shape,
                    Throws = throws,
                    Handful = handful,
                });

                return;
            }

            Tray.Rolled += Read;

            GD.Print("table   Q and E turn the table a quarter, - and = zoom, space throws");

            // A CAMPAIGN TO PLAY: the launch screen made or loaded a character, so the table is the
            // game's and not the demonstration's. Opened on its own (the editor, check-table), the
            // demonstration still shows the whole stack working
            if (Game.Play.GameState.Run != null)
                AddChild(new Game.Play.PlayDirector { Name = "Play", Table = this });
            else
                Demonstrate();

            // and a picture of it, if one was asked for. Added last so it photographs the table
            // with everything already on it
            if (Shot.RequestedFrom(OS.GetCmdlineUserArgs(), out string path, out int after))
                AddChild(new Shot { Name = "Shot", Path = path, After = after });
        }

        // ONE REAL CHECK, THROWN ON THE REAL TRAY, so opening the scene shows the whole stack
        // working: a hero built from the SRD data, a d20 that tumbles in a wooden box, and core
        // reading the face off the felt. Delete it the moment there is a campaign to play instead.
        Actor _someone;
    }
}
