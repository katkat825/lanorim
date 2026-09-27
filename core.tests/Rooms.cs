using Core.Space;

namespace Core.Tests
{
    // a 7 x 3 room with nothing in it but floor, and one with a wall down the middle
    static class Rooms
    {
        public const string Open = @"
+-+-+-+-+-+-+-+
|@ . . . . . .|
+ + + + + + + +
|. . . . . . .|
+ + + + + + + +
|. . . . . . .|
+-+-+-+-+-+-+-+";

        public const string Split = @"
+-+-+-+-+-+-+-+
|@ . .|. . . .|
+ + + + + + + +
|. . .|. . . .|
+ + + + + + + +
|. . .|. . . .|
+-+-+-+-+-+-+-+";

        public static MapLayout Read(string text)
        {
            Assert.True(MapReader.TryRead(text, out MapLayout map, out string problem), problem);
            return map;
        }
    }
}
