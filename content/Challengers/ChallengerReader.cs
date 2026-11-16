using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Tables;
using Core.Words;

namespace Content.Challengers
{
    // A CHALLENGERS FILE (cc_task_cg Part 1): { "challengers": [ { "id", "shape", "monsters", "names",
    // "descriptions", "map" } ] }, a campaign's challengers/ folder, written by tools/import_challengers.py from
    // Kathleen's vault. Read by the shared EntryList, the monsters by the encounter's own band reader (TableReader.Counted).
    // Whether each monster and map exists is the package's question, as for an encounter
    public static class ChallengerReader
    {
        public static readonly IReadOnlyList<string> Keys =
            new[] { "id", "shape", "monsters", "names", "descriptions", "map", "kind" };

        // a monster in a challenger's list: which, and how many (a team's counts are its draw's, so it says none)
        public static readonly IReadOnlyList<string> MonsterKeys = new[] { "monster", "count" };

        public static readonly EntryList<ChallengerType> Entries =
            new EntryList<ChallengerType>("challengers", "challenger", Keys, ReadOne);

        static ChallengerType ReadOne(JsonElement entry, string id, List<string> problems)
        {
            ChallengerShape shape = ChallengerShape.Single;

            if (!EnumWords.TryName(entry.Text("shape"), out shape))
                problems.Add($"{id}: '{entry.Text("shape")}' is not a shape - single, group or team");

            var monsters = TableReader.Counted(entry, "monsters", "monster", "a monster id", id, problems)
                                      .Select(m => new Band(m.Id, m.Count))
                                      .ToList();

            // no monsters: a shared list, names and descriptions any single (or any group and team) draws from
            if (monsters.Count == 0 && !entry.Has("monsters"))
            {
                if (shape == ChallengerShape.Group)
                    problems.Add($"{id}: a list for any stat block is a single's or a team's, not a group's");

                if (entry.Has("map")) problems.Add($"{id}: a list for any stat block has no map");
            }
            else if (monsters.Count == 0)
                problems.Add($"{id}: 'monsters' is empty - who steps into the arena (leave it out for a shared list)");

            // a single is one monster, or one of several ("a single thug or scout"), each one
            if (shape == ChallengerShape.Single && monsters.Any(b => b.Count.Maximum != 1 || b.Count.Minimum != 1))
                problems.Add($"{id}: a single is one monster at a time - say \"group\" for more");

            if (shape == ChallengerShape.Team && monsters.Any(b => b.Count.Maximum != 1 || b.Count.Minimum != 1))
                problems.Add($"{id}: a team's monsters are what it is drawn from, so they have no 'count'");

            int names = entry.Number("names", 0);
            int descriptions = entry.Number("descriptions", 0);

            // a type may have no list of its own (it takes the shared one); a shared list is nothing without one
            bool shared = monsters.Count == 0 && !entry.Has("monsters");

            if (names < 0 || shared && names < 1)
                problems.Add($"{id}: 'names' is how many numbered names it has" + (shared ? ", at least 1" : ", 0 or more"));
            if (descriptions < 0 || shared && descriptions < 1)
                problems.Add($"{id}: 'descriptions' is how many numbered descriptions it has" +
                             (shared ? ", at least 1" : ", 0 or more"));

            string map = entry.Text("map");

            if (map.Length > 0 && !Json.IsId(map)) problems.Add($"{id}: '{map}' is not a map id");

            // a champion of its stat blocks, which leaves them to the shared lists too (Kathleen, 2026-11-09)
            ChallengerKind kind = ChallengerKind.Own;

            if (entry.Has("kind") && !EnumWords.TryName(entry.Text("kind"), out kind))
                problems.Add($"{id}: '{entry.Text("kind")}' is not a kind - own or champion");

            if (kind != ChallengerKind.Own && monsters.Count == 0)
                problems.Add($"{id}: a list for any stat block is no champion");

            return new ChallengerType(id, shape, monsters, names, descriptions, map, kind);
        }
    }
}
