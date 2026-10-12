using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE BOOK'S "MAP BUILDER" (cc_task_f Part 2; cc_task_l Part 1): a new map first (a name, a size, a starting template,
    // and where it lives: "My maps", the default, or a campaign), then My maps, then a campaign's maps; each a button
    // that opens it. The table comes up as the builder (GameState.Building, Game.Builder.MapBuilder). A map is saved
    // where it lives (MapFiles): a campaign's loads in that campaign with no conversion.
    // `--build` and `--map-probe` are the dev registry's (Dev/DevHooks.cs)
    public sealed class MapBuilderPage : IBookPage
    {
        // the campaign whose maps are listed, kept while the book is open
        string _shown;

        public string Id => "builder";

        public string NameKey => CampaignBook.MapBuilderKey;

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

            NewMap(launch, page, campaigns);

            page.AddChild(Ui.Title(MapPlace.LibraryKey));
            Maps(launch, page, Library);

            page.AddChild(Ui.Title(MapBuilderView.CampaignMapsKey));

            if (campaigns.Count == 0)
            {
                page.AddChild(Ui.Label(MapBuilderView.NoCampaignsKey));
            }
            else
            {
                var choose = new OptionButton { Name = "Campaign" };

                for (int i = 0; i < campaigns.Count; i++)
                {
                    choose.AddItem(Ui.Say(campaigns[i].Campaign.Manifest.NameKey), i);
                    if (campaigns[i].Campaign.Id == _shown) choose.Selected = i;
                }

                choose.ItemSelected += i => Open(launch, campaigns[(int)i].Campaign.Id);
                page.AddChild(Ui.Row(10, Ui.Label(MapBuilderView.CampaignKey), choose));

                var shown = campaigns.FirstOrDefault(c => c.Campaign.Id == _shown);
                if (shown.Place != null) Maps(launch, page, shown.Place);
            }

            page.AddChild(Ui.Button(ScreenWords.Back, launch.ShowBook));

            launch.Show(Ui.Panel(Ui.Scroll(page, 560)));
        }

        // a place's maps, each a button that opens it
        static void Maps(Launch launch, VBoxContainer page, MapPlace place)
        {
            var ids = MapFiles.Ids(place);

            if (ids.Count == 0) page.AddChild(Ui.Label(MapBuilderView.NoMapsKey));

            HFlowContainer maps = Ui.Flow(8);
            maps.Name = place.IsLibrary ? "MyMaps" : "CampaignMaps";

            foreach (string id in ids)
            {
                string one = id;
                maps.AddChild(Ui.Button(one, () => OpenMap(launch, place, one), true));
            }

            page.AddChild(maps);
        }

        // A NEW ONE, first and big: its name (the file's), its size, what it starts as, and where it lives
        static void NewMap(Launch launch, VBoxContainer page, IReadOnlyList<(Game.Campaigns.Loaded Campaign, MapPlace Place)> campaigns)
        {
            page.AddChild(Ui.Title(MapBuilderView.NewKey));

            var id = new LineEdit { Name = "MapId", PlaceholderText = "map_id", CustomMinimumSize = new Vector2(Ui.Px(220), 0) };
            SpinBox columns = Side(12), rows = Side(10);
            Label why = Ui.Label(MapBuilderView.IdRuleKey);
            why.Visible = false;

            var template = new OptionButton { Name = "Template" };
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

            var lives = new OptionButton { Name = "Lives" };
            lives.AddItem(Ui.Say(MapPlace.LibraryKey));
            foreach (var (campaign, _) in campaigns) lives.AddItem(Ui.Say(campaign.Manifest.NameKey));
            lives.Selected = 0;

            page.AddChild(Ui.Row(10, Ui.Label(MapBuilderView.IdKey), id));
            page.AddChild(Ui.Row(10, Ui.Label(MapBuilderView.ColumnsKey), columns, Ui.Label(MapBuilderView.RowsKey), rows));
            page.AddChild(Ui.Row(10, Ui.Label(MapBuilderView.TemplateKey), template));
            page.AddChild(Ui.Row(10, Ui.Label(MapBuilderView.LivesKey), lives));
            page.AddChild(why);

            Button make = Ui.Button(MapBuilderView.NewKey, () =>
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
                    MapBuilderView.Fresh((int)columns.Value, (int)rows.Value, MapTemplates.Ids[Mathf.Max(0, template.Selected)]),
                    place, name));
            });
            make.Name = "MakeMap";
            make.CustomMinimumSize = new Vector2(Ui.Px(260), Ui.Px(48));
            page.AddChild(make);
        }

        static SpinBox Side(int value) => new SpinBox
        {
            MinValue = MapBuilderView.MinSide,
            MaxValue = MapDraft.MaxSide,
            Value = value,
            Rounded = true,
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
            Scenes.Go(launch.GetTree(), Scenes.Table);
        }
    }
}
