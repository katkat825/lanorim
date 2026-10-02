using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // THE BOOK'S "MAP BUILDER" (cc_task_f Part 2): pick a campaign, then open one of its maps or start a new one at a
    // size; the table comes up as the builder (GameState.Building, Game.Builder.MapBuilder). A map is saved where its
    // campaign keeps them (<campaign>/maps/<id>.map, MapFiles), so it loads in that campaign with no conversion
    public partial class Launch
    {
        Game.Campaigns.Loaded _building;

        void ShowMapBuilder() => ShowMapBuilder(null);

        void ShowMapBuilder(string campaign)
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

                choose.ItemSelected += i => ShowMapBuilder(campaigns[(int)i].Id);
                page.AddChild(Ui.Row(10, Ui.Label(MapBuilderView.CampaignKey), choose));

                Maps(page, _building);
                NewMap(page, _building);
            }

            page.AddChild(Ui.Button(ScreenWords.Back, ShowBook));

            Show(Ui.Panel(Ui.Scroll(page, 560)));
        }

        // the campaign's maps, each a button that opens it
        void Maps(VBoxContainer page, Game.Campaigns.Loaded campaign)
        {
            page.AddChild(Ui.Label(MapBuilderView.OpenKey));

            var ids = MapFiles.Ids(campaign.Folder);

            if (ids.Count == 0) page.AddChild(Ui.Label(MapBuilderView.NoMapsKey));

            HFlowContainer maps = Ui.Flow(8);

            foreach (string id in ids)
            {
                string one = id;
                maps.AddChild(Ui.Button(one, () => OpenMap(campaign.Folder, one), true));
            }

            page.AddChild(maps);
        }

        // a new one: its name (the file's) and its size
        void NewMap(VBoxContainer page, Game.Campaigns.Loaded campaign)
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
                    OpenMap(campaign.Folder, name);
                    return;
                }

                why.Visible = !Content.Campaigns.ContentId.IsLocal(name);
                if (why.Visible) return;

                Build(new MapBuilderView(MapBuilderView.Fresh((int)columns.Value, (int)rows.Value), campaign.Folder, name));
            }));
        }

        static SpinBox Side(int value) => new SpinBox
        {
            MinValue = MapBuilderView.MinSide,
            MaxValue = MapDraft.MaxSide,
            Value = value,
            Rounded = true,
        };

        void OpenMap(string campaign, string id)
        {
            if (!MapFiles.Read(campaign, id, out MapDraft draft, out string problem))
            {
                GD.PushError($"map builder: {id} could not be read - {problem}");
                return;
            }

            var editor = new MapEditor(draft);
            editor.MarkSaved();

            Build(new MapBuilderView(editor, campaign, id));
        }

        void Build(MapBuilderView view)
        {
            GameState.Build(view);
            Scenes.Go(GetTree(), Scenes.Table);
        }

        // `--build <campaign> [map]`: that campaign's map opened in the builder, or a new one called `probe_map`, for a
        // picture of the builder; `--map-probe`: the builder driven (checks/check-mapbuilder.ps1)
        bool BuildFrom(string[] args)
        {
            // the map builder, driven (checks/check-mapbuilder.ps1): a new map in a throwaway campaign, at the table
            if (args.Contains(Game.Builder.MapBuilderProbe.Flag))
            {
                Build(new MapBuilderView(MapBuilderView.Fresh(12, 10), Game.Builder.MapBuilderProbe.MakeCampaign(),
                                         Game.Builder.MapBuilderProbe.MapId));
                return true;
            }

            int at = System.Array.IndexOf(args, "--build");
            if (at < 0 || at + 1 >= args.Length) return false;

            Game.Campaigns.Loaded campaign = GameState.Campaigns.Campaign(args[at + 1]);

            if (campaign == null)
            {
                GD.PushError($"map builder: no campaign '{args[at + 1]}'");
                return false;
            }

            string map = at + 2 < args.Length && !args[at + 2].StartsWith("--") ? args[at + 2] : null;

            if (map != null && MapFiles.Ids(campaign.Folder).Contains(map)) OpenMap(campaign.Folder, map);
            else Build(new MapBuilderView(MapBuilderView.Fresh(12, 10), campaign.Folder, map ?? "probe_map"));

            return true;
        }
    }
}
