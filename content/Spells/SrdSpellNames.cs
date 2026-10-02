using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;

namespace Content.Spells
{
    // every spell name the SRD prints. a name on this list is a promise about what the spell does
    // (decisions_checklist.md section 1, the hard rule): a spell flagged as an approximation may
    // never be shown under one of them, and a test holds the locale to that. beside each name, the
    // classes the SRD prints under it ("lists", written by tools/srd_spell_lists.py), so a test can
    // hold the v1 class lists to the SRD's (cc_task_f 1.2)
    public static class SrdSpellNames
    {
        static IReadOnlyCollection<string> _all;
        static IReadOnlyDictionary<string, IReadOnlyList<string>> _lists;

        public static IReadOnlyCollection<string> All => _all ??= Load(root =>
            new HashSet<string>(root.Strings("names").Select(Fold), StringComparer.Ordinal));

        // the SRD spell whose id this is, by the ids' own spelling ("Hunter's Mark" is hunters_mark), and the
        // SRD classes it is on ("wizard", "bard"...). null for a spell that isn't the SRD's
        public static IReadOnlyList<string> ClassesOf(string spellId) =>
            spellId != null && Lists.TryGetValue(spellId, out IReadOnlyList<string> classes) ? classes : null;

        static IReadOnlyDictionary<string, IReadOnlyList<string>> Lists => _lists ??= Load(root =>
            root.GetProperty("lists").EnumerateObject()
                .ToDictionary(spell => IdOf(spell.Name),
                              spell => (IReadOnlyList<string>)spell.Value.EnumerateArray()
                                                                     .Select(c => c.GetString()).ToArray(),
                              StringComparer.Ordinal));

        // case and spacing are not what makes two names the same spell to a player
        public static bool IsSrd(string name) =>
            !string.IsNullOrWhiteSpace(name) && All.Contains(Fold(name));

        static T Load<T>(Func<JsonElement, T> read)
        {
            if (!Json.TryParse(Srd.Read("reference/spell_names.json"), out JsonDocument document, out string problem))
                throw new InvalidOperationException("the SRD spell name list does not parse: " + problem);

            using (document)
                return read(document.RootElement);
        }

        static string Fold(string name) =>
            string.Join(" ", name.Trim().ToLowerInvariant()
                                 .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // "Hunter's Mark" -> hunters_mark. a spell whose id isn't spelled from its SRD name isn't found, and
        // SpellClassListTests says so
        static string IdOf(string name) =>
            string.Join("_", new string(Fold(name).Where(c => c != '\'')
                                                  .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
                             .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
