using System;
using System.Collections.Generic;
using System.Linq;
using Core.Tables;

namespace Content.Workshop.Tables
{
    // EVERY TABLE A CAMPAIGN SHIPS, AS THE WORKSHOP EDITS THEM (cc_task_n): its encounter tables (the Fights tab's
    // set pieces and the Random encounters tab's), loot tables, shops and lucky and unlucky moments. Part of the
    // CampaignDraft, so a change is one undo step and the autosave keeps it; read from and written to the campaign's
    // own files by TableFiles
    public sealed class TablesDraft
    {
        // THE WORKSHOP'S OWN FIGHTS TABLE (cc_task_n Part 1): where a fight made on the Fights tab goes, unless the
        // author moves it. "<<fight id>>" finds a fight in any table, so it's an ordinary encounter table
        public const string FightsTable = "fights";

        public const string FightsFile = "encounters/fights.json";

        // where a new random table, loot table, shop or moment goes
        public const string RandomFile = "encounters/random.json";

        public const string LootFile = "loot/loot.json";

        public const string ShopsFile = "merchants/merchants.json";

        public const string LuckFile = "consequences/consequences.json";

        public List<EncounterTableDraft> Encounters { get; set; } = new();

        public List<LootTableDraft> Loot { get; set; } = new();

        public List<ShopDraft> Shops { get; set; } = new();

        public List<ConsequenceDraft> Luck { get; set; } = new();

        // a table's or entry's name as the author wrote it, when it's more than its id ("fight:goblin_camp" -> "The
        // goblin camp"). Kept in workshop.json, never in the game's files (WorkshopNotes)
        public Dictionary<string, string> Names { get; set; } = new(StringComparer.Ordinal);

        public EncounterTableDraft Table(string id) => Encounters.FirstOrDefault(t => t.Id == id);

        public LootTableDraft LootTable(string id) => Loot.FirstOrDefault(t => t.Id == id);

        public ShopDraft Shop(string id) => Shops.FirstOrDefault(s => s.Id == id);

        public ConsequenceDraft Moment(string id) => Luck.FirstOrDefault(c => c.Id == id);

        // --- fights: every fight entry in every table, where it is -----------------------------------------------------

        public IEnumerable<(EncounterTableDraft Table, EncounterDraft Fight)> Fights =>
            Encounters.SelectMany(t => t.Entries.Where(e => e.IsFight).Select(e => (t, e)));

        public (EncounterTableDraft Table, EncounterDraft Fight) Fight(string id) =>
            Fights.FirstOrDefault(f => f.Fight.Id == id);

        // THE RANDOM ENCOUNTERS TAB'S TABLES: every table but the Workshop's fights, and but a hand-made one of set
        // pieces (no trigger, nothing but fights, as sample_millbrook's set_pieces) unless the story rolls it
        public IEnumerable<EncounterTableDraft> RandomTables(Func<string, bool> rolled) =>
            Encounters.Where(t => t.Id != FightsTable &&
                                  (t.Trigger != null || t.Entries.Any(e => !e.IsFight) || rolled(t.Id)));

        // --- names --------------------------------------------------------------------------------------------------

        public static string NameKey(string kind, string id) => kind + ":" + id;

        public string NameOf(string kind, string id) =>
            Names.TryGetValue(NameKey(kind, id), out string named) && named.Length > 0 ? named : Plain(id);

        public void Name(string kind, string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim() == Plain(id)) Names.Remove(NameKey(kind, id));
            else Names[NameKey(kind, id)] = name.Trim();
        }

        // "goblin_camp" -> "Goblin camp": what a hand-written id reads as when nobody named it
        public static string Plain(string id)
        {
            string words = (id ?? "").Replace('_', ' ').Trim();

            return words.Length == 0 ? "" : char.ToUpperInvariant(words[0]) + words.Substring(1);
        }

        // --- ids ----------------------------------------------------------------------------------------------------

        // every id a fight or an encounter entry has, across every table: <<fight id>> names one, so two fights of one id
        // anywhere is a fight the story can't tell apart
        public bool EntryTaken(string id) => Encounters.Any(t => t.Id == id || t.Entries.Any(e => e.Id == id));

        public bool LootTaken(string id) => Loot.Any(t => t.Id == id || t.Entries.Any(e => e.Id == id));

        public override string ToString() =>
            $"{Encounters.Count} encounter tables, {Fights.Count()} fights, {Loot.Count} loot tables, {Shops.Count} shops, {Luck.Count} moments";
    }

    // A NEW FIGHT's defaults: one monster, for level 1, medium. made by the editor, never read
    public static class NewEntries
    {
        public static EncounterDraft Fight(string id) => new()
        {
            Id = id,
            Kind = EntryKind.Fight,
            Level = 1,
            Difficulty = "moderate",
            Monsters = new List<BandDraft> { new() { Monster = "goblin" } },
        };
    }
}
