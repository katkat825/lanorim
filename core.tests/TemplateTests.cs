using System.Collections.Generic;
using System.Linq;
using Core.Combat;
using Core.Space;

namespace Core.Tests
{
    public class TemplateTests
    {
        static readonly Cell Origin = new Cell(10, 10);

        static HashSet<Cell> Set(IEnumerable<Cell> cells) => new HashSet<Cell>(cells);

        [Fact]
        public void ALineFacingEachWayRunsStraightOutFromTheSquareNextToYou()
        {
            Assert.Equal(Set(new[] { new Cell(10, 9), new Cell(10, 8), new Cell(10, 7) }),
                         Set(Template.Line(Origin, Facing.North, 3)));

            Assert.Equal(Set(new[] { new Cell(11, 10), new Cell(12, 10), new Cell(13, 10) }),
                         Set(Template.Line(Origin, Facing.East, 3)));

            Assert.Equal(Set(new[] { new Cell(10, 11), new Cell(10, 12), new Cell(10, 13) }),
                         Set(Template.Line(Origin, Facing.South, 3)));

            Assert.Equal(Set(new[] { new Cell(9, 10), new Cell(8, 10), new Cell(7, 10) }),
                         Set(Template.Line(Origin, Facing.West, 3)));
        }

        [Fact]
        public void TheOriginIsNeverInsideItsOwnShape()
        {
            foreach (Facing facing in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
            {
                Assert.DoesNotContain(Origin, Template.Line(Origin, facing, 20));
                Assert.DoesNotContain(Origin, Template.Cone(Origin, facing, 12));
                Assert.DoesNotContain(Origin, Template.Cube(Origin, facing, 3));
            }
        }

        [Fact]
        public void AWideLineIsThatManySquaresAcross()
        {
            List<Cell> line = Template.Line(Origin, Facing.East, 4, 3).ToList();

            Assert.Equal(12, line.Count);
            Assert.All(line, c => Assert.InRange(c.Y, 9, 11));
        }

        [Fact]
        public void AConeWidensAsItGoes()
        {
            // the k-th square out is k - 1/2 from the origin, and is as wide as it is far: one
            // square, one, three, three, five
            List<Cell> cone = Template.Cone(Origin, Facing.North, 5).ToList();

            int Row(int along) => cone.Count(c => c.Y == Origin.Y - along);

            Assert.Equal(1, Row(1));
            Assert.Equal(1, Row(2));
            Assert.Equal(3, Row(3));
            Assert.Equal(3, Row(4));
            Assert.Equal(5, Row(5));
        }

        [Fact]
        public void ASixtyFootConeCoversExactlyItsAreaInSquares()
        {
            // 60 x 60 / 2 square feet is 1800, which is 72 five-foot squares - Cone of Cold
            foreach (Facing facing in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
                Assert.Equal(72, Template.Cone(Origin, facing, 12).Distinct().Count());
        }

        [Fact]
        public void AConeFacingEachWayIsTheSameShapeTurned()
        {
            Assert.Equal(Set(new[]
                         {
                             new Cell(11, 10), new Cell(12, 10),
                             new Cell(13, 9), new Cell(13, 10), new Cell(13, 11),
                         }),
                         Set(Template.Cone(Origin, Facing.East, 3)));

            Assert.Equal(Set(new[]
                         {
                             new Cell(10, 11), new Cell(10, 12),
                             new Cell(9, 13), new Cell(10, 13), new Cell(11, 13),
                         }),
                         Set(Template.Cone(Origin, Facing.South, 3)));
        }

        [Fact]
        public void ACubeIsTheSquareInFrontOfYou()
        {
            // Thunderwave's 15-foot cube, thrown east: the three by three against your own square
            HashSet<Cell> cube = Set(Template.Cube(Origin, Facing.East, 3));

            Assert.Equal(9, cube.Count);
            Assert.All(cube, c => Assert.InRange(c.X, 11, 13));
            Assert.All(cube, c => Assert.InRange(c.Y, 9, 11));
        }

        [Fact]
        public void PointingAtASquareThrowsTowardItAlongTheLongerAxis()
        {
            Assert.Equal(Facing.East, Template.Toward(Origin, new Cell(15, 12)));
            Assert.Equal(Facing.West, Template.Toward(Origin, new Cell(2, 11)));
            Assert.Equal(Facing.North, Template.Toward(Origin, new Cell(11, 3)));
            Assert.Equal(Facing.South, Template.Toward(Origin, new Cell(8, 14)));

            // a dead diagonal has to go somewhere, and always the same somewhere
            Assert.Equal(Facing.South, Template.Toward(Origin, new Cell(13, 13)));
        }

        [Fact]
        public void OnTheBoardAShapeStopsAtTheEdgeOfTheMap()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Open));

            // from the left end of the middle row, a 20-square line east fits six squares
            List<Cell> line = field.Line(new Cell(0, 1), Facing.East, 20).ToList();

            Assert.Equal(6, line.Count);
            Assert.All(line, c => Assert.True(field.Map.Contains(c)));

            // and a cone thrown north from the top row covers nothing at all
            Assert.Empty(field.Cone(new Cell(3, 0), Facing.North, 12));
        }

        [Fact]
        public void OnTheBoardAWallStopsALine()
        {
            var field = new Battlefield(Rooms.Read(Rooms.Split));

            // the wall is between columns 2 and 3
            List<Cell> line = field.Line(new Cell(0, 1), Facing.East, 6).ToList();

            Assert.All(line, c => Assert.True(c.X <= 2));
        }

        [Fact]
        public void AFacingReadsBackFromItsId()
        {
            foreach (Facing facing in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
            {
                Assert.True(Template.TryParse(facing.Id(), out Facing back));
                Assert.Equal(facing, back);
            }

            Assert.False(Template.TryParse("up", out _));
        }
    }
}
