using System.Collections.Generic;

namespace Content.Schema
{
    // the shape every list reader has: one file's text in, what it read and what was wrong with it out.
    // ItemReader.TryRead, SpellReader.TryRead, MonsterReader.TryRead and the rest, so one loop can read
    // a folder of any of them (Srd.ReadAll)
    public delegate bool ListReader<T>(string text, out IReadOnlyList<T> read, out IReadOnlyList<string> problems);
}
