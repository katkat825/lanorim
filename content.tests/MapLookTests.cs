using System.IO;
using System.Linq;
using Content.Maps;
using Core.Space;

namespace Content.Tests
{
    // cc_task_l Part 2 and 3.4: a look for every square apart from its rule (grounds), deep water and chasms, wall styles
    // and the fence's sight rule, and the map file's format 2 beside format 1
    public class MapLookTests
    {
        static MapDraft Field(int columns = 6, int rows = 4)
        {
            var draft = new MapDraft(columns, rows);
            draft.Paint(new Cell(0, 0), new Cell(columns - 1, rows - 1), Brush.Of(Ground.Grass));
            return draft;
        }

        [Fact]
        public void PaintingAGroundSetsItsLookAndItsDefaultRule()
        {
            var draft = new MapDraft(4, 2);

            Assert.True(draft.Paint(new Cell(0, 0), Ground.Grass));
            Assert.Equal((Tile.Floor, Ground.Grass), (draft.At(new Cell(0, 0)), draft.GroundAt(new Cell(0, 0))));

            Assert.True(draft.Paint(new Cell(1, 0), Ground.Water));
            Assert.Equal(Tile.Rough, draft.At(new Cell(1, 0)));

            Assert.True(draft.Paint(new Cell(2, 0), Ground.Snow));
            Assert.Equal(Tile.Rough, draft.At(new Cell(2, 0)));

            // a rule painted over a look keeps the look: a rocky patch in the grass
            Assert.True(draft.Paint(new Cell(0, 0), Tile.Rough));
            Assert.Equal((Tile.Rough, Ground.Grass), (draft.At(new Cell(0, 0)), draft.GroundAt(new Cell(0, 0))));

            // and one undo takes the rule back off, leaving the grass
            Assert.True(draft.Undo());
            Assert.Equal((Tile.Floor, Ground.Grass), (draft.At(new Cell(0, 0)), draft.GroundAt(new Cell(0, 0))));
        }

        [Fact]
        public void TheLayersRoundTripThroughFormatTwo()
        {
            MapDraft draft = Field();
            draft.Paint(new Cell(1, 1), new Cell(3, 1), Brush.Of(Ground.Cobble));
            draft.Paint(new Cell(5, 3), Tile.Chasm);
            draft.Wall(new[] { new Border(new Cell(2, 2), true), new Border(new Cell(2, 3), true) }, Edge.Wall, WallStyle.Fence);
            draft.Wall(new Border(new Cell(1, 0), false), Edge.Wall, WallStyle.Plaster);
            draft.PlaceStart(new Cell(0, 0));

            string saved = draft.Save();

            Assert.Contains("\"format\": 2", saved);
            Assert.Contains("\"ground\"", saved);
            Assert.Contains("\"walls\"", saved);

            Assert.True(MapDraft.TryRead(saved, out MapDraft back, out string problem), problem);
            Assert.Equal(saved, back.Save());
            Assert.Equal(Ground.Cobble, back.GroundAt(new Cell(2, 1)));
            Assert.Equal(Tile.Chasm, back.At(new Cell(5, 3)));
            Assert.Equal(WallStyle.Fence, back.StyleAt(new Border(new Cell(2, 3), true)));
            Assert.Equal(WallStyle.Plaster, back.StyleAt(new Border(new Cell(1, 0), false)));
        }

        [Fact]
        public void AFormatOneMapReadsAsStoneAndASampleMapReadsUnchanged()
        {
            const string old = "{ \"format\": 1, \"columns\": 2, \"rows\": 1, \"map\": \"+-+-+\\n|@ .|\\n+-+-+\" }";

            Assert.True(MapDraft.TryRead(old, out MapDraft draft, out string problem), problem);
            Assert.All(draft.Extent.Cells, c => Assert.Equal(Ground.Stone, draft.GroundAt(c)));
            Assert.Equal(WallStyle.Stone, draft.StyleAt(new Border(new Cell(0, 0), true)));

            // a map with no look writes no layers: only the format number moves
            Assert.DoesNotContain("\"ground\"", draft.Save());
            Assert.DoesNotContain("\"walls\"", draft.Save());

            string maps = Path.Combine(System.AppContext.BaseDirectory, "campaigns", "sample_millbrook", "maps");

            foreach (string file in Directory.GetFiles(maps, "*.map"))
            {
                Assert.True(MapDraft.TryRead(File.ReadAllText(file), out MapDraft sample, out string bad), bad);
                Assert.True(sample.Sound, string.Join("; ", sample.Problems()));
                Assert.All(sample.Extent.Cells, c => Assert.Equal(Ground.Stone, sample.GroundAt(c)));
            }
        }

        [Theory]
        [InlineData("{ \"format\": 3, \"columns\": 1, \"rows\": 1, \"map\": \"+-+\\n|@|\\n+-+\" }", "format 3")]
        [InlineData("{ \"format\": 2, \"columns\": 2, \"rows\": 1, \"map\": \"+-+-+\\n|@ .|\\n+-+-+\", \"ground\": \"gz\" }", "'z'")]
        [InlineData("{ \"format\": 2, \"columns\": 2, \"rows\": 1, \"map\": \"+-+-+\\n|@ .|\\n+-+-+\", \"ground\": \"g\" }", "1 letters")]
        [InlineData("{ \"format\": 2, \"columns\": 1, \"rows\": 1, \"map\": \"+-+\\n|@|\\n+-+\", \"walls\": \"+q+\\n .  \\n+ +\" }", "'q'")]
        public void ABadLayerSaysWhatIsWrong(string text, string says)
        {
            Assert.False(MapDraft.TryRead(text, out _, out string problem));
            Assert.Contains(says, problem);
        }

