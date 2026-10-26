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

        // the buildings' shelf, the props', or the people's (cc_task_m3 Part 10)
        public PropShelf Shelf { get; set; }

        // after a prop is chosen: the picker closes and the board shows it in hand
        public System.Action Picked { get; set; }

        LineEdit _search;
        TabBar _tabs;
        CardPages _cards;

        // the categories in the tabs' order, the whole shelf ("all") first
        List<string> _categories;
        string _category;

        public override void _Ready()
        {
            BuilderLayout layout = BuilderLayout.Current;

            _search = new LineEdit
            {
                Name = "Search",
                PlaceholderText = Ui.Say(Shelf switch
                {
                    PropShelf.Buildings => MapBuilderView.SearchBuildingsKey,
                    PropShelf.People => MapBuilderView.SearchNpcsKey,
                    _ => MapBuilderView.SearchKey,
                }),
                ClearButtonEnabled = true,
            };
            _search.TextChanged += _ => Fill();
            AddChild(_search);

            _categories = new List<string> { null };
            _categories.AddRange(Editor.Palette.CategoriesOn(Shelf));

            _tabs = new TabBar { Name = "Categories", ClipTabs = true, ScrollingEnabled = true };
            _tabs.AddTab(Ui.Say(MapBuilderView.AllPropsKey));
            foreach (string category in _categories.Skip(1)) _tabs.AddTab(Ui.Say(PropCatalogue.CategoryKey(category)));
            _tabs.TabChanged += tab =>
            {
                _category = _categories[(int)tab];
                Fill();
            };
            AddChild(_tabs);

            // in the tree, so the theme's own arrows are the ones made bigger
            BigArrows(_tabs, layout.TabArrowScale);

            _cards = new CardPages { Name = "Props" };
            AddChild(_cards);

            Fill();
            Ui.FocusLater(_search);
        }

        // THE TABS' SCROLL ARROWS, BIGGER (cc_task_m3 Part 4: "at least 3 times as large, easy to hit"): the theme's own
        // arrows, scaled, set on this bar alone. A TabBar sizes its arrow buttons by their icons
        public static void BigArrows(TabBar tabs, float scale)
        {
            if (scale <= 1f) return;

            foreach (string arrow in new[] { "increment", "increment_highlight", "decrement", "decrement_highlight" })
            {
                Image image = tabs.GetThemeIcon(arrow)?.GetImage();
                if (image == null) continue;

                image = (Image)image.Duplicate();
                if (image.IsCompressed()) image.Decompress();
                image.Resize(Mathf.RoundToInt(image.GetWidth() * scale), Mathf.RoundToInt(image.GetHeight() * scale), Image.Interpolation.Lanczos);
                tabs.AddThemeIconOverride(arrow, ImageTexture.CreateFromImage(image));
            }
        }

        // the shelf's props in the category that match the search, from the first page
        void Fill() =>
            _cards.Show(Editor.Palette.Matching(_search.Text, p => Ui.Say(p.NameKey), _category, Shelf)
                              .Select(p => new CardPages.Card(p.Id, Ui.Say(p.NameKey), IconFolder + p.Id + ".png",
                                                              () => { Editor.Choose(p.Id); Picked?.Invoke(); },
                                                              () => Editor.PropId == p.Id))
                              .ToList());

        // the chosen one shows pressed
        public void Update() => _cards?.Update();
    }
}
