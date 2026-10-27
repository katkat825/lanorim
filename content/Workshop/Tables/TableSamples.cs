using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Content.Encounters;
using Content.Loot;
using Core.Dice;
using Core.Tables;

namespace Content.Workshop.Tables
{
    // ROLL IT (cc_task_n Parts 2 and 3): a table thrown twenty times by the GM's own screen, for a feel of the odds. The
    // draft's JSON goes through the game's own readers first, so what's rolled is what the game would roll; null when
    // the table doesn't read yet (the problems panel says why)
    public static class TableSamples
    {
        public const int Times = 20;

        // what came up, most often first: an entry's id, or "" for nothing happening (the trigger didn't fire)
        public static IReadOnlyList<(string Entry, int Count)> Encounters(EncounterTableDraft table, int seed)
        {
            string text = new JsonObject { ["tables"] = new JsonArray(table.ToJson()) }.ToJsonString();

            if (!EncounterReader.TryRead(text, out IReadOnlyList<EncounterTable> read, out _) || read.Count != 1) return null;

            var screen = new GmScreen(new SeededRng(seed));

            return Enumerable.Range(0, Times)
                             .Select(_ => screen.Consult(read[0]))
                             .GroupBy(r => r.Triggered && r.Entry != null ? r.Entry.Id : "")
                             .Select(g => (g.Key, g.Count()))
                             .OrderByDescending(g => g.Item2).ThenBy(g => g.Key)
                             .ToList();
        }

        // what twenty openings came to: each one's coins in copper and its items, and the whole in copper
        public static LootSample Loot(LootTableDraft table, IEnumerable<LootTableDraft> all, int seed)
        {
            string text = new JsonObject { ["tables"] = new JsonArray(all.Select(t => (JsonNode)t.ToJson()).ToArray()) }.ToJsonString();

            if (!LootReader.TryRead(text, out IReadOnlyList<LootTable> read, out _)) return null;

            var tables = new LootTables(read);
            LootTable rolled = tables.Find(table.Id);

            if (rolled == null) return null;

            var screen = new GmScreen(new SeededRng(seed));
            List<LootRoll> rolls = Enumerable.Range(0, Times).Select(_ => screen.Open(rolled, tables)).ToList();

            return new LootSample(rolls.Select(r => (r.Copper, (IReadOnlyList<Found>)r.Found)).ToList());
        }
    }

    public sealed record LootSample(IReadOnlyList<(int Copper, IReadOnlyList<Found> Items)> Openings)
    {
        public int Copper => Openings.Sum(o => o.Copper);

        public int Empty => Openings.Count(o => o.Copper == 0 && o.Items.Count == 0);

        // every item, and how many came up in all
        public IReadOnlyList<(string Item, int Count)> Items =>
            Openings.SelectMany(o => o.Items).GroupBy(f => f.Item).Select(g => (g.Key, g.Sum(f => f.Count)))
                    .OrderByDescending(i => i.Item2).ToList();
    }
}
