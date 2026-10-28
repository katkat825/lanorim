using System.Linq;
using Content.Maps;
using Core.Space;

namespace Content.Tests
{
    // THE PROP IN HAND (cc_task_m2 Part 8): a click places one and selects it; Ctrl keeps placing more of the same;
    // Escape drops what's in hand, then closes what's open, then clears the selection, one step a press; and the
    // pickers' two shelves, buildings apart from props
    public class MapEditorHandTests
    {
        // a floor to place on (a new draft is rock until it's painted), the start in a corner
        internal static MapEditor Editor()
        {
            var editor = new MapEditor(new MapDraft(10, 8));
            editor.Drag(new Cell(0, 0), new Cell(9, 7));
            editor.Draft.PlaceStart(new Cell(0, 7));
            return editor;
        }

        internal static MapTarget At(int x, int y) => new MapTarget(new Cell(x, y), null);

        [Fact]
        public void AClickPlacesOneAndSelectsItAndTheHandIsEmpty()
        {
            MapEditor editor = Editor();
            Assert.True(editor.Choose("barrel"));
            Assert.True(editor.InHand);

            Assert.Equal(1, editor.Apply(At(3, 3), At(3, 3), more: false));

            Assert.Single(editor.Draft.Props);
            Assert.False(editor.InHand);
            Assert.Equal(MapTool.Select, editor.Tool);
            Assert.Same(editor.Draft.PropAt(new Cell(3, 3)), editor.Picked);

            // the next click picks, it doesn't place another
            editor.Apply(At(5, 5), At(5, 5), more: false);
            Assert.Single(editor.Draft.Props);
        }

        [Fact]
        public void CtrlClickKeepsPlacingMoreOfTheSame()
        {
            MapEditor editor = Editor();
            editor.Choose("barrel");

            Assert.Equal(1, editor.Apply(At(1, 1), At(1, 1), more: true));
            Assert.Equal(1, editor.Apply(At(3, 1), At(3, 1), more: true));
            Assert.True(editor.InHand);

            // and a plain click places the last and lets go
            Assert.Equal(1, editor.Apply(At(5, 1), At(5, 1), more: false));
            Assert.Equal(3, editor.Draft.Props.Count);
            Assert.False(editor.InHand);
            Assert.Equal(new Cell(5, 1), editor.Picked.Cell);
        }

        [Fact]
        public void AClickThatPlacesNothingKeepsTheHand()
        {
            MapEditor editor = Editor();
            editor.Choose("barrel");
            editor.Apply(At(3, 3), At(3, 3), more: true);

            // the square is taken: nothing placed, still in hand
            Assert.Equal(0, editor.Apply(At(3, 3), At(3, 3), more: false));
            Assert.True(editor.InHand);
        }

        [Fact]
        public void EscapeDropsTheHandThenClosesWhatsOpenThenClearsTheSelection()
        {
            MapEditor editor = Editor();
            editor.Choose("barrel");
            editor.Apply(At(2, 2), At(2, 2), more: true);

            // with a prop in hand, a picker open and a prop picked up... the hand first
            Assert.Equal(MapEscape.Dropped, editor.Escape(open: true));
            Assert.False(editor.InHand);
            Assert.Equal(MapTool.Select, editor.Tool);

            // then the picker: the screen closes it, and the editor changes nothing
            editor.Apply(At(2, 2), At(2, 2), more: false);
            Assert.NotNull(editor.Picked);
            Assert.Equal(MapEscape.Close, editor.Escape(open: true));
            Assert.NotNull(editor.Picked);

            // then the selection
            Assert.Equal(MapEscape.Cleared, editor.Escape(open: false));
            Assert.Null(editor.Chosen);

            Assert.Equal(MapEscape.Nothing, editor.Escape(open: false));
        }

        [Fact]
        public void EscapePutsTheWallOrDoorDownToo()
        {
            MapEditor editor = Editor();
            editor.Tool = MapTool.Wall;

            Assert.Equal(MapEscape.Dropped, editor.Escape(open: true));
            Assert.Equal(MapTool.Select, editor.Tool);
            Assert.Equal(MapEscape.Close, editor.Escape(open: true));
        }

        [Fact]
        public void ShiftDragIsStillTheScatterAndLeavesTheHandAlone()
        {
            MapEditor editor = Editor();
            editor.Choose("barrel");

            int stood = editor.Scatter(Enumerable.Range(0, 8).Select(x => new Cell(x, 4)).ToList(), 17);

            Assert.True(stood > 1);
            Assert.True(editor.InHand);
        }

        [Fact]
        public void BuildingsAreOneShelfAndPropsTheOther()
        {
            PropCatalogue palette = PropCatalogue.Srd();

            Assert.Equal(new[] { PropCatalogue.BuildingsCategory }, palette.CategoriesOn(buildings: true));
            Assert.DoesNotContain(PropCatalogue.BuildingsCategory, palette.CategoriesOn(buildings: false));

            // every category with something on offer is on one shelf; Walls & fences has nothing now (cc_task_m3 Part 6)
            Assert.Equal(palette.Categories.Count(c => palette.In(c).Any(p => !p.Retired)),
                         palette.CategoriesOn(true).Count + palette.CategoriesOn(false).Count + palette.CategoriesOn(PropShelf.People).Count);
            Assert.DoesNotContain("walls_fences", palette.CategoriesOn(false));

            var buildings = palette.Matching("", p => p.Id, null, buildings: true).ToList();
            Assert.NotEmpty(buildings);
            // bigger than a square, but for the farm's chicken coop and water tower (cc_task_m3 Part 5)
            Assert.All(buildings.Where(p => p.Id is not ("chicken_coop" or "water_tower")), p => Assert.True(p.Width * p.Depth > 1, p.Id));
            Assert.DoesNotContain(palette.Matching("", p => p.Id, null, buildings: false),
                                  p => p.Category == PropCatalogue.BuildingsCategory);
        }
    }
}
