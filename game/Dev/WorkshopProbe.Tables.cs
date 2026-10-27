using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Workshop;
using Content.Workshop.Tables;
using Core.Tables;
using Game.Screens;
using Game.Workshop;
using Godot;

namespace Game.Dev
{
    // THE TABLE EDITORS, DRIVEN (cc_task_n, check-workshop extended): on the probe's campaign, through the tabs' own
    // fields - a fight (a second monster from the picker turns the meter amber), a random table with a fight and a
    // moment, a loot table with all three coins and an item, a shop and an armory (whose picker greys out a potion), and
    // a lucky roll; saved, and the pack loads with no errors
    public partial class WorkshopProbe
    {
        const string Fight = "bridge_goblins";
        const string Road = "forest_road";
        const string Chest = "probe_chest";

        TablesDraft Tables => Page.Editor.Draft.Tables;

        void MakeFight()
        {
            Page.GoThere(WorkshopTab.Fights);
            Type<LineEdit>("NewFightName", "Bridge Goblins", changed: false);
            Press("NewFight");
            Expect(Tables.Fight(Fight).Fight != null, "a fight made on the Fights tab");
            Expect(Tables.Fight(Fight).Table?.Id == TablesDraft.FightsTable, "in the Workshop's own fights table");

            Choose(Find<OptionButton>("FightMap_" + Fight), 1);
            Expect(Tables.Fight(Fight).Fight?.Map == "first_steps", $"fought on the campaign's map ({Tables.Fight(Fight).Fight?.Map})");

            Expect(Meter() == "Green", $"one goblin at level 1 is about right ({Meter()})");

            Pick("AddMonster_" + Fight + "_List", "Goblin");
            Press("AddMonster_" + Fight + "_Add");
            Expect(Tables.Fight(Fight).Fight?.Monsters.Count == 2, "a second monster from the picker");
            Expect(Meter() == "Amber", $"and the meter goes amber ({Meter()})");

            Type<LineEdit>("FightLine_" + Fight, "Goblins drop from the bridge rails.");
        }

        // a picker's line chosen by the name it starts with
        void Pick(string list, string name)
        {
            ItemList items = Find<ItemList>(list);
            int at = Enumerable.Range(0, items?.ItemCount ?? 0).FirstOrDefault(i => items.GetItemText(i).StartsWith(name + "   ") || items.GetItemText(i) == name, -1);
            Expect(at >= 0, $"{name} in the picker");
            if (at >= 0) items.Select(at);
        }

        string Meter() => Find<VBoxContainer>("Meter_" + Fight)?.GetMeta("colour").AsString();

        void MakeRandomTable()
        {
            Page.GoThere(WorkshopTab.Encounters);
            Type<LineEdit>("NewRandomTableName", "Forest Road", changed: false);
            Press("NewRandomTable");
            Expect(Tables.Table(Road) != null, "a random table made");
            Expect(Oftenness.Of(Tables.Table(Road).Trigger) == HowOften.Sometimes, "something happens sometimes");

            HBoxContainer adds = Find<HBoxContainer>("AddEntries_" + Road);
            adds?.GetChild<Button>(0).EmitSignal(BaseButton.SignalName.Pressed);
            Find<HBoxContainer>("AddEntries_" + Road)?.GetChild<Button>(1).EmitSignal(BaseButton.SignalName.Pressed);

            EncounterTableDraft road = Tables.Table(Road);
            Expect(road.Entries.Select(e => e.Kind).SequenceEqual(new[] { EntryKind.Nothing, EntryKind.Fight, EntryKind.Line }),
                   $"with a fight and a moment beside its nothing ({string.Join(", ", road.Entries)})");

            string fight = road.Entries.First(e => e.IsFight).Id, moment = road.Entries.First(e => e.Kind == EntryKind.Line).Id;
            Type<LineEdit>("FightLine_" + fight, "Wolves, between the trees.");
            Type<LineEdit>("EntryLine_" + moment, "An owl calls, and nothing else.");
            Expect(Tables.Table(Road).Entry(moment).Line.Length > 0, "the moment's line written");

            Choose(Find<OptionButton>("HowOften_" + Road), (int)HowOften.Rarely);
            Expect(Tables.Table(Road).Trigger?.AtLeast == 16, "Rarely writes 16 or more on a d20");

            Press("RollIt_" + Road);
            Expect(Find<VBoxContainer>("Rolled_" + Road) != null, "Roll it shows twenty rolls");
        }

