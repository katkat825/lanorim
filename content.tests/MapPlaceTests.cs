using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Content.Screens;
using Core.Space;

namespace Content.Tests
{
    // cc_task_l Part 1: maps of your own. The "My maps" library and a campaign are both places MapFiles reads and writes;
    // Save as and Copy to campaign move a map between them and ask before writing over one; and a new map starts from a
    // template, fitted to its size
    public class MapPlaceTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_places_" + Guid.NewGuid().ToString("N"));

        MapPlace Library => MapPlace.Library(Path.Combine(_root, "maps"));

        string CampaignFolder => Path.Combine(_root, "place_test");

        MapPlace Campaign
        {
            get
            {
                Directory.CreateDirectory(CampaignFolder);
                File.WriteAllText(Path.Combine(CampaignFolder, "pack.json"),
                    "{ \"id\": \"place_test\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"0.1\", " +
                    "\"chapters\": [ { \"id\": \"one\", \"maps\": [ \"mill_yard\" ] } ], \"start\": \"one\" }");
                return MapPlace.OfCampaign(CampaignFolder, "campaign.place_test.name");
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Fact]
        public void TheLibraryAndACampaignAreBothPlacesAndAMapGoesBetweenThem()
        {
            MapEditor editor = MapBuilderView.Fresh(16, 12, "village_street");
            var view = new MapBuilderView(editor, Library, "mill_yard");

            Assert.Null(view.Save());
            Assert.Equal(new[] { "mill_yard" }, MapFiles.Ids(Library));
            Assert.True(view.Place.IsLibrary);
            Assert.Equal(MapPlace.LibraryKey, view.Place.NameKey);

            // copied into a campaign: there, the same map; and the one open is still the library's
            Assert.Null(view.CopyTo(Campaign, "mill_yard"));
            Assert.True(MapFiles.Read(Campaign, "mill_yard", out MapDraft copy, out string problem), problem);
            Assert.Equal(editor.Draft.Save(), copy.Save());
            Assert.True(view.Place.IsLibrary);

            Package pack = Package.Read(CampaignFolder);
            Assert.True(pack.Maps.ContainsKey("mill_yard"), string.Join("; ", pack.Problems.Select(p => p.What)));
            Assert.Equal(Content.Maps.Ground.Cobble, pack.LookOn("mill_yard").At(new Cell(4, 5)));
            Assert.Contains(pack.PropsOn("mill_yard"), p => p.Id == "cottage");
        }

        [Fact]
        public void NothingWritesOverAMapWithoutBeingToldTo()
        {
            var first = new MapBuilderView(MapBuilderView.Fresh(5, 5), Campaign, "cellar");
            Assert.Null(first.Save());

            var other = new MapBuilderView(MapBuilderView.Fresh(9, 9, "empty_field"), Library, "field");

            Assert.True(other.Taken(Campaign, "cellar"));
            Said refused = other.CopyTo(Campaign, "cellar");
            Assert.Equal(MapBuilderView.TakenKey, refused.Key);
            Assert.True(MapFiles.Read(Campaign, "cellar", out MapDraft still, out _));
            Assert.Equal(5, still.Columns);

            // rename: the next free name; replace: told to
            Assert.Equal("cellar_2", MapFiles.FreeId(Campaign, "cellar"));
            Assert.Null(other.CopyTo(Campaign, "cellar", replace: true));
            Assert.True(MapFiles.Read(Campaign, "cellar", out MapDraft now, out _));
            Assert.Equal(9, now.Columns);

            // a map's own file is never "taken" from itself
            Assert.False(first.Taken(Campaign, "cellar"));
        }

        [Fact]
        public void SaveAsMakesTheMapThatFile()
        {
            var view = new MapBuilderView(MapBuilderView.Fresh(6, 6), Library, "draft_one");
            Assert.Null(view.Save());

            Assert.Null(view.SaveAs(Campaign, "crypt"));
            Assert.False(view.Place.IsLibrary);
            Assert.Equal("crypt", view.Id);
            Assert.False(view.Editor.Unsaved);

            // both files are there: save as leaves the old one alone
            Assert.True(MapFiles.Has(Library, "draft_one"));
            Assert.True(MapFiles.Has(Campaign, "crypt"));
        }

        [Fact]
        public void EveryTemplateMakesASoundMapAtAnySize()
        {
            Assert.Equal(new[] { "empty_field", "dungeon_room", "forest_clearing", "village_street", "house_room" }, MapTemplates.Ids);

            foreach (string id in MapTemplates.Ids)
                foreach ((int c, int r) in new[] { (3, 3), (12, 10), (20, 16), (64, 64) })
                {
                    MapDraft draft = MapTemplates.Make(id, c, r);

                    Assert.NotNull(draft);
                    Assert.Equal((c, r), (draft.Columns, draft.Rows));
                    Assert.True(draft.At(draft.Start).IsPassable(), $"{id} at {c} x {r}");
                    Assert.False(draft.CanUndo);
                }
        }

        [Fact]
        public void TheTemplatesLookLikeTheirNames()
        {
            MapDraft field = MapTemplates.Make("empty_field", 10, 10);
            Assert.All(field.Extent.Cells, c => Assert.Equal(Content.Maps.Ground.Grass, field.GroundAt(c)));
            Assert.Equal(0, field.Layout().Borders.Count(b => field.At(b) != Edge.None));

            MapDraft room = MapTemplates.Make("dungeon_room", 20, 16);
            Assert.Equal(Edge.Wall, room.At(new Border(new Cell(0, 7), true)));
            Assert.Equal(Edge.Wall, room.At(new Border(new Cell(20, 7), true)));
            Assert.Equal(Edge.None, room.At(new Border(new Cell(10, 7), true)));
            Assert.Equal(Tile.Floor, room.At(new Cell(19, 15)));

            MapDraft forest = MapTemplates.Make("forest_clearing", 16, 14);
            Assert.True(forest.Props.Count(p => PropCatalogue.Srd().Find(p.Id)?.Category == "trees") >= 12);

            MapDraft street = MapTemplates.Make("village_street", 16, 12);
            Assert.Equal(2, street.Props.Count(p => PropCatalogue.Srd().Find(p.Id)?.Category == "buildings"));
            Assert.Contains(street.Extent.Cells, c => street.GroundAt(c) == Content.Maps.Ground.Cobble);

            MapDraft house = MapTemplates.Make("house_room", 10, 8);
            Assert.Equal(Content.Maps.Ground.Planks, house.GroundAt(new Cell(5, 5)));
            Assert.Equal(WallStyle.Timber, house.StyleAt(new Border(new Cell(0, 3), true)));
            Assert.Contains(house.Layout().Borders, b => house.At(b) == Edge.Door);

            Assert.All(MapTemplates.Ids, id => Assert.True(MapTemplates.Make(id, 16, 14).Sound, id));
        }
    }
}
