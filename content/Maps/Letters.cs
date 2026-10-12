using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Maps
{
    // ONE LETTER PER VALUE, for a map file's compact layers (a square's ground, a line's wall style): the way the map
    // text itself is one glyph a square, so a layer lines up with the map under it in a text editor. The first value
    // is the default, which a layer may leave as a space
    public sealed class Letters<T> where T : struct, Enum
    {
        readonly (T Value, char Letter)[] _table;

        public Letters(params (T Value, char Letter)[] table) => _table = table;

        public T Default => _table[0].Value;

        public char Of(T value)
        {
            foreach ((T one, char c) in _table)
                if (one.Equals(value)) return c;

            return _table[0].Letter;
        }

        public bool TryRead(char letter, out T value)
        {
            if (letter == ' ')
            {
                value = Default;
                return true;
            }

            foreach ((T one, char c) in _table)
                if (c == letter)
                {
                    value = one;
                    return true;
                }

            value = Default;
            return false;
        }

        public IEnumerable<T> Values => _table.Select(e => e.Value);

        // "s stone, g grass": what a problem with the layer quotes back
        public string Legend => string.Join(", ", _table.Select(e => $"{e.Letter} {e.Value.ToString().ToLowerInvariant()}"));
    }
}