        void MakeLoot()
        {
            Page.GoThere(WorkshopTab.Loot);
            Type<LineEdit>("NewLootName", "Probe Chest", changed: false);
            Press("NewLoot");
            LootDraft find = Tables.LootTable(Chest)?.Entries.FirstOrDefault();
            Expect(find?.Kind == LootKind.Find, "a loot table made, a find in it");
            if (find == null) return;

            Type<LineEdit>($"Coin_silver_{find.Id}", "2d4");
            Type<LineEdit>($"Coin_copper_{find.Id}", "5");

            Type<LineEdit>($"AddItem_{find.Id}_Search", "Torch");
            Pick($"AddItem_{find.Id}_List", "Torch");
            Press($"AddItem_{find.Id}_Add");

            find = Tables.LootTable(Chest).Entries.First();
            Expect(!find.Gold.IsEmpty && find.Silver.Roll == "2d4" && find.Copper.Roll == "5", "gold, silver and copper");
            Expect(find.Items.Any(i => i.Item == "torch"), $"and a torch ({string.Join(", ", find.Items.Select(i => i.Item))})");

            Press("RollLoot_" + Chest);
            Expect(Find<VBoxContainer>("Opened_" + Chest) != null, "Roll it shows what twenty openings came to");
        }

        void MakeShops()
        {
            Page.GoThere(WorkshopTab.Shops);
            Type<LineEdit>("NewShopName", "Probe Store", changed: false);
            Press("NewShop");
            Type<LineEdit>("AddStock_probe_store_Search", "Potion of Healing");
            Pick("AddStock_probe_store_List", "Potion of Healing");
            Press("AddStock_probe_store_Add");
            Expect(Tables.Shop("probe_store")?.Stock.Contains("potion_of_healing") == true, "a shop selling a potion");

            Type<LineEdit>("NewShopName", "Probe Racks", changed: false);
            Press("NewShop");
            Choose(Find<OptionButton>("ShopKind_probe_racks"), 1);
            Expect(Tables.Shop("probe_racks")?.Armory == true, "a second, made an armory");

            Type<LineEdit>("AddStock_probe_racks_Search", "Potion of Healing");
            ItemList list = Find<ItemList>("AddStock_probe_racks_List");
            Expect(list != null && list.ItemCount > 0 && list.IsItemDisabled(0), "whose picker greys out the potion");

            Type<LineEdit>("AddStock_probe_racks_Search", "Longsword");
            Pick("AddStock_probe_racks_List", "Longsword");
            Press("AddStock_probe_racks_Add");
            Expect(Tables.Shop("probe_racks")?.Stock.SequenceEqual(new[] { "longsword" }) == true, "and lends a longsword");
        }

        void MakeLucky()
        {
            Page.GoThere(WorkshopTab.Lucky);
            Type<LineEdit>("NewLuckyName", "A Coin in the Mud", changed: false);
            Press("NewLucky");
            ConsequenceDraft coin = Tables.Moment("a_coin_in_the_mud");
            Expect(coin?.Lucky == true, "a lucky roll made");

            Choose(Find<OptionButton>("MomentKind_a_coin_in_the_mud"), TableWords.MomentKinds.ToList().IndexOf("gold"));
            Type<LineEdit>("MomentLine_a_coin_in_the_mud", "Something glints in the mud.");
            Expect(Tables.Moment("a_coin_in_the_mud")?.Kind == "gold" && Tables.Moment("a_coin_in_the_mud").Amount.Length > 0, "finding coins");
        }

        // after Save: the tables are in the campaign's own files and the pack loads with no errors
        void TablesSaved()
        {
            Package pack = Package.Read(Folder);

            Expect(File.Exists(Path.Combine(Folder, TablesDraft.FightsFile)), "the fight is in encounters/fights.json");
            Expect(pack.Sound, "and the pack loads with no errors: " + string.Join("; ", pack.Problems.Where(p => !p.IsACaution)));
            Expect(pack.Merchants.Any(m => m.Id == "probe_racks" && m.Armory), "the armory is the game's");
        }
    }
}
