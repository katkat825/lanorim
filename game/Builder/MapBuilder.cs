using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Core.Space;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // THE MAP BUILDER AT THE TABLE (cc_task_f Part 2; v1_build_order.md Phase 2). The table's own board draws the map,
    // so what the author sees is what the player gets; the table's camera turns (Q, E) and zooms, with a straight-down
    // view for drawing. This node is a view over MapEditor (through MapBuilderView): it turns the mouse into a point in
    // squares and the keys into the editor's verbs, and lays the board again after every change. No map rule is here.
    //
    // Opened from the book's "Map builder" (Launch.Builder), which leaves GameState.Building set for the table
    public partial class MapBuilder : Node
    {
        public Game.Table.Table Table { get; set; }

        public MapBuilderView View { get; set; }

        public MapEditor Editor => View.Editor;

        MapBuilderUi _ui;
        BuilderMarks _marks;

        // where a press began (a drag), and what the pointer is over now
        MapTarget? _pressed;
        MapTarget _over;

        float _tablePitch;

        public override void _Ready()
        {
            _marks = new BuilderMarks { Name = "BuilderMarks", Board = Table.Board };
            Table.Board.AddChild(_marks);

            var layer = new CanvasLayer { Name = "Ui" };
            AddChild(layer);

            _ui = new MapBuilderUi { Name = "Builder", Builder = this };
            layer.AddChild(_ui);

            _tablePitch = Table.Camera?.Pitch ?? 45f;

            Lay();
            GD.Print($"builder {View.Id} in {View.Campaign}: {View.Draft}");

            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), MapBuilderProbe.Flag) >= 0)
                AddChild(new MapBuilderProbe { Name = "MapBuilderProbe", Builder = this });
        }

        // --- the board ----------------------------------------------------------------------------------------------

        // the map as it stands now, on the table's board, with its props, spawns and start
        void Lay()
        {
            MapLayout map = View.Draft.Layout();

            Table.Board.Lay(map);
            Table.Board.Dress(View.Draft.Props);
            Table.GmScreen?.StandBehind(Table.Board);

            _marks.Markers(View.Draft, Ui.Say(MapEditor.ToolKey(MapTool.Start)));
            Hover(_over);
        }

        // after anything that changed the map: the board again, and the panels
        public void Changed()
        {
            Lay();
            _ui.Refresh();
        }

        // --- the pointer --------------------------------------------------------------------------------------------

        public override void _UnhandledInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseMotion motion:
                    // a drag let go over a panel never reached here: the button is up, so there's no drag
                    if ((motion.ButtonMask & MouseButtonMask.Left) == 0) _pressed = null;

                    Hover(Aim(motion.Position));
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left } click:
                    if (click.Pressed)
                    {
                        _pressed = Aim(click.Position);
                    }
                    else if (_pressed is MapTarget from)
                    {
                        _pressed = null;
                        Do(Editor.Apply(from, Aim(click.Position)) > 0);
                    }

                    GetViewport().SetInputAsHandled();
                    break;

                case InputEventKey { Pressed: true, Echo: false } key when Keyed(key):
                    GetViewport().SetInputAsHandled();
                    break;
            }
        }

        MapTarget Aim(Vector2 screen) =>
            Table.Board.GridPointUnder(screen) is Vector2 at ? Editor.Aim(at.X, at.Y) : MapTarget.Nothing;

        // the hover highlight: what a click here, or the drag so far, would change
        void Hover(MapTarget over)
        {
            _over = over;

            var (squares, lines) = Editor.Preview(_pressed ?? over, over);

            if (squares.Count > 0) Table.Board.Flash(squares, BuilderLayout.Current.HoverColour);
            else Table.Board.Unflash();

            _marks.Lines(lines);
        }

        // Ctrl+Z, Ctrl+Y (or Ctrl+Shift+Z), Ctrl+S, and R to turn a prop. an author's keys, the usual ones everywhere
        bool Keyed(InputEventKey key)
        {
            bool ctrl = key.CtrlPressed || key.MetaPressed;

            if (ctrl && key.Keycode == Key.Z && !key.ShiftPressed) Undo();
            else if (ctrl && (key.Keycode == Key.Y || key.Keycode == Key.Z && key.ShiftPressed)) Redo();
            else if (ctrl && key.Keycode == Key.S) Save();
            else if (!ctrl && key.Keycode == Key.R) Rotate(key.ShiftPressed ? -1 : 1);
            else return false;

            return true;
        }

        // --- the verbs the panels and the keys share ------------------------------------------------------------------

        void Do(bool changed)
        {
            if (changed) Changed();
        }

        public void Undo() => Do(View.Draft.Undo());

        public void Redo() => Do(View.Draft.Redo());

        public void Rotate(int quarters)
        {
            Editor.Rotate(quarters);
            _ui.Refresh();
        }

        public void Save()
        {
            Said refused = View.Save();

            _ui.Notice(refused == null ? Ui.Say(View.SavedKey) : Game.Access.AccessDesk.Words(refused));
            _ui.Refresh();
        }

        // straight down for drawing, and back to the table's own angle
        public bool TopDown { get; private set; }

        public void ToggleTopDown()
        {
            TopDown = !TopDown;

            if (Table.Camera != null) Table.Camera.Pitch = TopDown ? BuilderLayout.Current.TopDownPitch : _tablePitch;
        }

        // a problem with a place: the camera goes to it and the square pulses
        public void Show(MapProblem problem)
        {
            if (problem?.At is not Cell at || Table.Camera == null) return;

            Table.Board.Pulse(at);
            Table.Camera.Following = Table.Board.ToGlobal(Table.Board.Where(at));
        }

        // back to the book; the panels ask first when there are changes not saved
        public void Leave(bool anyway = false)
        {
            if (Editor.Unsaved && !anyway)
            {
                _ui.AskToLeave();
                return;
            }

            GameState.StopBuilding();
            Scenes.Go(GetTree(), Scenes.Book);
        }
    }
}
