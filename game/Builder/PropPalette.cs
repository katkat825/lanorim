using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // A PICKER OF PROPS OR BUILDINGS (cc_task_l Part 4.1; cc_task_m2 Part 8): a search box, the shelf's categories as tabs,
    // the props as thumbnails with their names under them, a page at a time with page buttons. The buildings are one
    // shelf and every other prop the other (PropCatalogue.CategoriesOn). A thumbnail is rendered once, ahead of time,
    // into res://ui/prop_icons/<id>.png (`--prop-icons`, Dev/PropIcons.cs), never live; a prop with no picture yet is its
    // name alone. Plain: the look is Kathleen's (#5)
    public partial class PropPalette : VBoxContainer
    {
        public const string IconFolder = "res://ui/prop_icons/";

        public MapEditor Editor { get; set; }

        // the buildings' shelf, or the props'
        public bool Buildings { get; set; }

        // after a prop is chosen: the picker closes and the board shows it in hand
        public System.Action Picked { get; set; }

        LineEdit _search;
        TabBar _tabs;
        GridContainer _grid;
        Label _page;
        Button _back, _on;

        readonly List<(string Id, Button Button)> _shown = new();

        // the categories in the tabs' order, the whole shelf ("all") first
        List<string> _categories;
        string _category;
        int _at;

        int PerPage => System.Math.Max(1, BuilderLayout.Current.ThumbColumns * BuilderLayout.Current.PickerRows);

        public override void _Ready()
        {
            BuilderLayout layout = BuilderLayout.Current;

            _search = new LineEdit
            {
                Name = "Search",
                PlaceholderText = Ui.Say(Buildings ? MapBuilderView.SearchBuildingsKey : MapBuilderView.SearchKey),
                ClearButtonEnabled = true,
            };
            _search.TextChanged += _ =>
            {
                _at = 0;
                Fill();
            };
            AddChild(_search);

            _categories = new List<string> { null };
            _categories.AddRange(Editor.Palette.CategoriesOn(Buildings));

            _tabs = new TabBar { Name = "Categories", ClipTabs = true, ScrollingEnabled = true };
            _tabs.AddTab(Ui.Say(MapBuilderView.AllPropsKey));
            foreach (string category in _categories.Skip(1)) _tabs.AddTab(Ui.Say(PropCatalogue.CategoryKey(category)));
            _tabs.TabChanged += tab =>
            {
                _category = _categories[(int)tab];
                _at = 0;
                Fill();
            };
            AddChild(_tabs);

            _grid = new GridContainer { Name = "Props", Columns = System.Math.Max(1, layout.ThumbColumns) };
            AddChild(_grid);

            _back = Ui.Button(MapBuilderView.PageBackKey, () => Turn(-1));
            _back.Name = "PageBack";
            _on = Ui.Button(MapBuilderView.PageOnKey, () => Turn(1));
            _on.Name = "PageOn";
            _page = MapBuilderUi.Light(Ui.Plain(""));
            _page.Name = "Page";

            var pages = Ui.Row(layout.Gap, _back, _page, _on);
            pages.Alignment = AlignmentMode.Center;
            AddChild(pages);

            Fill();
            Ui.FocusLater(_search);
        }

        void Turn(int pages)
        {
            _at += pages;
            Fill();
        }

        // the shelf's props in the category that match the search, a page of them as thumbnails
        void Fill()
        {
            Ui.Clear(_grid);
            _shown.Clear();

            int side = BuilderLayout.Current.ThumbSize;

            List<PropEntry> matching = Editor.Palette.Matching(_search.Text, p => Ui.Say(p.NameKey), _category, Buildings).ToList();
            int pages = System.Math.Max(1, (matching.Count + PerPage - 1) / PerPage);
            _at = System.Math.Clamp(_at, 0, pages - 1);

            foreach (PropEntry prop in matching.Skip(_at * PerPage).Take(PerPage))
            {
                string id = prop.Id;
                string path = IconFolder + id + ".png";

                var button = new Button
                {
                    Name = id,
                    Text = Ui.Say(prop.NameKey),
                    ToggleMode = true,
                    Icon = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null,
                    ExpandIcon = true,
                    IconAlignment = HorizontalAlignment.Center,
                    VerticalIconAlignment = VerticalAlignment.Top,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(side, side + side / 3),
                };
                button.AddThemeFontSizeOverride("font_size", BuilderLayout.Current.ThumbFontSize);
                button.Pressed += () =>
                {
                    Editor.Choose(id);
                    Picked?.Invoke();
                };

                _grid.AddChild(button);
                _shown.Add((id, button));
            }

            _page.Text = Ui.Say(MapBuilderView.PageKey, _at + 1, pages);
            _back.Disabled = _at == 0;
            _on.Disabled = _at >= pages - 1;

            Update();
        }

        // the chosen one shows pressed
        public void Update()
        {
            foreach ((string id, Button button) in _shown.Where(s => IsInstanceValid(s.Button)))
                button.SetPressedNoSignal(Editor.PropId == id);
        }
    }
}
