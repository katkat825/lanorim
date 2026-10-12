using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Core.Space;
using Game.Access;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // THE MAP BUILDER AT THE TABLE (cc_task_f Part 2; cc_task_l; v1_build_order.md Phase 2). The table's own board draws the
    // map, so what the author sees is what the player gets; the table's camera turns (Q, E) and zooms, with a
    // straight-down view for drawing. This node is a view over MapEditor (through MapBuilderView): it turns the mouse into
    // a point in squares and the keys (the builder's acts, game/Access) into the editor's verbs, and lays the board again
    // after every change. No map rule is here.
    //
    // Opened from the book's "Map builder" (MapBuilderPage), which leaves GameState.Building set for the table
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

        // a Shift-drag with a prop in hand: the squares the pointer has crossed, for the scatter (cc_task_l Part 4.5)
        List<Cell> _scattering;

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

            var laying = System.Diagnostics.Stopwatch.StartNew();
            Lay();
            GD.Print($"builder {View.Id} in {View.Place}: {View.Draft}, laid in {laying.ElapsedMilliseconds} ms");

            // `--map-probe`: the builder driven (the dev registry, Dev/MapBuilderProbe.cs)
            Game.Dev.DevHooks.AtBuilder(this);
        }

        // --- the board ----------------------------------------------------------------------------------------------

        // the map as it stands now, on the table's board, with its look, its props, spawns and start
        void Lay()
        {
            MapLayout map = View.Draft.Layout();

            Table.Board.Lay(map, View.Draft.Look());
            Table.Board.Dress(View.Draft.Props);
            Table.GmScreen?.StandBehind(Table.Board);

            _marks.Markers(View.Draft, Ui.Say(MapEditor.ToolKey(MapTool.Start)));
            _marks.Chosen(Editor.Chosen);
            Hover(_over);
        }

        // after anything that changed the map: the board again, and the panels
        public void Changed()
        {
            Lay();
            _ui.Refresh();
        }

        // after something that changed only what's chosen: the outline and the panels
        public void Chose()
        {
            _marks.Chosen(Editor.Chosen);
            Hover(_over);
            _ui.Refresh();
        }

        // --- the pointer --------------------------------------------------------------------------------------------

        public override void _UnhandledInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseMotion motion:
                    // a drag let go over a panel never reached here: the button is up, so there's no drag
                    if ((motion.ButtonMask & MouseButtonMask.Left) == 0)
                    {
                        _pressed = null;
                        _scattering = null;
                    }

                    Hover(Aim(motion.Position));
                    if (_over.Square is Cell crossed) _scattering?.Add(crossed);
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left } click:
                    if (click.Pressed) Press(click);
                    else Release(click);

                    GetViewport().SetInputAsHandled();
                    break;

                case InputEventKey { Pressed: true, Echo: false } key when Keyed(key):
                    GetViewport().SetInputAsHandled();
                    break;
            }
        }

        void Press(InputEventMouseButton click)
        {
            _pressed = Aim(click.Position);

            // Shift with a prop in hand starts a scatter along the drag
            _scattering = click.ShiftPressed && Editor.Tool == MapTool.Prop && Editor.PropId != null && _pressed?.Square is Cell first
                ? new List<Cell> { first }
                : null;
        }

        void Release(InputEventMouseButton click)
        {
            if (_pressed is not MapTarget from) return;

            _pressed = null;

            if (_scattering != null)
            {
                List<Cell> path = _scattering.Where(View.Draft.Contains).ToList();
                _scattering = null;

                // the seed is the stroke's own: the same drag scatters the same wood
                int stood = Editor.Scatter(path, path.Aggregate(17, (h, c) => h * 31 + c.X * 7 + c.Y));
                GD.Print($"builder scattered {stood} {Editor.PropId} along {path.Count} squares");
                Do(stood > 0);
                return;
            }

            bool changed = Editor.Apply(from, Aim(click.Position)) > 0;

            if (changed) Changed();
            else if (Editor.Tool == MapTool.Select) Chose();
        }

        MapTarget Aim(Vector2 screen) =>
            Table.Board.GridPointUnder(screen) is Vector2 at ? Editor.Aim(at.X, at.Y) : MapTarget.Nothing;

        // the hover highlight: what a click here, or the drag so far, would change
        void Hover(MapTarget over)
        {
            _over = over;

            var (squares, lines) = Editor.Preview(_pressed ?? over, over);

            if (_scattering != null) squares = _scattering.Concat(squares).Where(View.Draft.Contains).Distinct().ToList();

            if (squares.Count > 0) Table.Board.Flash(squares, BuilderLayout.Current.HoverColour);
            else Table.Board.Unflash();

            _marks.Lines(lines);
        }

        // THE BUILDER'S ACTS (game/Access, rebindable on the controls page): Ctrl+Z, Ctrl+Y, Ctrl+S, R and Shift+R to turn,
        // Ctrl+C, Ctrl+V and Delete for the Select tool; Escape lets go of what's chosen. Ctrl+Shift+Z still redoes, the
        // other way every editor spells it
        bool Keyed(InputEventKey key)
        {
            switch (Keyboard.Pressed(key))
            {
                case Act.UndoEdit: Undo(); return true;
                case Act.RedoEdit: Redo(); return true;
                case Act.SaveMap: Save(); return true;
                case Act.TurnProp: Rotate(1); return true;
                case Act.TurnPropBack: Rotate(-1); return true;
                case Act.CopySelection: Copy(); return true;
                case Act.PasteSelection: Paste(); return true;
                case Act.DeleteSelection: Delete(); return true;
            }

            if ((key.CtrlPressed || key.MetaPressed) && key.ShiftPressed && key.Keycode == Key.Z)
            {
                Redo();
                return true;
            }

            if (key.Keycode == Key.Escape && Editor.Chosen != null)
            {
                Editor.Deselect();
                Chose();
                return true;
            }

            return false;
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
            bool turning = Editor.Tool == MapTool.Select && Editor.Picked != null;

            Editor.Rotate(quarters);

            if (turning) Changed();
            else _ui.Refresh();
        }

        public void Copy()
        {
            if (Editor.Copy()) _ui.Notice(Ui.Say(MapBuilderView.CopyKey));
        }

        // where the pointer is, or where the selection is when the pointer is off the map
        public void Paste()
        {
            Cell? at = _over.Square ?? Editor.Selection?.Min;

            if (at is Cell here && Editor.Paste(here)) Changed();
            else Chose();
        }

        public void Delete() => Do(Editor.Delete());

        public void Notice(string words) => _ui.Notice(words);

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

        // PLAY THIS MAP (cc_task_l Part 5): the panel, then the fight set in it, at the table, and back here after it
        public void ShowTrial() => _ui.ShowTrial();

        public void Play(MapTrial trial) => TrialRun.Start(this, trial);

        public MapBuilderUi Panels => _ui;
    }
}
