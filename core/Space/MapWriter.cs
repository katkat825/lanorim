using System.Collections.Generic;
using System.Text;

namespace Core.Space
{
    // the other half of MapReader: a MapLayout back out as the same double-resolution text.
    // round-trips exactly, which is what lets the map builder save what it painted and the reader
    // read back the identical map - the test that pins that is the whole point of this file.
    public static class MapWriter
    {
        public static string Write(MapLayout map, IReadOnlyDictionary<int, Cell> spawns = null)
        {
            if (map == null) return "";

            spawns ??= map.Spawns;

            int width = map.Columns * 2 + 1;
            int height = map.Rows * 2 + 1;

            var text = new StringBuilder();

            for (int line = 0; line < height; line++)
            {
                for (int column = 0; column < width; column++)
                {
                    bool oddLine = line % 2 == 1;
                    bool oddColumn = column % 2 == 1;

                    int x = (column - 1) / 2;
                    int y = (line - 1) / 2;

                    if (oddLine && oddColumn)
                    {
                        text.Append(Square(map, spawns, new Cell(x, y)));
                        continue;
                    }

                    if (!oddLine && !oddColumn)
                    {
                        text.Append(MapReader.CornerGlyph);
                        continue;
                    }

                    // a line between two squares: vertical when the *column* is even
                    if (oddLine)
                    {
                        var border = new Border(new Cell(column / 2, y), true);

                        text.Append(Glyph(map.At(border), true));
                    }
                    else
                    {
                        var border = new Border(new Cell(x, line / 2), false);

                        text.Append(Glyph(map.At(border), false));
                    }
                }

                text.Append('\n');
            }

            return text.ToString();
        }

        static char Square(MapLayout map, IReadOnlyDictionary<int, Cell> spawns, Cell cell)
        {
            if (cell == map.Start) return MapReader.StartGlyph;

            foreach (KeyValuePair<int, Cell> spawn in spawns)
                if (spawn.Value == cell && spawn.Key >= 1 && spawn.Key <= 9)
                    return (char)('0' + spawn.Key);

            return map.At(cell) switch
            {
                Tile.Floor => '.',
                Tile.Rough => '~',
                _ => '#',
            };
        }

        static char Glyph(Edge edge, bool vertical) => edge switch
        {
            Edge.Wall => vertical ? '|' : '-',
            Edge.Door => 'x',
            _ => ' ',
        };
    }
}
