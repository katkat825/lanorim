using System.Linq;
using Content.Maps;
using Core.Combat;
using Core.Space;

namespace Content.Tests
{
    // cc_task_ui-issues-9-30.md 1.1: a prop whose palette entry says "blocks" fills its square. The
    // rules' map (MapDraft.Layout) keeps the tile under it and marks the square impassable, so routes,
    // the reachable squares and a walk all go round it. It doesn't block sight
    public class BlockingPropTests
    {
        // a 5 x 3 room, all floor, the hero starting at the west end of the middle row
        static MapDraft Room()
        {
            var draft = new MapDraft(5, 3);
            draft.Paint(new Cell(0, 0), new Cell(4, 2), Tile.Floor);
            draft.PlaceStart(new Cell(0, 1));
            return draft;
        }

        [Fact]
        public void ARouteGoesRoundACrate()
        {
            MapDraft draft = Room();
            Assert.True(draft.PlaceProp("crate", new Cell(2, 1)));

            MapLayout map = draft.Layout();

            Assert.Equal(Tile.Floor, map.At(new Cell(2, 1)));
            Assert.False(map.IsPassable(new Cell(2, 1)));
            Assert.True(map.IsTransparent(new Cell(2, 1)));

            var route = Route.Between(map, new Cell(0, 1), new Cell(4, 1), _ => false);

            Assert.NotNull(route);
            Assert.DoesNotContain(new Cell(2, 1), route);
            Assert.Equal(5, route.Count);
        }

        [Fact]
        public void AStoolDoesNotBlock()
        {
            MapDraft draft = Room();
            Assert.True(draft.PlaceProp("stool", new Cell(2, 1)));

            MapLayout map = draft.Layout();

            Assert.True(map.IsPassable(new Cell(2, 1)));
            Assert.NotNull(Route.Between(map, new Cell(0, 1), new Cell(2, 1), _ => false));
        }

        [Fact]
        public void TheReachableSquaresAndAWalkLeaveTheCrateOut()
        {
            MapDraft draft = Room();
            draft.PlaceProp("crate", new Cell(1, 1));

            var field = new Battlefield(draft.Layout());
            var hero = new Core.Characters.Actor("hero", side: Core.Characters.Allegiance.Hero);
            Assert.True(field.Place(hero, new Cell(0, 1)));

            Assert.False(field.Place(new Core.Characters.Actor("rat"), new Cell(1, 1)));

            var reach = field.Reachable(hero, 6);
            Assert.DoesNotContain(new Cell(1, 1), reach.Keys);
            Assert.Contains(new Cell(2, 1), reach.Keys);
        }

        [Fact]
        public void NobodyStartsOrSpawnsInsideACrate()
        {
            MapDraft draft = Room();
            draft.PlaceProp("crate", new Cell(3, 1));

            Assert.False(draft.PlaceStart(new Cell(3, 1)));
            Assert.False(draft.PlaceSpawn(1, new Cell(3, 1)));

            // and the other way round: no crate on a spawn or on the start
            Assert.True(draft.PlaceSpawn(2, new Cell(4, 2)));
            Assert.False(draft.PlaceProp("crate", new Cell(4, 2)));
            Assert.False(draft.PlaceProp("crate", draft.Start));

            // a chair can share a square with a monster
            Assert.True(draft.PlaceProp("chair", new Cell(4, 2)));
        }

        // a hand-written map: a crate on spawn 1 is unsound, and so is a barrel in the only corridor to it
        [Theory]
        [InlineData("crate", 2, "spawn 1 is inside a prop")]
        [InlineData("barrel", 1, "cannot be walked to")]
        public void AHandWrittenMapWithABlockingPropInTheWayIsUnsound(string prop, int x, string problem)
        {
            string text = @"{ ""format"": 1, ""columns"": 3, ""rows"": 1,
                ""map"": ""\n+-+-+-+\n|@ . 1|\n+-+-+-+"",
                ""props"": [ { ""id"": """ + prop + @""", ""x"": " + x + @", ""y"": 0 } ] }";

            Assert.True(MapDraft.TryRead(text, out MapDraft draft, out string read), read);
            Assert.Contains(draft.Problems(), p => p.Contains(problem));
        }

        [Fact]
        public void TheSampleCampaignsPropsBlockAndItsMapsStaySound()
        {
            var pack = Campaigns.Package.Read(SampleCampaignTests.Folder);

            MapLayout cellar = pack.Maps["mill_cellar"];
            Assert.Contains(new Cell(1, 4), cellar.Blocked);   // the barrel
            Assert.Contains(new Cell(5, 0), cellar.Blocked);   // the crate

            MapLayout yard = pack.Maps["mill_yard"];
            Assert.Contains(new Cell(7, 5), yard.Blocked);     // the cauldron
            Assert.Contains(new Cell(3, 6), yard.Blocked);     // the crate

            Assert.Empty(pack.Faults);
        }
    }
}
