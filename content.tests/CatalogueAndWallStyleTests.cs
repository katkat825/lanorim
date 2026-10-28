using System;
using System.IO;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Core.Space;
using Core.Words;

namespace Content.Tests
{
    // THE BUILDER'S SHELVES AND ITS WALLS (cc_task_m3 Parts 5 and 6): retired pieces gone from the pickers and still drawn
    // on any map that has one; the Medieval Village Pack's buildings and the farm's on the Buildings shelf; the walls and
    // fences that stood in Props now wall styles on the squares' edges, each with a door only where it has one
    public class CatalogueAndWallStyleTests
    {
        static readonly PropCatalogue Palette = PropCatalogue.Srd();

        static readonly string[] Retired =
        {
            "hut", "cottage", "longhouse", "farmhouse", "tower_house", "town_hall", "mill", "archery_range", "storehouse",
            "tree", "tree_pine", "bush", "rock_scatter", "fence_rail",
        };

        [Fact]
        public void RetiredPiecesAreOfferedByNoPickerAndStayInThePalette()
        {
            foreach (string id in Retired)
            {
                PropEntry entry = Palette.Find(id);

                Assert.NotNull(entry);
                Assert.True(entry.Retired, id);
                Assert.DoesNotContain(Palette.Matching("", p => p.Id, null, buildings: true), p => p.Id == id);
                Assert.DoesNotContain(Palette.Matching("", p => p.Id, null, buildings: false), p => p.Id == id);
                Assert.DoesNotContain(Palette.Matching(id, p => p.Id), p => p.Id == id);
            }
        }

        [Fact]
        public void AMapThatHasARetiredPieceStillReadsAndCoversItsSquares()
        {
            var draft = new MapDraft(8, 8);
            draft.Paint(new Cell(0, 0), new Cell(7, 7), Tile.Floor);
            Assert.True(draft.PlaceProp("cottage", new Cell(2, 2), 0));

            Assert.True(MapDraft.TryRead(draft.Save(), out MapDraft again, out string problem), problem);

            Prop cottage = Assert.Single(again.Props);
            Assert.Equal("cottage", cottage.Id);
            Assert.Equal(4, Palette.Footprint(cottage).Count);
            Assert.True(again.IsBlocked(new Cell(3, 3)));
            Assert.DoesNotContain(again.Findings(), f => f.Said.Key.Contains("prop", StringComparison.Ordinal));
        }

        [Fact]
        public void NoTemplateUsesARetiredPiece()
        {
            foreach (string id in MapTemplates.Ids)
                Assert.All(MapTemplates.Read(id).Props, p => Assert.False(Palette.Find(p.Id)?.Retired ?? false, $"{id}: {p.Id}"));
        }

        [Theory]
        [InlineData("bell_tower", 2, 2)]
        [InlineData("blacksmith", 3, 3)]
        [InlineData("village_house_1", 2, 2)]
        [InlineData("village_house_2", 2, 3)]
        [InlineData("village_house_3", 2, 2)]
        [InlineData("village_house_4", 2, 2)]
        [InlineData("inn", 3, 3)]
        [InlineData("village_mill", 3, 2)]
        [InlineData("sawmill", 4, 3)]
        [InlineData("stable", 4, 3)]
        public void TheMedievalVillagePacksBuildingsAreOnTheBuildingsShelf(string id, int across, int deep)
        {
            PropEntry building = Palette.Find(id);

            Assert.Equal(PropCatalogue.BuildingsCategory, building.Category);
            Assert.StartsWith("Medieval Village Pack - Dec 2020", building.Pack);
            Assert.True(building.Blocks);
            Assert.Equal((across, deep), (building.Width, building.Depth));
            Assert.Contains(Palette.Matching("", p => p.Id, null, buildings: true), p => p.Id == id);
        }

        [Fact]
        public void TheFarmBuildingsAreBuildingsAndTheFieldsStayProps()
        {
            foreach (string id in new[] { "barn", "barn_big", "barn_small", "barn_open", "chicken_coop", "silo", "silo_barn",
                                          "water_tower", "windmill", "windmill_tower", "farmstead" })
                Assert.Equal(PropCatalogue.BuildingsCategory, Palette.Find(id).Category);

            foreach (string id in new[] { "wheat_field", "ploughed_field", "log_pile", "sack", "farm_crate_empty" })
                Assert.Equal("farm", Palette.Find(id).Category);
        }

        [Fact]
        public void NoWallDoorOrFenceIsAProp()
        {
            Assert.Empty(Palette.Matching("", p => p.Id, "walls_fences", buildings: false));
            Assert.DoesNotContain(Palette.Matching("", p => p.Id, null, buildings: false),
                                  p => p.Id.StartsWith("ruin_wall") || p.Id.StartsWith("ruin_arch") || p.Id == "ruin_doors");

            // what wasn't a wall at all moved to where it belongs, and the gatehouses are buildings
            Assert.Equal("village_market", Palette.Find("kerb").Category);
            Assert.Equal("ruins", Palette.Find("railing").Category);
            Assert.Equal(PropCatalogue.BuildingsCategory, Palette.Find("gatehouse").Category);
            Assert.Equal(PropCatalogue.BuildingsCategory, Palette.Find("gatehouse_stone").Category);
        }

