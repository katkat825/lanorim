using System.Linq;
using Content.Maps;
using Core.Space;

namespace Content.Tests
{
    // cc_task_l Part 4: fill; select, copy, paste and move, each one undo step; a placed prop picked up, turned and
    // deleted; a scatter of trees
    public class MapEditingTests
    {
        static MapEditor Editor(int columns = 10, int rows = 8)
        {
            MapEditor editor = Content.Screens.MapBuilderView.Fresh(columns, rows, "empty_field");
            editor.Draft.PlaceStart(new Cell(0, 0));
            return editor;
        }

        [Fact]
        public void FillTakesTheJoinedSquaresOfOneGroundUpToTheWalls()
        {
            MapEditor editor = Editor(6, 3);
            MapDraft draft = editor.Draft;

            // a fence across the middle and a cobbled square
            draft.Wall(Enumerable.Range(0, 3).Select(y => new Border(new Cell(3, y), true)), Edge.Wall, WallStyle.Fence);
            draft.Paint(new Cell(1, 1), Ground.Cobble);

            editor.Tool = MapTool.Fill;
            editor.Brush = Brush.Of(Ground.Dirt);

            var preview = editor.Preview(editor.Aim(0.5, 0.5), editor.Aim(0.5, 0.5));
            Assert.Equal(8, preview.Squares.Count);   // the west half, less the cobbles

            Assert.True(editor.Click(new Cell(0, 0)));
            Assert.Equal(Ground.Dirt, draft.GroundAt(new Cell(2, 2)));
            Assert.Equal(Ground.Cobble, draft.GroundAt(new Cell(1, 1)));
            Assert.Equal(Ground.Grass, draft.GroundAt(new Cell(4, 0)));

            Assert.True(draft.Undo());
            Assert.Equal(Ground.Grass, draft.GroundAt(new Cell(2, 2)));
        }

        [Fact]
        public void ACopyPastesItsGroundWallsPropsAndMarkersAsOneUndoStep()
        {
            MapEditor editor = Editor();
            MapDraft draft = editor.Draft;

            draft.Paint(new Cell(1, 1), new Cell(2, 2), Brush.Of(Ground.Planks));
            draft.Wall(new Border(new Cell(1, 1), false), Edge.Wall, WallStyle.Timber);
            draft.PlaceProp("barrel", new Cell(2, 2));
            draft.PlaceSpawn(3, new Cell(1, 2));

            editor.Tool = MapTool.Select;
            Assert.Equal(0, editor.Apply(editor.Aim(1.5, 1.5), editor.Aim(2.5, 2.5)));
            Assert.Equal(CellBox.Between(new Cell(1, 1), new Cell(2, 2)), editor.Selection);

            Assert.True(editor.Copy());
            Assert.True(editor.Paste(new Cell(6, 4)));

            Assert.Equal(Ground.Planks, draft.GroundAt(new Cell(7, 5)));
            Assert.Equal(WallStyle.Timber, draft.StyleAt(new Border(new Cell(6, 4), false)));
            Assert.Contains(draft.Props, p => p.Id == "barrel" && p.Cell == new Cell(7, 5));
            Assert.Equal(new Cell(6, 5), draft.Spawns[3]);

            // one undo takes the whole paste back
            Assert.True(draft.Undo());
            Assert.Equal(Ground.Grass, draft.GroundAt(new Cell(7, 5)));
            Assert.DoesNotContain(draft.Props, p => p.Cell == new Cell(7, 5));
            Assert.Equal(new Cell(1, 2), draft.Spawns[3]);
        }

