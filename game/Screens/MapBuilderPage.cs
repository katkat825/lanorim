using System;
using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE BOOK'S "MAP BUILDER" (cc_task_f Part 2; cc_task_l Part 1), WRITTEN ON THE OPEN BOOK the way the contents and a
    // campaign's pages are (Launch.Spread, BookPages): a new map on the left page (a name, a size, a starting template,
    // and where it lives: "My maps", the default, or a campaign), and on the right My maps and a campaign's maps, each
    // a line of the contents that opens it, then Contents to go back. When the pages are too small to read, the same
    // on a panel over the book, as the menu does. The table comes up as the builder (GameState.Building). A map is saved
    // where it lives (MapFiles): a campaign's loads in that campaign with no conversion.
    // `--build` and `--map-probe` are the dev registry's (Dev/DevHooks.cs)
    public sealed class MapBuilderPage : IBookPage
    {
        // the campaign whose maps are listed, kept while the book is open
        string _shown;

        public string Id => "builder";

        public string NameKey => CampaignBook.MapBuilderKey;

        // reached from the Workshop (cc_task_m 1.1), not the contents
        public bool InContents => false;

        // back to the Workshop, which opened this page
        static void Back(Launch launch) => launch.PageCalled(Game.Workshop.WorkshopPage.PageId)?.Open(launch);

        // "MY MAPS", the maps that belong to no campaign: user://maps, the player's own space, in the editor and in an
        // exported build alike. On Windows that is %APPDATA%\Godot\app_userdata\Lanorim\maps (project.godot sets no
        // custom user folder)
        public static MapPlace Library => MapPlace.Library(ProjectSettings.GlobalizePath(LibraryFolder));

        // the probe keeps a library of its own (check-mapbuilder), never the player's
        public static string LibraryFolder { get; set; } = "user://maps";

        // every campaign a map can live in, by its folder, named as the book names it
        public static IReadOnlyList<(Game.Campaigns.Loaded Campaign, MapPlace Place)> Campaigns =>
            GameState.Campaigns.Campaigns.Where(c => c.Manifest != null)
                     .Select(c => (c, MapPlace.OfCampaign(c.Folder, c.Manifest.NameKey)))
                     .ToList();

        public void Open(Launch launch, string campaign = null)
        {
            var campaigns = Campaigns;
            _shown = campaign ?? _shown ?? campaigns.FirstOrDefault().Campaign?.Id;

            if (!launch.CanSpread)
            {
                OnPanel(launch, campaigns);
                return;
            }

            launch.Spread((left, right) =>
            {
                left.AddChild(Ui.Title(MapBuilderView.TitleKey));
                NewMap(launch, left, campaigns);

                Lists(launch, right, campaigns);
                right.AddChild(Ui.Button(Content.Workshop.WorkshopWords.TitleKey, () => Back(launch)));
            }, () => OnPanel(launch, campaigns));
        }

        // the pages too small to read: the same, on a panel over the book
        void OnPanel(Launch launch, IReadOnlyList<(Game.Campaigns.Loaded Campaign, MapPlace Place)> campaigns)
        {
            var page = Ui.Column(10, Ui.Title(MapBuilderView.TitleKey));

            NewMap(launch, page, campaigns);
            Lists(launch, page, campaigns);
            page.AddChild(Ui.Button(Content.Workshop.WorkshopWords.TitleKey, () => Back(launch)));

            launch.Show(Ui.Panel(Ui.Scroll(page, 560)));
        }

        // My maps, then a campaign's maps: the campaign chosen, and its maps
        void Lists(Launch launch, VBoxContainer page, IReadOnlyList<(Game.Campaigns.Loaded Campaign, MapPlace Place)> campaigns)
        {
            page.AddChild(Ui.Title(MapPlace.LibraryKey));
            Maps(launch, page, Library);

            page.AddChild(Ui.Title(MapBuilderView.CampaignMapsKey));

            if (campaigns.Count == 0)
            {
                page.AddChild(Ui.Label(MapBuilderView.NoCampaignsKey));
                return;
            }

            var choose = new OptionButton { Name = "Campaign", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false };

            for (int i = 0; i < campaigns.Count; i++)
            {
                choose.AddItem(Ui.Say(campaigns[i].Campaign.Manifest.NameKey), i);
                if (campaigns[i].Campaign.Id == _shown) choose.Selected = i;
            }

            choose.ItemSelected += i => Open(launch, campaigns[(int)i].Campaign.Id);
            page.AddChild(Ui.Row(8, Ui.Label(MapBuilderView.CampaignKey), choose));

            var shown = campaigns.FirstOrDefault(c => c.Campaign.Id == _shown);
            if (shown.Place != null) Maps(launch, page, shown.Place);
        }

        // a place's maps, each a line of the contents that opens it
        static void Maps(Launch launch, VBoxContainer page, MapPlace place)
        {
            var ids = MapFiles.Ids(place);

            if (ids.Count == 0) page.AddChild(Ui.Label(MapBuilderView.NoMapsKey));

            foreach (string id in ids)
            {
                string one = id;
                page.AddChild(Launch.Leader(Ui.Button(one, () => OpenMap(launch, place, one), true),
                                            (place.IsLibrary ? "MyMap_" : "CampaignMap_") + one));
            }
        }

        // A NEW ONE: its name (the file's), its size, what it starts as, where it lives, and the button
        static void NewMap(Launch launch, VBoxContainer page, IReadOnlyList<(Game.Campaigns.Loaded Campaign, MapPlace Place)> campaigns)
        {
            page.AddChild(Ui.Label(MapBuilderView.NewKey));

            var id = new LineEdit { Name = "MapId", PlaceholderText = "map_id", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            SpinBox columns = Side("Columns", 12), rows = Side("Rows", 10);
            Label why = Ui.Label(MapBuilderView.IdRuleKey);
            why.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            why.Visible = false;

            var template = new OptionButton { Name = "Template", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false };
            foreach (string one in MapTemplates.Ids) template.AddItem(Ui.Say(MapTemplates.NameKey(one)));
            template.Selected = MapTemplates.Ids.ToList().IndexOf(MapTemplates.Standard);

            // a template's own size, when it's picked: the size boxes are only a starting point
            template.ItemSelected += i =>
            {
                MapDraft drawn = MapTemplates.Read(MapTemplates.Ids[(int)i]);
                if (drawn == null) return;
                columns.Value = drawn.Columns;
                rows.Value = drawn.Rows;
            };

            var lives = new OptionButton { Name = "Lives", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false };
            lives.AddItem(Ui.Say(MapPlace.LibraryKey));
            foreach (var (campaign, _) in campaigns) lives.AddItem(Ui.Say(campaign.Manifest.NameKey));
            lives.Selected = 0;

            page.AddChild(Ui.Row(8, Ui.Label(MapBuilderView.IdKey), id));
            page.AddChild(Ui.Row(8, Ui.Label(MapBuilderView.ColumnsKey), columns, Ui.Label(MapBuilderView.RowsKey), rows));
            page.AddChild(Ui.Row(8, Ui.Label(MapBuilderView.TemplateKey), template));
            page.AddChild(Ui.Row(8, Ui.Label(MapBuilderView.LivesKey), lives));
            page.AddChild(why);

            Button make = Ui.Button(MapBuilderView.MakeKey, () =>
            {
                string name = id.Text.Trim();
                MapPlace place = lives.Selected <= 0 ? Library : campaigns[lives.Selected - 1].Place;

                // a name that's taken opens that map rather than writing over it
                if (MapFiles.Has(place, name))
                {
                    OpenMap(launch, place, name);
                    return;
                }

                why.Visible = !Content.Campaigns.ContentId.IsLocal(name);
                if (why.Visible) return;

                Build(launch, new MapBuilderView(
                    MapBuilderView.Fresh((int)columns.Value, (int)rows.Value, MapTemplates.Ids[Math.Max(0, template.Selected)]),
                    place, name));
            });
            make.Name = "MakeMap";
            page.AddChild(make);
        }

        static SpinBox Side(string name, int value) => new SpinBox
        {
            Name = name,
            MinValue = MapBuilderView.MinSide,
            MaxValue = MapDraft.MaxSide,
            Value = value,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        // one of a place's maps, in the builder at the table
        public static void OpenMap(Launch launch, MapPlace place, string id)
        {
            if (!MapFiles.Read(place, id, out MapDraft draft, out string problem))
            {
                GD.PushError($"map builder: {id} could not be read - {problem}");
                return;
            }

            var editor = new MapEditor(draft);
            editor.MarkSaved();

            Build(launch, new MapBuilderView(editor, place, id));
        }

        // the table, as the builder of this map
        public static void Build(Launch launch, MapBuilderView view)
        {
            GameState.Build(view);

            // leaving the builder comes back to the Workshop, where every way into it starts
            GameState.BackTo = Game.Workshop.WorkshopPage.PageId;
            Scenes.Go(launch.GetTree(), Scenes.Table);
        }
    }
}