        [Fact]
        public void AChasmIsSeenAcrossAndNeverCrossed()
        {
            MapDraft draft = Field(5, 1);
            draft.Paint(new Cell(2, 0), Tile.Chasm);
            draft.PlaceStart(new Cell(0, 0));

            MapLayout map = draft.Layout();

            Assert.False(Route.Exists(map, new Cell(0, 0), new Cell(4, 0), _ => false));
            Assert.True(Sight.Clear(map, new Cell(0, 0), new Cell(4, 0)));

            // rock, by contrast, blocks both
            draft.Paint(new Cell(2, 0), Tile.Void);
            Assert.False(Sight.Clear(draft.Layout(), new Cell(0, 0), new Cell(4, 0)));
        }

        [Fact]
        public void AFenceOrAHedgeStopsAWalkerAndNotAnEyeAndAStoneWallStopsBoth()
        {
            MapDraft draft = Field(4, 1);
            draft.PlaceStart(new Cell(0, 0));
            var line = new Border(new Cell(2, 0), true);

            foreach (WallStyle style in new[] { WallStyle.Fence, WallStyle.Hedge })
            {
                draft.Wall(line, Edge.Wall, style);
                MapLayout map = draft.Layout();

                Assert.Equal(Edge.Bars, map.At(line));
                Assert.False(Route.Exists(map, new Cell(0, 0), new Cell(3, 0), _ => false));
                Assert.True(Sight.Clear(map, new Cell(0, 0), new Cell(3, 0)));
            }

            draft.Wall(line, Edge.Wall, WallStyle.Fieldstone);
            Assert.Equal(Edge.Wall, draft.Layout().At(line));
            Assert.False(Sight.Clear(draft.Layout(), new Cell(0, 0), new Cell(3, 0)));

            // the file writes the fence as the wall it is, and its style says fence
            draft.Wall(line, Edge.Wall, WallStyle.Fence);
            Assert.True(MapDraft.TryRead(draft.Save(), out MapDraft back, out _));
            Assert.Equal(Edge.Wall, back.At(line));
            Assert.Equal(Edge.Bars, back.Layout().At(line));
        }

        [Fact]
        public void ADoorTakesItsWallsStyleAndAStyleChangeOnTheSameWallIsAnEdit()
        {
            MapDraft draft = Field(5, 3);
            var run = Enumerable.Range(0, 3).Select(y => new Border(new Cell(2, y), true)).ToList();

            draft.Wall(run, Edge.Wall, WallStyle.Fence);
            Assert.True(draft.Wall(run[1], Edge.Door, WallStyle.Stone));
            Assert.Equal(WallStyle.Fence, draft.StyleAt(run[1]));

            // a door standing on its own is the style chosen
            var alone = new Border(new Cell(4, 0), false);
            draft.Wall(alone, Edge.Door, WallStyle.Timber);
            Assert.Equal(WallStyle.Timber, draft.StyleAt(alone));

            // the same wall in another style is an edit, and one undo puts the old style back
            Assert.Equal(1, draft.Wall(new[] { run[0] }, Edge.Wall, WallStyle.Hedge));
            Assert.True(draft.Undo());
            Assert.Equal(WallStyle.Fence, draft.StyleAt(run[0]));

            // and a line erased forgets its style
            draft.Wall(run[0], Edge.None);
            Assert.Equal(WallStyle.Stone, draft.StyleAt(run[0]));
        }

        // a hero opens doors in play, so the room past one isn't cut off (the house template's rooms)
        [Fact]
        public void ASpawnPastAShutDoorCanBeReachedAndOnePastAWallCant()
        {
            MapDraft draft = Field(4, 1);
            draft.PlaceStart(new Cell(0, 0));
            draft.PlaceSpawn(1, new Cell(3, 0));

            draft.Wall(new Border(new Cell(2, 0), true), Edge.Door, WallStyle.Timber);
            Assert.True(draft.Sound, string.Join("; ", draft.Problems()));

            draft.Wall(new Border(new Cell(2, 0), true), Edge.Wall, WallStyle.Timber);
            Assert.Contains(draft.Findings(), f => f.Said.Key == MapProblem.KeyFor("spawn_unreachable"));
        }

        [Fact]
        public void TheLookIsWhatTheBoardDraws()
        {
            MapDraft draft = Field(3, 2);
            draft.Wall(new Border(new Cell(1, 0), true), Edge.Wall, WallStyle.Hedge);

            MapLook look = draft.Look();

            Assert.Equal(Ground.Grass, look.At(new Cell(2, 1)));
            Assert.Equal(WallStyle.Hedge, look.At(new Border(new Cell(1, 0), true)));
            Assert.Equal(new[] { Ground.Grass }, look.Grounds.ToArray());
        }
    }
}
