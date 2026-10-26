using System;
using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE MAP BUILDER'S PAGE (cc_task_f Part 2; cc_task_l Part 1; cc_task_m2 Part 7), reached from the Workshop's My
    // maps: a new map (its name, a size, a starting template - Blank first - and where it lives: "My maps", the default,
    // or a campaign), then My maps and a campaign's maps, each a line that opens it, then the Workshop to go back. In a
    // straight box like the Workshop's hub and the new-campaign wizard, not written on the book's pages. A map's name is
    // what its author types; its id, the file's name, is made from it and never shown here (MapName). The table
    // comes up as the builder (GameState.Building). A map is saved where it lives (MapFiles): a campaign's loads in that
    // campaign with no conversion. `--build` and `--map-probe` are the dev registry's (Dev/DevHooks.cs)
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

            var page = Ui.Column(10, Ui.Title(MapBuilderView.TitleKey));
            page.Name = "MapBuilderPage";

            NewMap(launch, page, campaigns);
            Lists(launch, page, campaigns);
            page.AddChild(Ui.Button(Content.Workshop.WorkshopWords.TitleKey, () => Back(launch)));

            launch.Show(Ui.Panel(Ui.Scroll(page, 560)), 1040);
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

        // a place's maps, each a line that opens it, by its name (its id when it has none)
        static void Maps(Launch launch, VBoxContainer page, MapPlace place)
        {
            var ids = MapFiles.Ids(place);

            if (ids.Count == 0)
                page.AddChild(Ui.Label(place.IsLibrary ? Content.Workshop.WorkshopWords.NoMyMapsKey : MapBuilderView.NoMapsKey));

            foreach (string id in ids)
            {
                string one = id;
                page.AddChild(Launch.Leader(Ui.Button(MapName.Of(place, one), () => OpenMap(launch, place, one), true),
                                            (place.IsLibrary ? "MyMap_" : "CampaignMap_") + one));
            }
        }

        // A NEW ONE: its name, its size, what it starts as, where it lives, and the button. The name's rule is said in red
        // directly under its box while it's broken. The id it will have is never shown here (cc_task_m3 Part 3: Advanced
        // is gone from this page; MapName makes the id)
        static void NewMap(Launch launch, VBoxContainer page, IReadOnlyList<(Game.Campaigns.Loaded Campaign, MapPlace Place)> campaigns)
        {
            page.AddChild(Ui.Label(MapBuilderView.NewKey));

            var name = new LineEdit { Name = "MapName", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            Label rule = Game.Workshop.Forms.NameRule(name);
            SpinBox columns = Side("Columns", 12), rows = Side("Rows", 10);

            var template = new OptionButton { Name = "Template", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false };
            foreach (string one in MapTemplates.Ids) template.AddItem(Ui.Say(MapTemplates.NameKey(one)));
            template.Selected = MapTemplates.Ids.ToList().IndexOf(MapTemplates.Standard);

            // a template's own size, when it's picked: the size boxes are only a starting point. Blank has no size of its
            // own: it takes the boxes' (cc_task_m3 Part 3)
            template.ItemSelected += i =>
            {
                MapDraft drawn = MapTemplates.TakesTheSize(MapTemplates.Ids[(int)i]) ? null : MapTemplates.Read(MapTemplates.Ids[(int)i]);
                if (drawn == null) return;
                columns.Value = drawn.Columns;
                rows.Value = drawn.Rows;
            };

            var lives = new OptionButton { Name = "Lives", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false };
            lives.AddItem(Ui.Say(MapPlace.LibraryKey));
            foreach (var (campaign, _) in campaigns) lives.AddItem(Ui.Say(campaign.Manifest.NameKey));
            lives.Selected = 0;

            MapPlace Place() => lives.Selected <= 0 ? Library : campaigns[lives.Selected - 1].Place;

            page.AddChild(Ui.Row(8, Ui.Label(MapBuilderView.IdKey), name));
            page.AddChild(rule);
            page.AddChild(Ui.Row(8, Ui.Label(MapBuilderView.ColumnsKey), columns, Ui.Label(MapBuilderView.RowsKey), rows));
            page.AddChild(Ui.Row(8, Ui.Label(MapBuilderView.TemplateKey), template));
            page.AddChild(Ui.Row(8, Ui.Label(MapBuilderView.LivesKey), lives));

            Button make = Ui.Button(MapBuilderView.MakeKey, () =>
            {
                string typed = name.Text.Trim();

                if (!MapName.Allowed(typed))
                {
                    rule.Visible = true;
                    name.GrabFocus();
                    return;
                }

                // a name whose id is taken there gets the next free id: two maps may share a name, never a file
                MapPlace place = Place();
                var view = new MapBuilderView(
                    MapBuilderView.Fresh((int)columns.Value, (int)rows.Value, MapTemplates.Ids[Math.Max(0, template.Selected)]),
                    place, MapName.IdIn(place, typed));
                view.Draft.Name = typed;

                Build(launch, view);
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
