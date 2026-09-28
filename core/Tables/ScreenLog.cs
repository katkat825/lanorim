using System.Collections.Generic;

namespace Core.Tables
{
    // every roll and every result, as debug text. never shown to a player, so it is not localized,
    // and it does show hidden numbers - that is what it is for
    public sealed class ScreenLog : ScreenObserver
    {
        readonly LineLog<string> _log = new LineLog<string>();

        public IReadOnlyList<string> Lines => _log.Lines;

        public override void Rolled(GmRoll roll) => _log.Add(roll.ToString());

        public override void Consulted(TableRoll result) => _log.Add("== " + result);

        public override void Opened(LootRoll result) => _log.Add("== " + result);

        public override string ToString() => _log.ToString();
    }
}