        // --- the wall styles -------------------------------------------------------------------------------------------

        [Fact]
        public void EveryStyleHasItsOwnLetterAndANewStyleReadsAndWrites()
        {
            WallStyle[] styles = Enum.GetValues<WallStyle>();
            Assert.Equal(styles.Length, styles.Select(s => WallStyles.Letter.Of(s)).Distinct().Count());

            var draft = new MapDraft(styles.Length + 2, 4);
            draft.Paint(new Cell(0, 0), new Cell(draft.Columns - 1, 3), Tile.Floor);

            for (int i = 0; i < styles.Length; i++) Assert.True(draft.Wall(new Border(new Cell(i, 1), false), Edge.Wall, styles[i]));

            Assert.True(MapDraft.TryRead(draft.Save(), out MapDraft again, out string problem), problem);

            for (int i = 0; i < styles.Length; i++) Assert.Equal(styles[i], again.StyleAt(new Border(new Cell(i, 1), false)));
        }

        [Fact]
        public void AnOldMapsWallsReadAsTheyDid()
        {
            // the old letters are the old letters: s t p f e h
            Assert.Equal("stpfeh", string.Concat(new[] { WallStyle.Stone, WallStyle.Timber, WallStyle.Plaster, WallStyle.Fieldstone,
                                                          WallStyle.Fence, WallStyle.Hedge }.Select(s => WallStyles.Letter.Of(s))));

            MapDraft street = MapTemplates.Read("village_street");
            Assert.True(MapDraft.TryRead(street.Save(), out MapDraft again, out _));
            Assert.Equal(street.Save(), again.Save());
        }

        [Fact]
        public void AStyleWithNoDoorTakesNone()
        {
            Assert.False(WallStyle.TownWall.HasDoor());
            Assert.False(WallStyle.RuinLow.HasDoor());

            var draft = new MapDraft(6, 4);
            draft.Paint(new Cell(0, 0), new Cell(5, 3), Tile.Floor);

            // a door put in a town wall's run would be a town wall's: there is none
            Assert.True(draft.Wall(new Border(new Cell(1, 1), false), Edge.Wall, WallStyle.TownWall));
            Assert.False(draft.Wall(new Border(new Cell(2, 1), false), Edge.Door, WallStyle.TownWall));
            Assert.Equal(Edge.None, draft.At(new Border(new Cell(2, 1), false)));

            // in a ruin it is a ruin's door
            Assert.True(draft.Wall(new Border(new Cell(1, 2), false), Edge.Wall, WallStyle.Ruin));
            Assert.True(draft.Wall(new Border(new Cell(2, 2), false), Edge.Door, WallStyle.Stone));
            Assert.Equal(WallStyle.Ruin, draft.StyleAt(new Border(new Cell(2, 2), false)));
        }

        [Fact]
        public void TheEditorOffersOnlyWhatItHasOnePickAtATime()
        {
            var editor = new MapEditor(new MapDraft(6, 6));

            Assert.Equal(MapBuilderView.Styles, MapEditor.StylesFor(Edge.Wall));
            Assert.DoesNotContain(WallStyle.TownWall, MapEditor.StylesFor(Edge.Door));
            Assert.DoesNotContain(WallStyle.RuinLow, MapEditor.StylesFor(Edge.Door));
            Assert.All(MapEditor.StylesFor(Edge.Door), s => Assert.True(s.HasDoor()));

            // a town wall chosen, then Door: the style moves to one that has a door
            Assert.True(editor.PickStyle(WallStyle.TownWall));
            Assert.True(editor.PickLine(Edge.Door));
            Assert.Equal(Edge.Door, editor.Line);
            Assert.True(editor.Style.HasDoor());

            // with Door chosen, a style with none can't be picked; one with a door can, and replaces the last
            Assert.False(editor.PickStyle(WallStyle.RuinLow));
            Assert.True(editor.PickStyle(WallStyle.Fieldstone));
            Assert.Equal(WallStyle.Fieldstone, editor.Style);

            // and Wall lets Door go
            Assert.True(editor.PickLine(Edge.Wall));
            Assert.Equal(Edge.Wall, editor.Line);
            Assert.Equal(WallStyle.Fieldstone, editor.Style);
        }

        // every style has its wall's scene, and its door's exactly when it has a door (the game draws them, WallLooks)
        [Fact]
        public void EveryStyleIsDrawnAndOnlyADoorStyleHasADoor()
        {
            string walls = Path.Combine(CreditsTests.Game(), "models", "walls");

            foreach (WallStyle style in Enum.GetValues<WallStyle>().Where(s => s != WallStyle.Stone))
            {
                Assert.True(File.Exists(Path.Combine(walls, style.Id(), "wall.tscn")), style.Id());
                Assert.Equal(style.HasDoor(), File.Exists(Path.Combine(walls, style.Id(), "door.tscn")));
            }
        }
    }
}
