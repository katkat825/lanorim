using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // THE PROP PALETTE (cc_task_l Part 4.1): a search box, the categories as tabs, and the props as thumbnails with their
    // names under them. A thumbnail is rendered once, ahead of time, into res://ui/prop_icons/<id>.png (`--prop-icons`,
    // Dev/PropIcons.cs), never live; a prop with no picture yet is its name alone. Plain: the look is Kathleen's (#5)
    public partial class PropPalette : VBoxContainer
    {
        public const string IconFolder = "res://ui/prop_icons/";

        public MapEditor Editor { get; set; }

        // after a prop is chosen, so the rest of the panel can show it
        public System.Action Picked { get; set; }

        LineEdit _search;
        TabBar _tabs;
        GridContainer _grid;

        readonly List<(string Id, Button Button)> _shown = new();

        // the categories in the tabs' order, "all" first
        List<string> _categories;

        public override void _Ready()
        {
            BuilderLayout layout = BuilderLayout.Current;

            _search = new LineEdit
            {
                Name = "Search",
                PlaceholderText = Ui.Say(MapBuilderView.SearchKey),
                ClearButtonEnabled = true,
            };
            _search.TextChanged += _ => Fill();
            AddChild(_search);

            _categories = new List<string> { null };
            _categories.AddRange(Editor.Palette.Categories);

            _tabs = new TabBar { Name = "Categories", ClipTabs = true, ScrollingEnabled = true };
            _tabs.AddTab(Ui.Say(MapBuilderView.AllPropsKey));
            foreach (string category in Editor.Palette.Categories) _tabs.AddTab(Ui.Say(PropCatalogue.CategoryKey(category)));
            _tabs.CurrentTab = System.Math.Max(0, _categories.IndexOf(Editor.Category));
            _tabs.TabChanged += tab =>
            {
                Editor.Browse(_categories[(int)tab]);
                Fill();
            };
            AddChild(_tabs);

            _grid = new GridContainer { Name = "Props", Columns = System.Math.Max(1, layout.ThumbColumns) };
            AddChild(Ui.Scroll(_grid, layout.PaletteHeight));

            Fill();
        }

        // the category's props that match the search, as thumbnails
        void Fill()
        {
            Ui.Clear(_grid);
            _shown.Clear();

            int side = BuilderLayout.Current.ThumbSize;

            foreach (PropEntry prop in Editor.Palette.Matching(_search.Text, p => Ui.Say(p.NameKey), Editor.Category))
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
                    TooltipText = Ui.Say(prop.NameKey),
                };
                button.Pressed += () =>
                {
                    Editor.Choose(id);
                    Picked?.Invoke();
                };

                _grid.AddChild(button);
                _shown.Add((id, button));
            }

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
