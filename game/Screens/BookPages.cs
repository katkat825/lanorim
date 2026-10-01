using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Screens
{
    // WORDS WRITTEN ON THE OPEN BOOK'S TWO PAGES (cc_task_ui-issues-10-01.md 3.4): the contents, or a campaign's
    // two pages. Each page is a column stood over that page's place on the screen (BookTable.PagesOnScreen),
    // moved every frame so it stays on the page at every window size. No panel and no frame: the page is the
    // ground. The letters are as large as the page has room for, up to ContentsFontMax.
    //
    // TOO SMALL TO READ: once the book has finished opening, if the letters would be smaller than
    // ContentsFontMin the pages can't carry the menu, and TooSmall is raised once (Launch puts the menu back
    // on panels over the book and says why in the log).
    public partial class BookPages : Control
    {
        readonly BookTable _book;
        readonly int _forceBelow;

        public BookPages(BookTable book, bool tooSmall = false)
        {
            _book = book;
            _forceBelow = tooSmall ? int.MaxValue : 0;
            Left = Page("Left");
            Right = Page("Right");
        }

        public VBoxContainer Left { get; }

        public VBoxContainer Right { get; }

        // the letters' size now, in canvas pixels
        public int FontSize { get; private set; }

        public event Action<int> TooSmall;

        bool _judged;

        static VBoxContainer Page(string name)
        {
            var page = new VBoxContainer { Name = name, Visible = false };
            page.AddThemeConstantOverride("separation", 2);
            return page;
        }

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            AddChild(Left);
            AddChild(Right);
        }

        public override void _Process(double delta)
        {
            if (_book?.PagesOnScreen() is not { } pages)
            {
                Left.Visible = Right.Visible = false;
                return;
            }

            (Rect2 left, Rect2 right) = pages;

            int size = Fit(left, right);

            if (size != FontSize)
            {
                FontSize = size;
                foreach (Control words in Words()) words.AddThemeFontSizeOverride("font_size", size);
            }

            Stand(Left, left);
            Stand(Right, right);

            if (_judged || !_book.Still) return;

            _judged = true;

            if (size < Math.Max(_book.ContentsFontMin, _forceBelow)) TooSmall?.Invoke(size);
        }

        static void Stand(Control page, Rect2 on)
        {
            page.Position = on.Position;
            page.Size = on.Size;
            page.Visible = true;
        }

        IEnumerable<Control> Words() =>
            Nodes.Under<Control>(Left).Concat(Nodes.Under<Control>(Right)).Where(c => c is Label or Button);

        // the largest letters both pages hold: every line down the page, a long one wrapped where it may wrap,
        // and nothing that may not wrap wider than the page
        int Fit(Rect2 left, Rect2 right)
        {
            for (int size = _book.ContentsFontMax; size > 1; size--)
                if (Holds(Left, left.Size, size) && Holds(Right, right.Size, size)) return size;

            return 1;
        }

        bool Holds(VBoxContainer page, Vector2 room, int size)
        {
            float lines = 0f;

            foreach (Control line in page.GetChildren().OfType<Control>())
            {
                float n = Lines(line, size, room.X);
                if (float.IsInfinity(n)) return false;
                lines += n;
            }

            return lines * size * _book.ContentsLine <= room.Y;
        }

        // how many lines a control takes at this size in this width (infinity: it doesn't fit)
        static float Lines(Control control, int size, float width)
        {
            if (!control.Visible) return 0f;

            switch (control)
            {
                case Label { ClipText: true }:
                    return 1f;

                case Label label:
                    return Wrapped(label, label.Text, label.AutowrapMode, size, width, 0f);

                case Button button:
                    return Wrapped(button, button.Text, button.AutowrapMode, size, width, size);

                case HBoxContainer row:
                    {
                        List<Control> parts = row.GetChildren().OfType<Control>().Where(c => c.Visible).ToList();
                        float fixedWide = parts.Where(c => !Expands(c)).Sum(c => Wide(c, size));
                        float rest = width - fixedWide - 8f * (parts.Count - 1);

                        return parts.Select(c => Expands(c) ? Lines(c, size, rest) : Wide(c, size) <= width ? 1f : float.PositiveInfinity)
                                    .DefaultIfEmpty(1f).Max();
                    }

                default:
                    return 1f;
            }
        }

        static bool Expands(Control c) => (c.SizeFlagsHorizontal & SizeFlags.Expand) != 0;

        // a button's words and its frame's padding (about a letter's height either side)
        static float Wide(Control c, int size) => c switch
        {
            Button b => Measure(b, b.Text, size) + size,
            Label { ClipText: true } => 0f,
            Label l => Measure(l, l.Text, size),
            _ => 0f,
        };

        static float Wrapped(Control c, string text, TextServer.AutowrapMode wrap, int size, float width, float padding)
        {
            float wide = Measure(c, text, size) + padding;

            if (wide <= width) return 1f;
            if (wrap == TextServer.AutowrapMode.Off) return float.PositiveInfinity;

            // a word longer than the line can't wrap
            float longest = (text ?? "").Split(' ').Select(w => Measure(c, w, size)).DefaultIfEmpty(0f).Max() + padding;

            return longest > width ? float.PositiveInfinity : Mathf.Ceil(wide / Math.Max(1f, width - padding));
        }

        static float Measure(Control c, string text, int size) =>
            c.GetThemeFont("font")?.GetStringSize(text ?? "", HorizontalAlignment.Left, -1, size).X ?? 0f;
    }
}
