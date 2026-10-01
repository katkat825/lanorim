using System;
using Godot;

namespace Game.Screens
{
    // BEGIN, ON THE CLOSED BOOK'S COVER (cc_task_ui-issues-10-01.md 3.3). Kathleen: "ditch the ui text block
    // and just put a Begin button on the cover of the book". The title is on the cover in 3D (BookTable); this
    // is the one button, stood over the cover where BookTable.BeginAt says, moved every frame with the book's
    // place on the screen so it stays on the cover at every window size. It is an ordinary themed button:
    // clickable, focused so Enter or Space presses it (the Controls page says so), and it carries its words
    // as its accessible name for a screen reader. Pressing it opens the book.
    public partial class BookCover : Control
    {
        readonly BookTable _book;
        readonly Button _begin;

        public BookCover(BookTable book, Action begin)
        {
            _book = book;
            _begin = Ui.Button(ScreenWords.Begin, begin);
            _begin.Name = "Begin";
            _begin.AccessibilityName = _begin.Text;
            _begin.Visible = false;
        }

        public Button Begin => _begin;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_begin);
            Ui.FocusLater(_begin);
        }

        public override void _Process(double delta)
        {
            if (_book?.CoverOnScreen() is not Rect2 cover)
            {
                _begin.Visible = false;
                return;
            }

            Vector2 size = _begin.GetCombinedMinimumSize();
            _begin.Size = size;
            _begin.Position = new Vector2(cover.GetCenter().X - size.X * 0.5f,
                                          cover.Position.Y + cover.Size.Y * _book.BeginAt - size.Y * 0.5f);
            _begin.Visible = true;
        }
    }
}
