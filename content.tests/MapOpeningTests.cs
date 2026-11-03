using System.Linq;
using Content.Maps;
using Content.Screens;
using Core.Space;

namespace Content.Tests
{
    // ARCHWAYS AND WINDOWS (cc_task_m4 Part 4): two kinds of line beside Wall and Door in Walls and doors. An archway is
    // walked through and seen through, a window seen through only (Edge.Bars to the rules, as a fence is); each style
    // offers only the kinds it has a model for; the map file writes a letter for each, and an old map never needs one
    public class MapOpeningTests
    {
        // a floor 6 x 3, a plaster wall down the line between columns 2 and 3, the hero west of it
        static MapDraft Room()
        {
            var draft = new MapDraft(6, 3);
            draft.Paint(new Cell(0, 0), new Cell(5, 2), Tile.Floor);
            draft.PlaceStart(new Cell(0, 1));

            for (int y = 0; y < 3; y++) Assert.True(draft.Wall(new Border(new Cell(2, y), true), Edge.Wall, WallStyle.Plaster));

            return draft;
        }

        static readonly Border Middle = new(new Cell(2, 1), true);

        static readonly Cell West = new(1, 1), East = new(4, 1);

        [Fact]
        public void AWallStopsBothAndIsWhereTheTestsStart()
        {
            MapLayout map = Room().Layout();

            Assert.False(Route.Exists(map, West, East, _ => false));
            Assert.False(Sight.Clear(map, West, East));
        }

        [Fact]
        public void AHeroWalksAndSeesThroughAnArchway()
        {
            MapDraft draft = Room();
            Assert.True(draft.Wall(Middle, Edge.Archway, WallStyle.Plaster));

            Assert.Equal(Edge.Archway, draft.At(Middle));
            Assert.Equal(WallStyle.Plaster, draft.StyleAt(Middle));

            MapLayout map = draft.Layout();
            Assert.Equal(Edge.None, map.At(Middle));
            Assert.True(Route.Exists(map, West, East, _ => false));
            Assert.True(Sight.Clear(map, West, East));

            // the board draws it as an arch
            Assert.Equal(Edge.Archway, draft.Look().Drawn(Middle));
        }

        [Fact]
        public void AHeroSeesThroughAWindowButCantWalkThroughIt()
        {
            MapDraft draft = Room();
            Assert.True(draft.Wall(Middle, Edge.Window, WallStyle.Plaster));

            MapLayout map = draft.Layout();
            Assert.Equal(Edge.Bars, map.At(Middle));
            Assert.False(Route.Exists(map, West, East, _ => false));
            Assert.True(Sight.Clear(map, West, East));
            Assert.Equal(Edge.Window, draft.Look().Drawn(Middle));
        }

        [Fact]
        public void TheMapFileWritesALetterForEachAndReadsThemBack()
        {
            MapDraft draft = Room();
            draft.Wall(Middle, Edge.Archway, WallStyle.Plaster);
            draft.Wall(new Border(new Cell(2, 0), true), Edge.Window, WallStyle.Plaster);

            string saved = draft.Save();

            Assert.Contains("a", saved);
            Assert.Contains("w", saved);
            Assert.True(MapDraft.TryRead(saved, out MapDraft again, out string problem), problem);
            Assert.Equal(Edge.Archway, again.At(Middle));
            Assert.Equal(Edge.Window, again.At(new Border(new Cell(2, 0), true)));
            Assert.Equal(WallStyle.Plaster, again.StyleAt(Middle));

            // an old map, with walls and doors only, reads as it always did
            Assert.True(MapDraft.TryRead(Room().Save(), out MapDraft old, out problem), problem);
            Assert.DoesNotContain(old.Layout().Borders, b => old.At(b) is Edge.Archway or Edge.Window);
        }

        [Fact]
        public void AStyleWithoutTheModelOffersNoSuchLine()
        {
            Assert.Equal(new[] { Edge.Wall, Edge.Door, Edge.Archway, Edge.Window }, MapBuilderView.Lines);

            // the dungeon's stone and a hedge have no archway or window; timber has an arch and no window
            Assert.DoesNotContain(WallStyle.Stone, MapEditor.StylesFor(Edge.Archway));
            Assert.DoesNotContain(WallStyle.Hedge, MapEditor.StylesFor(Edge.Window));
            Assert.Contains(WallStyle.Timber, MapEditor.StylesFor(Edge.Archway));
            Assert.DoesNotContain(WallStyle.Timber, MapEditor.StylesFor(Edge.Window));
            Assert.All(MapEditor.StylesFor(Edge.Window), s => Assert.True(s.HasWindow()));

            // and the draft won't lay one in a style that has none: a stone wall keeps its wall
            var draft = new MapDraft(4, 2);
            draft.Paint(new Cell(0, 0), new Cell(3, 1), Tile.Floor);
            Border line = new(new Cell(1, 0), true);
            draft.Wall(line, Edge.Wall, WallStyle.Stone);

            Assert.False(draft.Wall(line, Edge.Archway, WallStyle.Stone));
            Assert.Equal(Edge.Wall, draft.At(line));

            // picking Window with timber chosen moves the style to one that has a window
            var editor = new MapEditor(new MapDraft(4, 4));
            Assert.True(editor.PickLine(Edge.Wall));
            Assert.True(editor.PickStyle(WallStyle.Timber));
            Assert.True(editor.PickLine(Edge.Window));
            Assert.True(editor.Style.HasWindow());
        }
    }
}
