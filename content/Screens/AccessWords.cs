using System.Collections.Generic;

namespace Content.Screens
{
    // WHAT THE KEYBOARD'S OWN KEYS SAY (cc_task_open-questions-answers.md 4.2): F3 turning the reader on and off, and
    // what Tab reaches on the table - a piece, a button on the bar, the dice tray. Whole sentences, each its own key
    public static class AccessWords
    {
        static string K(string what) => ScreenKeys.Key("access", what);

        public static readonly string ReadingOnKey = K("reading_on");
        public static readonly string ReadingOffKey = K("reading_off");

        // "Giant Rat: 7 of 7 hit points, 3 squares away."
        public static readonly string PieceKey = K("piece");

        // "Giant Rat: 7 of 7 hit points, beside you."
        public static readonly string BesideKey = K("piece_beside");

        // "You: 12 of 12 hit points."
        public static readonly string YouKey = K("you");

        // "Greatsword, on the bar."
        public static readonly string ButtonKey = K("button");

        // "The dice tray: 14, 3."
        public static readonly string TrayKey = K("tray");

        public static readonly string TrayEmptyKey = K("tray_empty");

        public static readonly string NothingKey = K("nothing_to_reach");

        public static IEnumerable<string> Keys() =>
            new[] { ReadingOnKey, ReadingOffKey, PieceKey, BesideKey, YouKey, ButtonKey, TrayKey, TrayEmptyKey, NothingKey };
    }
}
