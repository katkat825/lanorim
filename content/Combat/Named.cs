using Core.Characters;

namespace Content.Combat
{
    // A NAME IN A LOG LINE THAT MAY NOT HAVE WORDS: a weapon's or a spell's key, and the creature to
    // name instead when the locale has none for it. A statblock's own attacks (a rat's bite) mostly
    // have no name of their own, so "Rat bite hits" falls back to "Giant Rat hits"
    public sealed class Named
    {
        public Named(string key, Actor or)
        {
            Key = key ?? "";
            Or = or;
        }

        public string Key { get; }

        public Actor Or { get; }

        public override string ToString() => Key;
    }
}
