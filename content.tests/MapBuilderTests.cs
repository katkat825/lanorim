using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Content.Screens;
using Core.Space;

namespace Content.Tests
{
    // THE MAP BUILDER'S SCREEN AS LOGIC (cc_task_f, Part 2): what a point on the board aims at, what a click or a drag
    // would change, wall runs, problems with places, and the file a campaign loads
    public class MapBuilderTests : IDisposable
    {
        // a campaign's folder is named by its id
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_builder_" + Guid.NewGuid().ToString("N"));

        string _campaign => Path.Combine(_root, "builder_test");

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Theory]
        [InlineData(2.05, 1.5, 2, 1, true)]   // just right of the line between columns 1 and 2
        [InlineData(1.95, 1.5, 2, 1, true)]   // just left of it: the same line
        [InlineData(1.5, 3.1, 1, 3, false)]   // just below the line between rows 2 and 3
        [InlineData(0.02, 0.4, 0, 0, true)]   // the map's west edge
        public void APointMeansTheLineNearestIt(double x, double y, int cx, int cy, bool vertical)
        {
            Border line = Border.Nearest(x, y, out double off);

            Assert.Equal(new Border(new Cell(cx, cy), vertical), line);
            Assert.True(off < 0.11);
        }

        [Fact]
        public void TheWallToolAimsAtLinesTheOthersAtSquaresAndTheEraserAtAWallItIsOn()
        {
            MapEditor editor = MapBuilderView.Fresh(6, 4);

            editor.Tool = MapTool.Paint;
            Assert.Equal(new MapTarget(new Cell(2, 1), null), editor.Aim(2.05, 1.5));

            editor.Tool = MapTool.Wall;
            Assert.Equal(new MapTarget(null, new Border(new Cell(2, 1), true)), editor.Aim(2.05, 1.5));

            // the eraser takes the outer wall it's on, and the square when it's in the middle of one
            editor.Tool = MapTool.Erase;
            Assert.Equal(new MapTarget(null, new Border(new Cell(0, 1), true)), editor.Aim(0.05, 1.5));
            Assert.Equal(new MapTarget(new Cell(2, 1), null), editor.Aim(2.05, 1.5));

            // off the map is nothing
            editor.Tool = MapTool.Paint;
            Assert.True(editor.Aim(-1, 2).IsNothing);
        }

        [Fact]
        public void ADragOfWallsLaysTheRunInOneUndoStep()
        {
            MapEditor editor = MapBuilderView.Fresh(6, 4);
            editor.Tool = MapTool.Wall;

            MapTarget from = editor.Aim(3.0, 0.5), to = editor.Aim(3.1, 2.6);

            var preview = editor.Preview(from, to);
            Assert.Equal(3, preview.Lines.Count);
            Assert.Empty(preview.Squares);

            Assert.Equal(3, editor.Apply(from, to));
            Assert.All(preview.Lines, l => Assert.Equal(Edge.Wall, editor.Draft.At(l)));

            editor.Draft.Undo();
            Assert.All(preview.Lines, l => Assert.Equal(Edge.None, editor.Draft.At(l)));
        }

        [Fact]
        public void TheHoverShowsAPaintedRectangleAndOneSquareForTheOtherTools()
        {
            MapEditor editor = MapBuilderView.Fresh(6, 4);

            editor.Tool = MapTool.Paint;
            Assert.Equal(6, editor.Preview(editor.Aim(1.5, 1.5), editor.Aim(3.5, 2.5)).Squares.Count);

            editor.Tool = MapTool.Spawn;
            Assert.Equal(new[] { new Cell(3, 2) }, editor.Preview(editor.Aim(1.5, 1.5), editor.Aim(3.5, 2.5)).Squares);
        }

        [Fact]
        public void ANewMapIsARoomWithItsStartOnTheFloor()
        {
            MapDraft draft = MapBuilderView.Fresh(8, 5).Draft;

            Assert.Equal((8, 5), (draft.Columns, draft.Rows));
            Assert.Empty(draft.Findings());
            Assert.Equal(Edge.Wall, draft.At(new Border(new Cell(0, 0), true)));
        }

        [Fact]
        public void AProblemSaysItsKeyAndWhereItIs()
        {
            MapEditor editor = MapBuilderView.Fresh(6, 4);
            MapDraft draft = editor.Draft;

            // a spawn walled off from the start
            draft.PlaceSpawn(2, new Cell(5, 3));
            draft.Wall(MapEditor.Run(new Border(new Cell(5, 0), true), new Border(new Cell(5, 3), true)), Edge.Wall);

            MapProblem walled = Assert.Single(draft.Findings());
            Assert.Equal("ui.map.problem_spawn_unreachable", walled.Said.Key);
            Assert.Equal(2, walled.Said.Args[0]);
            Assert.Equal(new Cell(5, 3), walled.At);
            Assert.Contains("cannot be walked to", Assert.Single(draft.Problems()));

            // a map with nothing painted has no place to show
            MapProblem empty = Assert.Single(new MapDraft(3, 3).Findings(), p => p.Said.Key.EndsWith("nothing_painted"));
            Assert.Null(empty.At);
            Assert.All(MapProblem.Names, n => Assert.Contains(MapProblem.KeyFor(n), MapBuilderView.Keys()));
        }

        // a map saved by the builder is a file its campaign loads, with no conversion
        [Fact]
        public void ASavedMapLoadsThroughItsCampaign()
        {
            Directory.CreateDirectory(_campaign);
            File.WriteAllText(Path.Combine(_campaign, "pack.json"),
                "{ \"id\": \"builder_test\", \"kind\": \"campaign\", \"format\": 1, \"engine\": \"0.1\", " +
                "\"chapters\": [ { \"id\": \"one\", \"maps\": [ \"crypt\" ] } ], \"start\": \"one\" }");

            MapEditor editor = MapBuilderView.Fresh(7, 5);
            editor.Draft.PlaceSpawn(1, new Cell(5, 3));
            editor.Draft.PlaceProp("barrel", new Cell(3, 1), 1);

            var view = new MapBuilderView(editor, _campaign, "crypt");

            Assert.True(editor.Unsaved);
            Assert.Null(view.Save());
            Assert.False(editor.Unsaved);
            Assert.Equal(MapBuilderView.SavedCleanKey, view.SavedKey);

            Assert.Equal(new[] { "crypt" }, MapFiles.Ids(_campaign));
            Assert.True(MapFiles.Read(_campaign, "crypt", out MapDraft back, out string problem), problem);
            Assert.Equal(editor.Draft.Save(), back.Save());

            Package pack = Package.Read(_campaign);
            Assert.True(pack.Maps.ContainsKey("crypt"), string.Join("; ", pack.Problems.Select(p => p.What)));
            Assert.Equal(new Cell(5, 3), pack.Maps["crypt"].SpawnAt(1));
            Assert.Single(pack.PropsOn("crypt"));

            editor.Draft.Paint(new Cell(1, 1), Tile.Rough);
            Assert.True(editor.Unsaved);
        }

        [Fact]
        public void AMapWithProblemsSavesAndSaysSoAndABadIdDoesNot()
        {
            MapEditor editor = MapBuilderView.Fresh(4, 4);
            editor.Draft.Paint(editor.Draft.Start, Tile.Void);

            var view = new MapBuilderView(editor, _campaign, "broken");
            Assert.Null(view.Save());
            Assert.Equal(MapBuilderView.SavedWithProblemsKey, view.SavedKey);

            Assert.Equal(MapBuilderView.IdRuleKey, new MapBuilderView(editor, _campaign, "Bad Name").Save().Key);
        }
    }
}
