using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE BOOK'S "MAP BUILDER" (cc_task_f Part 2): pick a campaign, then open one of its maps or start a new one at a
    // size; the table comes up as the builder (GameState.Building, Game.Builder.MapBuilder). A map is saved where its
    // campaign keeps them (<campaign>/maps/<id>.map, MapFiles), so it loads in that campaign with no conversion.
    // Was Launch.Builder.cs (cc_task_j Part 1); `--build` and `--map-probe` are the dev registry's (Dev/DevHooks.cs)
    public sealed class MapBuilderPage : IBookPage
    {
        // the campaign picked, kept while the book is open
        Game.Campaigns.Loaded _building;

        public string Id => "builder";

        public string NameKey => CampaignBook.MapBuilderKey;

        public void Open(Launch launch, string campaign = null)
        {
            var campaigns = GameState.Campaigns.Campaigns.Where(c => c.Manifest != null).ToList();
            _building = campaigns.FirstOrDefault(c => c.Id == campaign) ?? _building ?? campaigns.FirstOrDefault();

            var page = Ui.Column(10, Ui.Title(MapBuilderView.TitleKey));

            if (_building == null)
            {
                page.AddChild(Ui.Label(MapBuilderView.NoCampaignsKey));
            }
            else
            {
                var choose = new OptionButton { Name = "Campaign" };

                for (int i = 0; i < campaigns.Count; i++)
                {
                    choose.AddItem(Ui.Say(campaigns[i].Manifest.NameKey), i);
                    if (campaigns[i] == _building) choose.Selected = i;
                }

                choose.ItemSelected += i => Open(launch, campaigns[(int)i].Id);
                page.AddChild(Ui.Row(10, Ui.Label(MapBuilderView.CampaignKey), choose));

                Maps(launch, page, _building);
                NewMap(launch, page, _building);
            }

            page.AddChild(Ui.Button(ScreenWords.Back, launch.ShowBook));

            launch.Show(Ui.Panel(Ui.Scroll(page, 560)));
        }

        // the campaign's maps, each a button that opens it
        static void Maps(Launch launch, VBoxContainer page, Game.Campaigns.Loaded campaign)
        {
            page.AddChild(Ui.Label(MapBuilderView.OpenKey));

            var ids = MapFiles.Ids(campaign.Folder);

            if (ids.Count == 0) page.AddChild(Ui.Label(MapBuilderView.NoMapsKey));

            HFlowContainer maps = Ui.Flow(8);

            foreach (string id in ids)
            {
                string one = id;
                maps.AddChild(Ui.Button(one, () => OpenMap(launch, campaign.Folder, one), true));
            }

            page.AddChild(maps);
        }

        // a new one: its name (the file's) and its size
        static void NewMap(Launch launch, VBoxContainer page, Game.Campaigns.Loaded campaign)
        {
            page.AddChild(Ui.Label(MapBuilderView.NewKey));

            var id = new LineEdit { Name = "MapId", PlaceholderText = "map_id", CustomMinimumSize = new Vector2(Ui.Px(220), 0) };
            SpinBox columns = Side(12), rows = Side(10);
            Label why = Ui.Label(MapBuilderView.IdRuleKey);
            why.Visible = false;

            page.AddChild(Ui.Row(10, Ui.Label(MapBuilderView.IdKey), id));
            page.AddChild(Ui.Row(10, Ui.Label(MapBuilderView.ColumnsKey), columns, Ui.Label(MapBuilderView.RowsKey), rows));
            page.AddChild(why);
            page.AddChild(Ui.Button(MapBuilderView.NewKey, () =>
            {
                string name = id.Text.Trim();

                // a name that's taken opens that map rather than writing over it
                if (MapFiles.Ids(campaign.Folder).Contains(name))
                {
                    OpenMap(launch, campaign.Folder, name);
                    return;
                }

                why.Visible = !Content.Campaigns.ContentId.IsLocal(name);
                if (why.Visible) return;

                Build(launch, new MapBuilderView(MapBuilderView.Fresh((int)columns.Value, (int)rows.Value), campaign.Folder, name));
            }));
        }

        static SpinBox Side(int value) => new SpinBox
        {
            MinValue = MapBuilderView.MinSide,
            MaxValue = MapDraft.MaxSide,
            Value = value,
            Rounded = true,
        };

        // one of a campaign's maps, in the builder at the table
        public static void OpenMap(Launch launch, string campaign, string id)
        {
            if (!MapFiles.Read(campaign, id, out MapDraft draft, out string problem))
            {
                GD.PushError($"map builder: {id} could not be read - {problem}");
                return;
            }

            var editor = new MapEditor(draft);
            editor.MarkSaved();

            Build(launch, new MapBuilderView(editor, campaign, id));
        }

        // the table, as the builder of this map
        public static void Build(Launch launch, MapBuilderView view)
        {
            GameState.Build(view);
            Scenes.Go(launch.GetTree(), Scenes.Table);
        }
    }
}
