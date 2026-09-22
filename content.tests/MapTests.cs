using System.Linq;
using Content.Maps;
using Core.Space;

namespace Content.Tests
{
    public class MapWriterTests
    {
        const string Room = @"
+-+-+-+-+-+
|@ . .|~ 1|
+ + +x+ + +
|. . .|. .|
+-+-+-+-+-+";

        [Fact]
        public void AMapRoundTripsThroughItsOwnText()
        {
            Assert.True(MapReader.TryRead(Room, out MapLayout map, out string problem), problem);

            string written = MapWriter.Write(map);

            Assert.True(MapReader.TryRead(written, out MapLayout again, out string second), second);

            Assert.Equal(map.Columns, again.Columns);
            Assert.Equal(map.Rows, again.Rows);
            Assert.Equal(map.Start, again.Start);

            foreach (Cell cell in map.Cells) Assert.Equal(map.At(cell), again.At(cell));

            foreach (Border border in map.Borders) Assert.Equal(map.At(border), again.At(border));

            Assert.Equal(map.Spawns.Count, again.Spawns.Count);

            foreach (var spawn in map.Spawns) Assert.Equal(spawn.Value, again.Spawns[spawn.Key]);
        }

        [Fact]
        public void TheWrittenTextIsWhatTheReaderWantsToSee()
        {
            MapReader.TryRead(Room, out MapLayout map, out _);

            string[] lines = MapWriter.Write(map).Split('\n').Where(l => l.Length > 0).ToArray();

            // odd count, odd width: the double-resolution shape MapReader insists on
            Assert.Equal(5, lines.Length);
            Assert.Equal(11, lines[0].Length);
            Assert.All(lines, l => Assert.Equal(11, l.Length));
        }
    }

    public class MapDraftTests
    {
        static MapDraft Room(int columns = 6, int rows = 4)
        {
            var draft = new MapDraft(columns, rows);

            draft.Paint(new Cell(0, 0), new Cell(columns - 1, rows - 1), Tile.Floor);
            draft.Enclose();
            draft.PlaceStart(new Cell(0, 0));

            return draft;
        }

        [Fact]
        public void AFreshDraftIsSolidRock()
        {
            var draft = new MapDraft(4, 4);

            Assert.All(draft.Layout().Cells, c => Assert.Equal(Tile.Void, draft.At(c)));
            Assert.False(draft.Sound);
        }

        [Fact]
        public void PaintingADragChangesEverySquareInTheBox()
        {
            var draft = new MapDraft(4, 4);

            Assert.Equal(4, draft.Paint(new Cell(0, 0), new Cell(1, 1), Tile.Floor));
            Assert.Equal(Tile.Floor, draft.At(new Cell(1, 1)));
            Assert.Equal(Tile.Void, draft.At(new Cell(2, 2)));
        }

        [Fact]
        public void PaintingWhatIsAlreadyThereIsNotAnEdit()
        {
            var draft = new MapDraft(4, 4);

            draft.Paint(new Cell(0, 0), Tile.Floor);

            Assert.False(draft.Paint(new Cell(0, 0), Tile.Floor));
        }

        [Fact]
        public void EnclosingPutsAWallAllTheWayRound()
        {
            var draft = new MapDraft(3, 3);

            draft.Paint(new Cell(0, 0), new Cell(2, 2), Tile.Floor);
            draft.Enclose();

            MapLayout map = draft.Layout();

            Assert.Equal(Edge.Wall, map.At(Border.West(new Cell(0, 1))));
            Assert.Equal(Edge.Wall, map.At(Border.East(new Cell(2, 1))));
            Assert.Equal(Edge.Wall, map.At(Border.North(new Cell(1, 0))));
            Assert.Equal(Edge.Wall, map.At(Border.South(new Cell(1, 2))));

            // and nothing in the middle
            Assert.Equal(Edge.None, map.Between(new Cell(1, 1), new Cell(1, 2)));
        }

        [Fact]
        public void AWallBetweenTwoSquaresStopsTheRoute()
        {
            MapDraft draft = Room();

            Assert.True(draft.Wall(new Cell(1, 0), new Cell(2, 0), Edge.Wall));

            MapLayout map = draft.Layout();

            Assert.False(map.CanCross(new Cell(1, 0), new Cell(2, 0)));
        }

        [Fact]
        public void ASpawnMustStandOnSomethingAndOnItsOwn()
        {
            MapDraft draft = Room();

            Assert.True(draft.PlaceSpawn(1, new Cell(3, 1)));

            // the same square again for a different monster
            Assert.False(draft.PlaceSpawn(2, new Cell(3, 1)));

            // and off the floor
            draft.Paint(new Cell(5, 3), Tile.Void);
            Assert.False(draft.PlaceSpawn(2, new Cell(5, 3)));
        }

        [Fact]
        public void ASpawnSlotOutsideOneToNineIsRefused()
        {
            MapDraft draft = Room();

            Assert.False(draft.PlaceSpawn(0, new Cell(2, 2)));
            Assert.False(draft.PlaceSpawn(10, new Cell(2, 2)));
        }

