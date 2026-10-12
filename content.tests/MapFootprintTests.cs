using System.Linq;
using Content.Maps;
using Core.Space;

namespace Content.Tests
{
    // cc_task_l Part 3.3: props bigger than a square. A prop covers its footprint, turns with its rotation, can't overlap
    // another blocking prop, and is a problem off the map or over the start or a spawn
    public class MapFootprintTests
    {
        const string Palette = @"{ ""categories"": [""buildings"", ""trees""], ""props"": [
            { ""id"": ""house"", ""category"": ""buildings"", ""blocks"": true, ""size"": [3, 2] },
            { ""id"": ""rug"", ""category"": ""buildings"", ""size"": [2, 2] },
            { ""id"": ""tree"", ""category"": ""trees"", ""blocks"": true } ] }";

        static MapDraft Field(int columns = 8, int rows = 6)
        {
            Assert.True(PropCatalogue.TryRead(Palette, out PropCatalogue palette, out var problems), string.Join("; ", problems));

            var draft = new MapDraft(columns, rows) { Palette = palette };
            draft.Paint(new Cell(0, 0), new Cell(columns - 1, rows - 1), Brush.Of(Ground.Grass));
            draft.PlaceStart(new Cell(0, 0));
            return draft;
        }

        [Fact]
        public void AFootprintCoversItsSizeAndATurnSwapsAcrossAndDeep()
        {
            MapDraft draft = Field();

            Assert.True(draft.PlaceProp("house", new Cell(2, 1)));
            Prop house = draft.Props.Single();

            Assert.Equal(6, draft.Covers(house).Count);
            Assert.Contains(new Cell(4, 2), draft.Covers(house));
            Assert.True(draft.IsBlocked(new Cell(4, 2)));
            Assert.False(draft.IsBlocked(new Cell(5, 2)));

            Prop turned = draft.TurnProp(house, 1);
            Assert.NotNull(turned);
            Assert.Contains(new Cell(3, 3), draft.Covers(turned));
            Assert.DoesNotContain(new Cell(4, 1), draft.Covers(turned));

            // the rules' map has every square of it blocked
            MapLayout map = draft.Layout();
            Assert.Equal(6, map.Blocked.Count);
            Assert.False(map.IsPassable(new Cell(3, 3)));
        }

        [Fact]
        public void TwoBlockingPropsCantShareASquareButARugCanGoUnderAnything()
        {
            MapDraft draft = Field();
            Assert.True(draft.PlaceProp("house", new Cell(2, 1)));

            Assert.False(draft.PlaceProp("tree", new Cell(4, 2)));
            Assert.False(draft.PlaceProp("house", new Cell(4, 2)));
            Assert.True(draft.PlaceProp("tree", new Cell(5, 2)));
            Assert.True(draft.PlaceProp("rug", new Cell(3, 2)));
        }

        [Fact]
        public void APropMustFitOnTheMapAndOffTheStartAndTheSpawns()
        {
            MapDraft draft = Field();
            draft.PlaceSpawn(1, new Cell(6, 4));

            Assert.False(draft.PlaceProp("house", new Cell(6, 1)));    // runs off the east edge
            Assert.False(draft.PlaceProp("house", new Cell(0, 0)));    // over the start
            Assert.False(draft.PlaceProp("house", new Cell(4, 3)));    // over spawn 1

            draft.Paint(new Cell(3, 4), Tile.Void);
            Assert.False(draft.PlaceProp("house", new Cell(1, 3)));    // a corner in the rock
        }

        [Fact]
        public void APropAMapFileOrAChangedPaletteLeftWrongIsAProblem()
        {
            MapDraft draft = Field();
            string saved = draft.Save().Replace("\"props\": []",
                "\"props\": [ { \"id\": \"house\", \"x\": 6, \"y\": 0, \"turn\": 0 }, " +
                "{ \"id\": \"house\", \"x\": 1, \"y\": 2, \"turn\": 0 }, { \"id\": \"tree\", \"x\": 2, \"y\": 3, \"turn\": 0 }, " +
                "{ \"id\": \"house\", \"x\": 0, \"y\": 0, \"turn\": 1 } ]");

            Assert.True(MapDraft.TryRead(saved, out MapDraft back, out string problem), problem);
            back.Palette = draft.Palette;

            var found = back.Findings().Select(f => f.Said.Key).ToList();

            Assert.Contains(MapProblem.KeyFor("prop_off_map"), found);
            Assert.Contains(MapProblem.KeyFor("props_overlap"), found);
            Assert.Contains(MapProblem.KeyFor("start_in_prop"), found);
        }

        [Fact]
        public void APickedUpPropMovesAndTurnsAsOneUndoStepAndRefusesWhereItWontFit()
        {
            MapDraft draft = Field();
            draft.PlaceProp("house", new Cell(2, 1));
            draft.PlaceProp("tree", new Cell(6, 4));
            Prop house = draft.PropAt(new Cell(4, 2));

            Assert.Equal("house", house.Id);

            Assert.Null(draft.MoveProp(house, new Cell(4, 3), 0));    // onto the tree
            Prop moved = draft.MoveProp(house, new Cell(2, 3), 0);
            Assert.NotNull(moved);
            Assert.True(draft.IsBlocked(new Cell(4, 4)));
            Assert.False(draft.IsBlocked(new Cell(2, 1)));

            Assert.True(draft.Undo());
            Assert.True(draft.IsBlocked(new Cell(2, 1)));

            // still found after the undo rebuilt the list: a prop is a value
            Assert.True(draft.RemoveProp(house));
            Assert.Single(draft.Props);
        }

        [Fact]
        public void TheSrdPaletteHasBuildingsBiggerThanASquare()
        {
            PropCatalogue palette = PropCatalogue.Srd();

            Assert.All(palette.In("buildings").Where(p => p.Blocks), p => Assert.True(p.Width * p.Depth >= 2, p.Id));
            Assert.Equal((4, 4), (palette.Find("barn").Width, palette.Find("barn").Depth));
            Assert.False(PropCatalogue.TryRead(@"{ ""categories"": [""a""], ""props"": [
                { ""id"": ""huge"", ""category"": ""a"", ""size"": [9, 1] },
                { ""id"": ""flat"", ""category"": ""a"", ""size"": [2] } ] }", out _, out var problems));
            Assert.Equal(2, problems.Count);
        }
    }
}
