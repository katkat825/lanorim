using System;
using System.Collections.Generic;
using System.Linq;
using Content.Workshop.Story;
using Game.Access;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE STORY BOARD (cc_task_o Part 1): the campaign's cards on a board you pan (drag the background, or the middle
    // mouse) and zoom (the wheel, or + and -), arrows from each card to every card it can lead to (lettered for a note's
    // options), and a small overview of the whole board in the corner. Double-click on empty board makes a card there;
    // drag from a card's arrow handle onto empty board makes a card and the go-to to it in one step, and onto another
    // card just the go-to. Tab and the arrow keys walk the cards, Enter opens one, Ctrl+D duplicates it, Delete asks.
    // Positions are the board's own units; the page keeps them in workshop.json, never in the Yarn
    public partial class StoryBoard : Control
    {
        public static readonly Vector2 Tile = new(230, 116);

        readonly Dictionary<string, Control> _tiles = new(StringComparer.Ordinal);

        Vector2 _pan;
        float _zoom = 1;
        bool _panning;
        string _dragging;
        Vector2 _grabbed;
        string _arrowFrom;
        Vector2 _arrowTo;

        public StoryDraft Story { get; set; }

        // what a card's dot says, and whether it starts a chapter
        public Func<string, Dot> DotOf { get; set; } = _ => Dot.None;

        public Func<string, bool> IsStart { get; set; } = _ => false;

        public event Action<string> Opened;

        public event Action<Vector2> NewCardAt;

        public event Action<string, Vector2> ArrowToEmpty;

        public event Action<string, string> ArrowToCard;

        public event Action<string, Vector2> Moved;

        public event Action<string> Duplicate;

        public event Action<string> Delete;

        public event Action Viewed;

        public float Zoom => _zoom;

        public override void _Ready()
        {
            ClipContents = true;
            FocusMode = FocusModeEnum.All;
            MouseFilter = MouseFilterEnum.Stop;
            CustomMinimumSize = new Vector2(0, Ui.Px(440));
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
        }

        public void Look(BoardView view)
        {
            _pan = new Vector2(view.X, view.Y);
            _zoom = Mathf.Clamp(view.Zoom <= 0 ? 1 : view.Zoom, 0.25f, 2.5f);
        }

        public BoardView View => new() { X = _pan.X, Y = _pan.Y, Zoom = _zoom };

        // --- the cards ----------------------------------------------------------------------------------------------

        public void Build()
        {
            foreach (Control old in _tiles.Values) old.QueueFree();
            _tiles.Clear();

            foreach (CardDraft card in Story.Cards) _tiles[card.Title] = TileFor(card);

            Place();
        }

        Control TileFor(CardDraft card)
        {
            string title = card.Title;
            CardLook look = Story.Looks.TryGetValue(title, out CardLook l) ? l : new CardLook();

            // the tile: a fixed-size holder, the card's panel filling it, and the arrow handle on its right edge
            var holder = new Control
            {
                Name = "CardTile_" + title,
                FocusMode = FocusModeEnum.All,
                MouseFilter = MouseFilterEnum.Stop,
                Size = Tile,
                CustomMinimumSize = Tile,
            };

            var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
            panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            holder.AddChild(panel);

            Label name = Ui.Plain(Story.NameOf(title));
            name.ThemeTypeVariation = "TitleLabel";
            name.AutowrapMode = TextServer.AutowrapMode.Off;
            name.ClipText = true;
            name.CustomMinimumSize = new Vector2(Tile.X - 40, 0);

            var badges = Ui.Row(4);
            if (IsStart(title)) badges.AddChild(Badge(StoryWords.ChapterStartKey));
            if (!card.IsText && Content.Workshop.Story.Blocks.All(card.Blocks).Any(b => b is Stop)) badges.AddChild(Badge(StoryWords.EndsStoryKey));
            if (card.IsText) badges.AddChild(Badge(StoryWords.TextCardKey));

            Dot dot = DotOf(title);
            if (dot != Dot.None)
            {
                var mark = new ColorRect { Name = "Dot", Color = dot == Dot.Red ? new Color(0.8f, 0.15f, 0.1f) : new Color(0.9f, 0.65f, 0.1f), CustomMinimumSize = new Vector2(12, 12) };
                mark.SetMeta("dot", dot.ToString());
                badges.AddChild(mark);
            }

            string first = card.IsText ? "" : string.Join("\n", Content.Workshop.Story.Blocks.Lines(card.Blocks).Take(2).Select(x => x.Text));
            Label lines = Forms.Note(first);
            lines.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            lines.MaxLinesVisible = 2;
            lines.CustomMinimumSize = new Vector2(Tile.X - 24, 0);
            lines.AddThemeFontSizeOverride("font_size", 14);

            int count = card.IsText ? 0 : card.Blocks.Count(b => b is not Blank);
            Label blocks = Forms.Hint(count == 1 ? StoryWords.BlocksOneKey : StoryWords.BlocksManyKey, count);
            blocks.Visible = !card.IsText;

            var column = Ui.Column(2, name, badges, lines, blocks);
            foreach (Control inside in Nodes.Under<Control>(column).Prepend(column)) inside.MouseFilter = MouseFilterEnum.Ignore;
            panel.AddChild(column);

            // the arrow handle: drag from it to lead somewhere
            var handle = new Button { Name = "Handle", Text = ">", FocusMode = FocusModeEnum.None, Size = new Vector2(24, 28) };
            handle.Position = new Vector2(Tile.X - 12, Tile.Y / 2 - 14);
            handle.ButtonDown += () => { _arrowFrom = title; _arrowTo = GetLocalMousePosition(); };
            holder.AddChild(handle);

            holder.GuiInput += e => TileInput(title, holder, e);
            AddChild(holder);
            return holder;
        }

        static Label Badge(string key)
        {
            Label badge = Ui.Label(key);
            badge.AutowrapMode = TextServer.AutowrapMode.Off;
            badge.AddThemeFontSizeOverride("font_size", 13);
            badge.Modulate = new Color(1, 1, 1, 0.8f);
            return badge;
        }

        // every tile where its card sits, at the board's zoom
        void Place()
        {
            foreach ((string title, Control tile) in _tiles)
            {
                tile.Scale = new Vector2(_zoom, _zoom);
                tile.Position = ToScreen(Where(title));
            }

            QueueRedraw();
        }

        public Vector2 Where(string title) =>
            Story.Looks.TryGetValue(title, out CardLook look) && look.Placed ? new Vector2(look.X, look.Y) : Laid(title);

        // a card nobody has placed: laid out in columns by how far it is from a chapter's start
        Vector2 Laid(string title)
        {
            var depth = new Dictionary<string, int>(StringComparer.Ordinal);
            var queue = new Queue<string>(Story.Cards.Select(c => c.Title).Where(IsStart));

            foreach (string start in queue) depth[start] = 0;

            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                if (Story.Card(at) is not { } card) continue;

                foreach ((string to, _) in StoryDraft.Leads(card))
                    if (!depth.ContainsKey(to))
                    {
                        depth[to] = depth[at] + 1;
                        queue.Enqueue(to);
                    }
            }

            int column = depth.TryGetValue(title, out int d) ? d : depth.Values.DefaultIfEmpty(-1).Max() + 1;
            int row = Story.Cards.Select(c => c.Title).Where(t => (depth.TryGetValue(t, out int e) ? e : depth.Values.DefaultIfEmpty(-1).Max() + 1) == column)
                           .TakeWhile(t => t != title).Count();

            return new Vector2(40 + column * (Tile.X + 70), 40 + row * (Tile.Y + 40));
        }

        Vector2 ToScreen(Vector2 board) => board * _zoom + _pan;

        Vector2 ToBoard(Vector2 screen) => (screen - _pan) / _zoom;

        // --- the view -----------------------------------------------------------------------------------------------

        public void ZoomBy(float factor, Vector2? about = null)
        {
            Vector2 centre = about ?? Size / 2;
            Vector2 before = ToBoard(centre);
            _zoom = Mathf.Clamp(_zoom * factor, 0.25f, 2.5f);
            _pan = centre - before * _zoom;
            Place();
            Viewed?.Invoke();
        }

        // FIT ALL: every card in view
        public void FitAll()
        {
            if (_tiles.Count == 0) return;

            Rect2 all = Bounds();
            float zoom = Mathf.Min((Size.X - 40) / Math.Max(1, all.Size.X), (Size.Y - 40) / Math.Max(1, all.Size.Y));
            _zoom = Mathf.Clamp(zoom, 0.25f, 1.5f);
            _pan = Size / 2 - (all.Position + all.Size / 2) * _zoom;
            Place();
            Viewed?.Invoke();
        }

        // a card brought to the middle (Go to card, a search result)
        public void Show(string title)
        {
            if (!_tiles.TryGetValue(title, out Control tile)) return;

            _pan = Size / 2 - (Where(title) + Tile / 2) * _zoom;
            Place();
            tile.GrabFocus();
            Viewed?.Invoke();
        }

        Rect2 Bounds()
        {
            var places = _tiles.Keys.Select(Where).ToList();
            Vector2 min = new(places.Min(p => p.X), places.Min(p => p.Y));
            Vector2 max = new(places.Max(p => p.X), places.Max(p => p.Y));
            return new Rect2(min, max - min + Tile);
        }

        // --- input ---------------------------------------------------------------------------------------------------

        public override void _GuiInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } up:
                    ZoomBy(1.1f, up.Position);
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } down:
                    ZoomBy(1 / 1.1f, down.Position);
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left, DoubleClick: true } twice:
                    NewCardAt?.Invoke(ToBoard(twice.Position) - Tile / 2);
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Middle } press:
                    if (!press.Pressed && _arrowFrom != null) EndArrow(press.Position);
                    _panning = press.Pressed && _arrowFrom == null;
                    if (!press.Pressed) Viewed?.Invoke();
                    GrabFocus();
                    break;

                case InputEventMouseMotion motion when _arrowFrom != null:
                    _arrowTo = motion.Position;
                    QueueRedraw();
                    break;

                case InputEventMouseMotion motion when _panning:
                    _pan += motion.Relative;
                    Place();
                    break;

                case InputEventKey { Pressed: true } key when Keyboard.Pressed(key) == Act.ZoomIn:
                    ZoomBy(1.1f);
                    break;

                case InputEventKey { Pressed: true } key when Keyboard.Pressed(key) == Act.ZoomOut:
                    ZoomBy(1 / 1.1f);
                    break;

                default:
                    return;
            }

            AcceptEvent();
        }

        public override void _Input(InputEvent @event)
        {
            // an arrow dragged from a handle is let go wherever the mouse is
            if (_arrowFrom != null && @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } up)
            {
                EndArrow(GetLocalMousePosition());
                GetViewport().SetInputAsHandled();
            }
            else if (_arrowFrom != null && @event is InputEventMouseMotion)
            {
                _arrowTo = GetLocalMousePosition();
                QueueRedraw();
            }
        }

        void EndArrow(Vector2 at)
        {
            string from = _arrowFrom;
            _arrowFrom = null;
            QueueRedraw();

            if (from == null) return;

            string onto = _tiles.FirstOrDefault(t => t.Key != from && new Rect2(t.Value.Position, Tile * _zoom).HasPoint(at)).Key;

            if (onto != null) ArrowToCard?.Invoke(from, onto);
            else if (!_tiles.Any(t => new Rect2(t.Value.Position, Tile * _zoom).HasPoint(at))) ArrowToEmpty?.Invoke(from, ToBoard(at) - Tile / 2);
        }

        void TileInput(string title, Control tile, InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left, DoubleClick: true }:
                    Opened?.Invoke(title);
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left } press:
                    if (press.Pressed)
                    {
                        _dragging = title;
                        _grabbed = press.Position * _zoom;
                        tile.GrabFocus();
                    }
                    else if (_dragging == title)
                    {
                        _dragging = null;
                        Moved?.Invoke(title, ToBoard(tile.Position));
                    }
                    break;

                case InputEventMouseMotion when _dragging == title:
                    tile.Position = GetLocalMousePosition() - _grabbed;
                    QueueRedraw();
                    break;

                case InputEventKey { Pressed: true } key:
                    Act? act = Keyboard.Pressed(key);

                    if (act == Act.Touch) Opened?.Invoke(title);
                    else if (act == Act.DuplicateCard) Duplicate?.Invoke(title);
                    else if (act == Act.DeleteSelection) Delete?.Invoke(title);
                    else if (key.IsAction("ui_left") || key.IsAction("ui_right") || key.IsAction("ui_up") || key.IsAction("ui_down"))
                        Step(title, key.IsAction("ui_left") ? Vector2.Left : key.IsAction("ui_right") ? Vector2.Right : key.IsAction("ui_up") ? Vector2.Up : Vector2.Down);
                    else return;

                    break;

                default:
                    return;
            }

            tile.AcceptEvent();
        }

        // the arrow keys: the nearest card that way
        void Step(string from, Vector2 way)
        {
            Vector2 at = Where(from);

            string next = _tiles.Keys.Where(t => t != from)
                                .Select(t => (Title: t, To: Where(t) - at))
                                .Where(t => t.To.Normalized().Dot(way) > 0.5f)
                                .OrderBy(t => t.To.Length())
                                .Select(t => t.Title)
                                .FirstOrDefault();

            if (next != null) Show(next);
        }
    }
}