        [Fact]
        public void APropNeedsSomewhereToStand()
        {
            MapDraft draft = Room();

            Assert.True(draft.PlaceProp("barrel", new Cell(2, 2)));

            draft.Paint(new Cell(4, 3), Tile.Void);

            Assert.False(draft.PlaceProp("barrel", new Cell(4, 3)));
        }

        [Fact]
        public void APropIdHasToSurviveBeingAKey() =>
            Assert.False(Room().PlaceProp("Big Barrel", new Cell(1, 1)));

        [Fact]
        public void UndoPutsItBack()
        {
            MapDraft draft = Room();

            draft.Paint(new Cell(2, 2), Tile.Rough);

            Assert.Equal(Tile.Rough, draft.At(new Cell(2, 2)));
            Assert.True(draft.CanUndo);

            draft.Undo();

            Assert.Equal(Tile.Floor, draft.At(new Cell(2, 2)));
        }

        [Fact]
        public void UndoTakesAWholeDragBackInOneStep()
        {
            var draft = new MapDraft(6, 6);

            draft.Paint(new Cell(0, 0), new Cell(5, 5), Tile.Floor);
            draft.Undo();

            Assert.All(draft.Layout().Cells, c => Assert.Equal(Tile.Void, draft.At(c)));
        }

        [Fact]
        public void RedoPutsItBackAgain()
        {
            MapDraft draft = Room();

            draft.PlaceProp("brazier", new Cell(3, 3));
            draft.Undo();

            Assert.Empty(draft.Props);
            Assert.True(draft.CanRedo);

            draft.Redo();

            Assert.Single(draft.Props);
        }

        [Fact]
        public void AnEditAfterAnUndoLosesTheRedo()
        {
            MapDraft draft = Room();

            draft.Paint(new Cell(1, 1), Tile.Rough);
            draft.Undo();

            Assert.True(draft.CanRedo);

            draft.Paint(new Cell(2, 2), Tile.Rough);

            Assert.False(draft.CanRedo);
        }

        [Fact]
        public void ADraftSavesAndLoadsAsTheSameMap()
        {
            MapDraft draft = Room();

            draft.PlaceSpawn(1, new Cell(4, 1));
            draft.PlaceSpawn(2, new Cell(5, 2));
            draft.Paint(new Cell(2, 2), Tile.Rough);
            draft.Wall(new Cell(1, 1), new Cell(2, 1), Edge.Door);
            draft.PlaceProp("barrel", new Cell(3, 3), 2);

            string saved = draft.Save();

            Assert.True(MapDraft.TryRead(saved, out MapDraft read, out string problem), problem);

            Assert.Equal(draft.Columns, read.Columns);
            Assert.Equal(draft.Rows, read.Rows);
            Assert.Equal(draft.Start, read.Start);
            Assert.Equal(Tile.Rough, read.At(new Cell(2, 2)));
            Assert.Equal(Edge.Door, read.Layout().Between(new Cell(1, 1), new Cell(2, 1)));
            Assert.Equal(2, read.Spawns.Count);
            Assert.Equal(new Cell(4, 1), read.Spawns[1]);

            Prop prop = Assert.Single(read.Props);
            Assert.Equal("barrel", prop.Id);
            Assert.Equal(new Cell(3, 3), prop.Cell);
            Assert.Equal(2, prop.Turn);

            // and saving the read-back draft gives the identical bytes
            Assert.Equal(saved, read.Save());
        }

        [Fact]
        public void AReadDraftHasNothingToUndo()
        {
            MapDraft draft = Room();

            MapDraft.TryRead(draft.Save(), out MapDraft read, out _);

            Assert.False(read.CanUndo);
        }

        [Fact]
        public void ASoundMapHasNoProblems()
        {
            MapDraft draft = Room();

            draft.PlaceSpawn(1, new Cell(4, 2));

            Assert.True(draft.Sound, string.Join("; ", draft.Problems()));
        }

        [Fact]
        public void ASpawnWalledOffFromTheStartIsCaught()
        {
            MapDraft draft = Room();

            draft.PlaceSpawn(1, new Cell(5, 0));

            // wall the far column off completely
            for (int y = 0; y < 4; y++)
                draft.Wall(new Border(new Cell(5, y), true), Edge.Wall);

            Assert.Contains(draft.Problems(), p => p.Contains("cannot be walked to"));
        }

        [Fact]
        public void AStartOnRockIsCaught()
        {
            MapDraft draft = Room();

            draft.PlaceStart(new Cell(2, 2));
            draft.Paint(new Cell(2, 2), Tile.Void);

            Assert.Contains(draft.Problems(), p => p.Contains("solid rock"));
        }

        [Fact]
        public void AnEmptyMapSaysSo() =>
            Assert.Contains(new MapDraft(4, 4).Problems(), p => p.Contains("nothing has been painted"));

        [Fact]
        public void ADraftIsPlayableAsABattlefield()
        {
            MapDraft draft = Room();

            draft.PlaceSpawn(1, new Cell(5, 3));

            var field = new Core.Combat.Battlefield(draft.Layout());

            Assert.Equal(6, field.Columns);
            Assert.NotNull(field.Map.SpawnAt(1));
            Assert.Equal(new Cell(0, 0), field.Map.SpawnAt(MapLayout.HeroSlot));
        }
    }
}
