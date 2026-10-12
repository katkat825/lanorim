using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Space;

namespace Content.Maps
{
    // A MAP FILE'S LOOK, AS TEXT (cc_task_l Part 2.5), compact and lined up with the map so a text editor shows it:
    //   "ground" is a row of letters per row of squares (Grounds.Letter: s stone, g grass, ...);
    //   "walls" is the map's own double-resolution shape, a style letter (WallStyles.Letter) on each line that has a
    //   styled wall or door, a space on the rest, '.' on a square and '+' on a corner, so it sits over the map text
    //   square for square (and no line of it is blank, which a reader would skip).
    // Format 2 writes each only when the map uses it
    public static class MapLayers
    {
        public static string WriteGround(Extent extent, Ground[] ground)
        {
            var text = new StringBuilder();

            for (int y = 0; y < extent.Rows; y++)
            {
                for (int x = 0; x < extent.Columns; x++)
                    text.Append(Grounds.Letter.Of(extent.At(ground, new Cell(x, y))));

                text.Append('\n');
            }

            return text.ToString();
        }

        public static bool ReadGround(string text, Extent extent, Ground[] into, out string problem)
        {
            problem = null;

            string[] rows = Lines(text);

            if (rows.Length != extent.Rows)
            {
                problem = $"the ground has {rows.Length} rows and the map {extent.Rows} - one row of letters a row of squares";
                return false;
            }

            for (int y = 0; y < rows.Length; y++)
            {
                if (rows[y].Length != extent.Columns)
                {
                    problem = $"ground row {y + 1} is {rows[y].Length} letters and the map is {extent.Columns} squares across";
                    return false;
                }

                for (int x = 0; x < extent.Columns; x++)
                {
                    if (!Grounds.Letter.TryRead(rows[y][x], out Ground ground))
                    {
                        problem = $"ground row {y + 1} has '{rows[y][x]}', which is not a ground. they are: {Grounds.Letter.Legend}";
                        return false;
                    }

                    into[extent.Index(new Cell(x, y))] = ground;
                }
            }

            return true;
        }

        // the map text's shape: a corner '+', a line's letter or a space, a square's space
        public static string WriteStyles(Extent extent, Func<Border, WallStyle> style)
        {
            var text = new StringBuilder();

            for (int line = 0; line <= extent.Rows * 2; line++)
            {
                for (int column = 0; column <= extent.Columns * 2; column++)
                    text.Append(LineAt(line, column) is Border border
                        ? (style(border) == WallStyle.Stone ? ' ' : WallStyles.Letter.Of(style(border)))
                        : line % 2 == 0 ? '+' : '.');

                text.Append('\n');
            }

            return text.ToString();
        }

        public static bool ReadStyles(string text, Extent extent, out Dictionary<Border, WallStyle> styles, out string problem)
        {
            styles = new Dictionary<Border, WallStyle>();
            problem = null;

            string[] lines = Lines(text);

            if (lines.Length != extent.Rows * 2 + 1)
            {
                problem = $"the walls layer has {lines.Length} lines and the map {extent.Rows * 2 + 1} - it is the map's own shape";
                return false;
            }

            for (int line = 0; line < lines.Length; line++)
                for (int column = 0; column < lines[line].Length; column++)
                {
                    char letter = lines[line][column];

                    if (letter is ' ' or '+' or '.') continue;

                    if (LineAt(line, column) is not Border border || !extent.Contains(border))
                    {
                        problem = $"walls line {line + 1} column {column + 1} is '{letter}' where no wall can go";
                        return false;
                    }

                    if (!WallStyles.Letter.TryRead(letter, out WallStyle style))
                    {
                        problem = $"walls line {line + 1} column {column + 1} is '{letter}', which is not a wall style. " +
                                  $"they are: {WallStyles.Letter.Legend}";
                        return false;
                    }

                    if (style != WallStyle.Stone) styles[border] = style;
                }

            return true;
        }

        // the line at a place in the double-resolution text, or null for a square or a corner (MapReader's rule)
        static Border? LineAt(int line, int column)
        {
            bool oddLine = line % 2 == 1, oddColumn = column % 2 == 1;

            if (oddLine == oddColumn) return null;

            return oddLine
                ? new Border(new Cell(column / 2, (line - 1) / 2), true)
                : new Border(new Cell((column - 1) / 2, line / 2), false);
        }

        // the layer's lines, blank ones (the leading newline a JSON string often has) left out
        static string[] Lines(string text) =>
            (text ?? "").Replace("\r\n", "\n").Split('\n').Where(l => l.Trim().Length > 0).ToArray();
    }
}
