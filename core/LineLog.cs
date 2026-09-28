using System.Collections.Generic;

namespace Core
{
    // LINES, IN THE ORDER THEY HAPPENED, and all of them as text when asked: the fight's and the GM
    // screen's debug logs, and the log the player reads. each had its own list and its own join
    // (cc_task_dedupe-methods.md #8)
    public sealed class LineLog<T>
    {
        readonly List<T> _lines = new List<T>();

        public IReadOnlyList<T> Lines => _lines;

        public void Add(T line) => _lines.Add(line);

        public override string ToString() => string.Join("\n", _lines);
    }
}
