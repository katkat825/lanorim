using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Content.Workshop.Tables
{
    // WHERE A TABLE IS USED (cc_task_n Part 6): "Used in: 3 story cards, 1 fight". The story's cards that call it
    // (<<fight id>>, <<encounter id>>, <<loot id>>, <<shop id>>, <<armory id>>), the fights that leave a loot table
    // behind, the loot tables that roll another. Delete asks first when anything does
    public sealed class WhereUsed
    {
        // node title -> its body, as the campaign's .yarn files say (the story editor fills this from its cards)
        readonly IReadOnlyDictionary<string, string> _story;
        readonly TablesDraft _tables;

        public WhereUsed(TablesDraft tables, IReadOnlyDictionary<string, string> story)
        {
            _tables = tables;
            _story = story ?? new Dictionary<string, string>();
        }

        // the story as the draft holds it, card by card: what each would write
        public static IReadOnlyDictionary<string, string> Story(Story.StoryDraft story) =>
            story.Cards.GroupBy(c => c.Title).ToDictionary(g => g.Key, g => string.Concat(g.Select(c => c.Write("\n"))), StringComparer.Ordinal);

        // the cards whose story calls <<verb id>> (or <<fight {$encounter}>> after <<encounter id>>: that's the table's)
        public IReadOnlyList<string> Cards(string verb, string id)
        {
            var call = new Regex($@"<<\s*{verb}\s+{Regex.Escape(id)}\b");

            return _story.Where(n => call.IsMatch(n.Value)).Select(n => n.Key).OrderBy(n => n, StringComparer.Ordinal).ToList();
        }

        public Use OfFight(string id) => new(Cards("fight", id), Array.Empty<string>(), Array.Empty<string>());

        public Use OfEncounterTable(string id) => new(Cards("encounter", id).Concat(Cards("fight", id)).Distinct().ToList(),
                                                     Array.Empty<string>(), Array.Empty<string>());

        public Use OfLoot(string id) =>
            new(Cards("loot", id),
                _tables.Fights.Where(f => f.Fight.Loot == id).Select(f => f.Fight.Id).ToList(),
                _tables.Loot.Where(t => t.Id != id && t.Entries.Any(e => e.Table == id)).Select(t => t.Id).ToList());

        public Use OfShop(string id) => new(Cards("shop", id).Concat(Cards("armory", id)).Distinct().ToList(),
                                           Array.Empty<string>(), Array.Empty<string>());

        public sealed record Use(IReadOnlyList<string> Cards, IReadOnlyList<string> Fights, IReadOnlyList<string> Tables)
        {
            public bool Any => Cards.Count + Fights.Count + Tables.Count > 0;
        }
    }
}
