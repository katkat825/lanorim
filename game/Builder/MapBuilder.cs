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

            // the GM screen isn't on the table while a map is built (cc_task_m2 Part 8): it's the play's, and it stood
            // where an author is looking
            if (Table.GmScreen != null) Table.GmScreen.Visible = false;

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

            // a prop in hand places one and is selected; with Ctrl held, more of the same (cc_task_m2 Part 8)
            bool changed = Editor.Apply(from, Aim(click.Position), more: click.CtrlPressed || click.MetaPressed) > 0;

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

        // THE BUILDER'S ACTS (game/Access, rebindable on the controls page): every menu item and bar button, the prop's
        // turn and the selection's copy, paste and clear (Do). Escape is its own (Escape), a bare Alt opens the menu bar
        // as F10 does, and Ctrl+Shift+Z still redoes, the other way every editor spells it
        bool Keyed(InputEventKey key)
        {
            if (Keyboard.Pressed(key) is Act act && Do(act)) return true;

            if ((key.CtrlPressed || key.MetaPressed) && key.ShiftPressed && key.Keycode == Key.Z)
            {
                Redo();
                return true;
            }

            if (key.Keycode == Key.Escape) return Escape();

            if (key.Keycode == Key.Alt && !key.CtrlPressed && !key.ShiftPressed)
            {
                _ui.OpenMenus();
                return true;
            }

            return false;
        }

        // EVERY MENU ITEM, BAR BUTTON AND KEY, ONE PLACE (cc_task_m2 Part 8): true when it was the builder's
        public bool Do(Act act)
        {
            switch (act)
            {
                case Act.UndoEdit: Undo(); return true;
                case Act.RedoEdit: Redo(); return true;
                case Act.SaveMap: Save(); return true;
                case Act.SaveMapAs: _ui.AskSaveAs(); return true;
                case Act.CopyMapTo: _ui.AskCopyTo(); return true;
                case Act.PlayMap: ShowTrial(); return true;
                case Act.BuilderToMainMenu: Leave(toMainMenu: true); return true;
                case Act.LeaveBuilder: Leave(); return true;
                case Act.TurnProp: Rotate(1); return true;
                case Act.TurnPropBack: Rotate(-1); return true;
                case Act.CopySelection: Copy(); return true;
                case Act.PasteSelection: Paste(); return true;
                case Act.DeleteSelection: Delete(); return true;
                case Act.SelectTool: Take(MapTool.Select); return true;
                case Act.TerrainTool: Take(MapTool.Paint); return true;
                case Act.FillTool: Take(MapTool.Fill); return true;
                case Act.StartTool: Take(MapTool.Start); return true;
                case Act.SpawnTool: Take(MapTool.Spawn); return true;
                case Act.EraseTool: Take(MapTool.Erase); return true;
                case Act.BuildingsPicker: _ui.Toggle(MapBuilderUi.BuildingsPicker); return true;
                case Act.PropsPicker: _ui.Toggle(MapBuilderUi.PropsPicker); return true;
                case Act.WallsAndDoors: _ui.Toggle(MapBuilderUi.WallsPanel); return true;
                case Act.BuilderProblems: _ui.Toggle(MapBuilderUi.ProblemsPanel); return true;
                case Act.ViewFromAbove: Look(fromAbove: true); return true;
                case Act.PlayersView: Look(fromAbove: false); return true;
                case Act.BuilderMenus: _ui.OpenMenus(); return true;
                default: return false;
            }
        }

        // a tool taken up: whatever was in hand put down, the selection let go, any picker closed
        public void Take(MapTool tool)
        {
            Editor.Escape(open: false);
            Editor.Tool = tool;
            if (tool != MapTool.Select) Editor.Deselect();
            _ui.Close();
            Chose();
        }

        // ESCAPE, one step a press (MapEditor.Escape): what's in hand put down, then the open picker, pop-up or question
        // closed, then the selection let go. False when there was nothing to do
        public bool Escape()
        {
            switch (Editor.Escape(_ui.AnythingOpen))
            {
                case MapEscape.Dropped:
                case MapEscape.Cleared:
                    Chose();
                    return true;

                case MapEscape.Close:
                    _ui.CloseWhatsOpen();
                    return true;

                default:
                    return false;
            }
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

        // straight down for drawing (View from above), and back to the table's own angle (Player's view)
        public bool TopDown { get; private set; }

        public void Look(bool fromAbove)
        {
            TopDown = fromAbove;

            if (Table.Camera != null) Table.Camera.Pitch = TopDown ? BuilderLayout.Current.TopDownPitch : _tablePitch;

            _ui.Refresh();
        }

        public void ToggleTopDown() => Look(!TopDown);

        // a problem with a place: the camera goes to it and the square pulses
        public void Show(MapProblem problem)
        {
            if (problem?.At is not Cell at || Table.Camera == null) return;

            Table.Board.Pulse(at);
            Table.Camera.Following = Table.Board.ToGlobal(Table.Board.Where(at));
        }

        // LEAVE, back to the Workshop that opened it, or (File, Main menu) to the book's main menu; asked first when there
        // are changes not saved
        public void Leave(bool anyway = false, bool toMainMenu = false)
        {
            if (Editor.Unsaved && !anyway)
            {
                _ui.AskToLeave(toMainMenu);
                return;
            }

            if (toMainMenu)
            {
                GameState.BackTo = Launch.MainMenu;
                GameState.Editing = null;
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