        [Fact]
        public void ADragFromInsideTheSelectionMovesItAndLeavesTheGroundAroundIt()
        {
            MapEditor editor = Editor();
            MapDraft draft = editor.Draft;

            draft.PlaceProp("cottage", new Cell(2, 2));
            draft.Paint(new Cell(2, 2), new Cell(3, 3), Brush.Of(Ground.Cobble));

            editor.Tool = MapTool.Select;
            editor.Apply(editor.Aim(2.5, 2.5), editor.Aim(3.5, 3.5));

            Assert.True(editor.Apply(editor.Aim(2.5, 2.5), editor.Aim(5.5, 3.5)) > 0);

            Assert.Contains(draft.Props, p => p.Id == "cottage" && p.Cell == new Cell(5, 3));
            Assert.Equal(Ground.Cobble, draft.GroundAt(new Cell(6, 4)));

            // where it was is the grass around it
            Assert.Equal(Ground.Grass, draft.GroundAt(new Cell(2, 2)));
            Assert.False(draft.IsBlocked(new Cell(2, 2)));
            Assert.Equal(CellBox.Between(new Cell(5, 3), new Cell(6, 4)), editor.Selection);

            Assert.True(draft.Undo());
            Assert.Contains(draft.Props, p => p.Id == "cottage" && p.Cell == new Cell(2, 2));
            Assert.Equal(Ground.Cobble, draft.GroundAt(new Cell(2, 2)));
        }

        [Fact]
        public void DeleteClearsTheSelectionAsOneStepAndNeverTheStart()
        {
            MapEditor editor = Editor();
            MapDraft draft = editor.Draft;

            draft.PlaceProp("oak_1", new Cell(1, 1));
            draft.PlaceProp("barrel", new Cell(2, 1));

            editor.Tool = MapTool.Select;
            editor.Apply(editor.Aim(0.5, 0.5), editor.Aim(3.5, 2.5));

            Assert.True(editor.Delete());
            Assert.Empty(draft.Props);
            Assert.Equal(new Cell(0, 0), draft.Start);
            Assert.True(draft.At(draft.Start).IsPassable());

            Assert.True(draft.Undo());
            Assert.Equal(2, draft.Props.Count);
        }

        [Fact]
        public void AClickedPropIsPickedUpThenTurnedMovedAndDeleted()
        {
            MapEditor editor = Editor();
            MapDraft draft = editor.Draft;
            draft.PlaceProp("townhouse", new Cell(2, 2));

            editor.Tool = MapTool.Select;
            editor.Apply(editor.Aim(4.5, 3.5), editor.Aim(4.5, 3.5));
            Assert.Equal("townhouse", editor.Picked?.Id);

            // the hover over it shows its footprint
            Assert.Equal(6, editor.Preview(editor.Aim(4.5, 3.5), editor.Aim(4.5, 3.5)).Squares.Count);

            Assert.Equal(1, editor.Rotate(1));
            Assert.Equal(1, draft.Props.Single().Turn);

            Assert.Equal(1, editor.Apply(editor.Aim(2.5, 2.5), editor.Aim(5.5, 2.5)));
            Assert.Equal(new Cell(5, 2), draft.Props.Single().Cell);

            Assert.True(editor.Delete());
            Assert.Empty(draft.Props);
            Assert.Null(editor.Picked);
        }

        [Fact]
        public void AScatterDropsThePropEveryFewSquaresAtTurnsOfItsOwnAsOneStep()
        {
            MapEditor editor = Editor(12, 4);
            editor.Choose("pine_1");

            var path = Enumerable.Range(0, 12).Select(x => new Cell(x, 2)).ToList();

            int stood = editor.Scatter(path, seed: 7);

            Assert.Equal(6, stood);
            Assert.Equal(6, editor.Draft.Props.Count);
            Assert.True(editor.Draft.Props.Select(p => p.Turn).Distinct().Count() > 1);

            // the same stroke, the same wood
            MapEditor again = Editor(12, 4);
            again.Choose("pine_1");
            again.Scatter(path, seed: 7);
            Assert.Equal(editor.Draft.Save(), again.Draft.Save());

            Assert.True(editor.Draft.Undo());
            Assert.Empty(editor.Draft.Props);
        }

        [Fact]
        public void TheEraserTakesABuildingFromAnyOfItsSquares()
        {
            MapEditor editor = Editor();
            editor.Draft.PlaceProp("barn", new Cell(2, 2));

            editor.Tool = MapTool.Erase;
            Assert.True(editor.Click(new Cell(5, 5)));
            Assert.Empty(editor.Draft.Props);
        }
    }
}
