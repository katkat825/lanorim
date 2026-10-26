using System;
using System.Collections.Generic;
using System.Linq;
using Content.Screens;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // A PICKER'S CARDS, A PAGE AT A TIME (cc_task_m3 Part 4): one row of wide cards, each a thumbnail with its name under
    // it, and Back, "Page 1 of 3" and Next under them. The Buildings and Props pickers (PropPalette) and the Walls and doors
    // picker (WallPalette) are each this under their own choices. The sizes are the builder's dials (BuilderLayout)
    public partial class CardPages : VBoxContainer
    {
        // one card: what it's called, the picture over its name (null: the name alone), what picking it does, and whether
        // it's the one chosen
        public sealed record Card(string Id, string Words, string Picture, Action Pick, Func<bool> Chosen);

        GridContainer _grid;
        Label _page;
        Button _back, _on;

        IReadOnlyList<Card> _cards = Array.Empty<Card>();
        readonly List<(Card Card, Button Button)> _shown = new();
        int _at;

        static BuilderLayout Layout => BuilderLayout.Current;

        static int PerPage => Math.Max(1, Layout.ThumbColumns * Layout.PickerRows);

        public override void _Ready()
        {
            _grid = new GridContainer { Name = "Cards", Columns = Math.Max(1, Layout.ThumbColumns) };
            AddChild(_grid);

            _back = Ui.Button(MapBuilderView.PageBackKey, () => Turn(-1));
            _back.Name = "PageBack";
            _on = Ui.Button(MapBuilderView.PageOnKey, () => Turn(1));
            _on.Name = "PageOn";
            _page = MapBuilderUi.Light(Ui.Plain(""));
            _page.Name = "Page";

            var pages = Ui.Row(Layout.Gap, _back, _page, _on);
            pages.Alignment = AlignmentMode.Center;
            AddChild(pages);

            Fill();
        }

        // these cards, from the first page (a new search, a new tab), or kept at the page it's on
        public void Show(IReadOnlyList<Card> cards, bool fromTheStart = true)
        {
            _cards = cards ?? Array.Empty<Card>();
            if (fromTheStart) _at = 0;
            if (_grid != null) Fill();
        }

        // the page a card is on, turned to (a picker opening on what's already chosen)
        public void ShowChosen()
        {
            int chosen = _cards.ToList().FindIndex(c => c.Chosen());
            if (chosen < 0) return;

            _at = chosen / PerPage;
            if (_grid != null) Fill();
        }

        void Turn(int pages)
        {
            _at += pages;
            Fill();
        }

        void Fill()
        {
            Ui.Clear(_grid);
            _shown.Clear();

            int side = Layout.ThumbSize;
            int pages = Math.Max(1, (_cards.Count + PerPage - 1) / PerPage);
            _at = Math.Clamp(_at, 0, pages - 1);

            foreach (Card card in _cards.Skip(_at * PerPage).Take(PerPage))
            {
                Card one = card;

                var button = new Button
                {
                    Name = card.Id,
                    Text = card.Words,
                    ToggleMode = true,
                    Icon = card.Picture != null && ResourceLoader.Exists(card.Picture) ? GD.Load<Texture2D>(card.Picture) : null,
                    ExpandIcon = true,
                    IconAlignment = HorizontalAlignment.Center,
                    VerticalIconAlignment = VerticalAlignment.Top,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(Layout.ThumbWidth, side + side / 2),
                };
                button.AddThemeFontSizeOverride("font_size", Layout.ThumbFontSize);
                button.Pressed += () => one.Pick();

                _grid.AddChild(button);
                _shown.Add((card, button));
            }

            _page.Text = Ui.Say(MapBuilderView.PageKey, _at + 1, pages);
            _back.Disabled = _at == 0;
            _on.Disabled = _at >= pages - 1;

            Update();
        }

        // the chosen one shows pressed, and only it
        public void Update()
        {
            foreach ((Card card, Button button) in _shown.Where(s => IsInstanceValid(s.Button)))
                button.SetPressedNoSignal(card.Chosen());
        }

        // what's on the page now (a probe)
        public IEnumerable<Button> Showing => _shown.Select(s => s.Button).Where(IsInstanceValid);
    }
}
