using System.Collections.Generic;

namespace Core.Tables
{
    // every roll and every result, as debug text. never shown to a player, so it is not localized,
    // and it does show hidden numbers - that is what it is for
    public sealed class ScreenLog : ScreenObserver
    {
        readonly List<string> _lines = new List<string>();

        public IReadOnlyList<string> Lines => _lines;

        public override void Rolled(GmRoll roll) => _lines.Add(roll.ToString());

        public override void Consulted(TableRoll result) => _lines.Add("== " + result);

        public override void Opened(LootRoll result) => _lines.Add("== " + result);

        public override string ToString() => string.Join("\n", _lines);
    }
}
