using System.Linq;
using Content.Campaigns;
using Content.Workshop.Tables;
using Core.Tables;

namespace Content.Workshop
{
    // THE TABLE EDITORS' OPERATIONS (cc_task_n): a new fight, random table, loot table, shop or lucky moment, each with
    // its id made from its first name (and the next free one if that's taken: an id never changes once the story can
    // name it, as a chapter's), and each taken away. Each one undo step; the forms' own fields are Edits of their own
    public sealed partial class CampaignEditor
    {
        TablesDraft Tables => Draft.Tables;

        // a fight in the Workshop's own fights table (made the first time), and its id
        public string NewFight(string name)
        {
            string id = ContentId.Free(ContentId.From(name, "fight"), Tables.EntryTaken);

            Edit("fight", d =>
            {
                EncounterTableDraft fights = d.Tables.Table(TablesDraft.FightsTable);

                if (fights == null)
                {
                    fights = new EncounterTableDraft { File = TablesDraft.FightsFile, Id = TablesDraft.FightsTable };
                    d.Tables.Encounters.Add(fights);
                }

                fights.Entries.Add(NewEntries.Fight(id));
                d.Tables.Name("fight", id, name);
            });

            return id;
        }

        // a fight taken out of whichever table it's in; a table it leaves empty goes with it
        public bool DeleteFight(string id) => Edit("fight", d =>
        {
            foreach (EncounterTableDraft table in d.Tables.Encounters) table.Entries.RemoveAll(e => e.Id == id);
            d.Tables.Encounters.RemoveAll(t => t.Id == TablesDraft.FightsTable && t.Entries.Count == 0);
            d.Tables.Names.Remove(TablesDraft.NameKey("fight", id));
        });

        // A RANDOM TABLE: sometimes, heard, one moment in it to start with (a table can't be empty)
        public string NewRandomTable(string name)
        {
            string id = ContentId.Free(ContentId.From(name, "encounters"), Tables.EntryTaken);

            Edit("encounters", d =>
            {
                d.Tables.Encounters.Add(new EncounterTableDraft
                {
                    File = TablesDraft.RandomFile,
                    Id = id,
                    Trigger = Oftenness.Trigger(HowOften.Sometimes, null),
                    Entries = { new EncounterDraft { Id = id + "_quiet", Kind = EntryKind.Nothing } },
                });
                d.Tables.Name("encounters", id, name);
            });

            return id;
        }

        public bool DeleteRandomTable(string id) => Edit("encounters", d =>
        {
            d.Tables.Encounters.RemoveAll(t => t.Id == id);
            d.Tables.Names.Remove(TablesDraft.NameKey("encounters", id));
        });

        // an entry on a random table: a fight (the Fights tab's form, inline), a moment or nothing
        public string AddEntry(string table, EntryKind kind)
        {
            string id = ContentId.Free(table + "_" + EnumWords(kind), Tables.EntryTaken);

            Edit("encounters", d => d.Tables.Table(table)?.Entries.Add(kind == EntryKind.Fight
                ? NewEntries.Fight(id)
                : new EncounterDraft { Id = id, Kind = kind }));

            return id;
        }

        static string EnumWords(EntryKind kind) => kind switch { EntryKind.Fight => "fight", EntryKind.Line => "moment", _ => "quiet" };

        public bool RemoveEntry(string table, string entry) =>
            Edit("encounters", d => d.Tables.Table(table)?.Entries.RemoveAll(e => e.Id == entry));

        // --- loot -------------------------------------------------------------------------------------------------

        public string NewLoot(string name)
        {
            string id = ContentId.Free(ContentId.From(name, "loot"), Tables.LootTaken);

            Edit("loot", d =>
            {
                d.Tables.Loot.Add(new LootTableDraft
                {
                    File = TablesDraft.LootFile,
                    Id = id,
                    Entries = { new LootDraft { Id = id + "_find", Kind = LootKind.Find, Gold = new CoinDraft { Roll = "1d6" } } },
                });
                d.Tables.Name("loot", id, name);
            });

            return id;
        }

        public bool DeleteLoot(string id) => Edit("loot", d =>
        {
            d.Tables.Loot.RemoveAll(t => t.Id == id);
            foreach ((EncounterTableDraft _, EncounterDraft fight) in d.Tables.Fights.Where(f => f.Fight.Loot == id).ToList()) fight.Loot = "";
            d.Tables.Names.Remove(TablesDraft.NameKey("loot", id));
        });

        public string AddLootEntry(string table, LootKind kind)
        {
            string id = ContentId.Free(table + "_" + (kind == LootKind.Find ? "find" : kind == LootKind.Table ? "more" : "empty"), Tables.LootTaken);

            Edit("loot", d => d.Tables.LootTable(table)?.Entries.Add(new LootDraft
            {
                Id = id,
                Kind = kind,
                Gold = kind == LootKind.Find ? new CoinDraft { Roll = "1d6" } : new CoinDraft(),
                Table = kind == LootKind.Table ? d.Tables.Loot.FirstOrDefault(t => t.Id != table)?.Id ?? "" : "",
            }));

            return id;
        }

        public bool RemoveLootEntry(string table, string entry) =>
            Edit("loot", d => d.Tables.LootTable(table)?.Entries.RemoveAll(e => e.Id == entry));

        // --- shops ------------------------------------------------------------------------------------------------

        public string NewShop(string name, bool armory)
        {
            string id = ContentId.Free(ContentId.From(name, armory ? "armory" : "shop"), taken => Tables.Shop(taken) != null);

            Edit("shop", d => d.Tables.Shops.Add(new ShopDraft { File = TablesDraft.ShopsFile, Id = id, Name = name ?? "", Armory = armory }));

            return id;
        }

        public bool DeleteShop(string id) => Edit("shop", d => d.Tables.Shops.RemoveAll(s => s.Id == id));

        // --- lucky and unlucky rolls ---------------------------------------------------------------------------------

        public string NewMoment(string name, bool lucky)
        {
            string id = ContentId.Free(ContentId.From(name, lucky ? "lucky" : "unlucky"),
                                       taken => Tables.Moment(taken) != null || Schema.ConsequenceReader.Srd().All.Any(c => c.Id == taken));

            Edit("luck", d => d.Tables.Luck.Add(new ConsequenceDraft
            {
                File = TablesDraft.LuckFile,
                Id = id,
                Name = name ?? "",
                Polarity = lucky ? "boon" : "bane",
                Kind = "flavour",
            }));

            return id;
        }

        public bool DeleteMoment(string id) => Edit("luck", d => d.Tables.Luck.RemoveAll(c => c.Id == id));

        // --- where each is used --------------------------------------------------------------------------------------

        public WhereUsed Uses => new(Tables, WhereUsed.Story(Draft.Story));
    }
}
