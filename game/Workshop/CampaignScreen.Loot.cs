using System;
using System.Collections.Generic;
using System.Linq;
using Content.Workshop;
using Content.Workshop.Tables;
using Core.Tables;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE LOOT TAB (cc_task_n Part 3): the tables Search for loot opens and fights leave behind. Each entry is a find
    // (items, how many; gold, silver and copper, each a number or dice, or dice times a number; a line the GM reads),
    // nothing (the pockets were empty) or another table rolled; each with how likely; and Roll it, twenty times, in coins
    public partial class CampaignScreen
    {
        readonly Dictionary<string, LootSample> _opened = new();

        void Loot(VBoxContainer page)
        {
            page.AddChild(Forms.Hint(TableWords.LootHintKey));

            if (Draft.Tables.Loot.Count == 0) page.AddChild(Forms.Hint(TableWords.NoLootKey));

            WhereUsed uses = Editor.Uses;

            foreach (LootTableDraft table in Draft.Tables.Loot)
            {
                string id = table.Id;
                page.AddChild(Card("loot", id, Draft.Tables.NameOf("loot", id), "", uses.OfLoot(id),
                                   () => Editor.DeleteLoot(id), body => LootTable(body, id)));
            }

            page.AddChild(Maker("NewLoot", TableWords.NewLootKey, TableWords.NewLootKey, Editor.NewLoot, "loot"));

            TableJson(page, Draft.Tables.Loot.Select(t => t.ToJson()));
        }

        static LootDraft LootIn(CampaignDraft d, string table, string entry) => d.Tables.LootTable(table)?.Entry(entry);

        void LootTable(VBoxContainer form, string id)
        {
            LootTableDraft table = Draft.Tables.LootTable(id);
            if (table == null) return;

            form.AddChild(NameField("loot", id));

            int total = table.Entries.Sum(e => Math.Max(1, e.Weight));
            foreach (LootDraft entry in table.Entries.ToList()) form.AddChild(LootEntry(id, entry, total));

            var adds = Ui.Row(8,
                Ui.Button(TableWords.AddFindKey, () => Change(() => Editor.AddLootEntry(id, LootKind.Find) != null)),
                Ui.Button(TableWords.AddEmptyKey, () => Change(() => Editor.AddLootEntry(id, LootKind.Nothing) != null)),
                Ui.Button(TableWords.AddRollTableKey, () => Change(() => Editor.AddLootEntry(id, LootKind.Table) != null)).Greyed(Draft.Tables.Loot.Count < 2, null));
            adds.Name = "AddLoot_" + id;
            form.AddChild(adds);

            Button roll = Ui.Button(TableWords.RollItKey, () =>
            {
                _opened[id] = TableSamples.Loot(Draft.Tables.LootTable(id), Draft.Tables.Loot, (int)Time.GetTicksMsec());
                Redraw();
            });
            roll.Name = "RollLoot_" + id;
            form.AddChild(roll);

            if (_opened.TryGetValue(id, out LootSample sample) && sample != null)
            {
                var shown = Ui.Column(2, Ui.Label(TableWords.LootTotalsKey, Ui.Money(sample.Copper), sample.Empty));
                shown.Name = "Opened_" + id;
                foreach ((string item, int count) in sample.Items) shown.AddChild(Ui.Label(TableWords.ResultKey, Pickers.ItemName(item), count));
                form.AddChild(shown);
            }
        }

        Control LootEntry(string table, LootDraft entry, int total)
        {
            string id = entry.Id;

            OptionButton kind = Forms.Choice("LootKind_" + id, Enum.GetValues<LootKind>(), entry.Kind, TableWords.LootEntry,
                k => Change(() => Editor.Edit("loot", d =>
                {
                    LootDraft e = LootIn(d, table, id);
                    e.Kind = k;
                    if (k != LootKind.Find) { e.Items.Clear(); e.Gold = new(); e.Silver = new(); e.Copper = new(); e.Speaks = false; }
                    if (k != LootKind.Table) e.Table = "";
                    else e.Table = d.Tables.Loot.FirstOrDefault(t => t.Id != table)?.Id ?? "";
                })));
            kind.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var top = Ui.Row(8, kind,
                Pickers.Likely("LootLikely_" + id, Math.Max(1, entry.Weight), total, v => Edit("loot.weight", d => LootIn(d, table, id).Weight = v, true)),
                Forms.Removal("RemoveLoot_" + id, () => Change(() => Editor.RemoveLootEntry(table, id))));

            var column = Ui.Column(6, top);
            column.Name = "LootEntry_" + id;

            if (entry.Kind == LootKind.Find)
            {
                // items, and how many of each
                for (int i = 0; i < entry.Items.Count; i++)
                {
                    int at = i;
                    Label name = Ui.Plain(Pickers.ItemName(entry.Items[i].Item));
                    name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    column.AddChild(Ui.Row(8, name, Ui.Label(TableWords.HowManyKey),
                        Pickers.Dice($"LotCount_{id}_{at}", entry.Items[i].Count, _advanced, t =>
                        {
                            if (Pickers.IsDice(t)) Edit("loot.count", d => LootIn(d, table, id).Items[at].Count = t == "1" ? "" : t, true);
                        }),
                        Forms.Removal($"RemoveLot_{id}_{at}", () => Change(() => Editor.Edit("loot", d => LootIn(d, table, id).Items.RemoveAt(at))))));
                }

                column.AddChild(Forms.Field(TableWords.ItemsKey,
                    Pickers.Item("AddItem_" + id, Things.Items, item => Change(() => Editor.Edit("loot", d => LootIn(d, table, id).Items.Add(new LotDraft { Item = item })))), null));

                // the coins
                var coins = Ui.Row(12);
                foreach ((string coin, CoinDraft purse) in entry.Coins) coins.AddChild(Coin(table, id, coin, purse));
                column.AddChild(Forms.Field(TableWords.CoinsKey, coins, TableWords.DiceHintKey));

                // its line
                column.AddChild(Forms.Field(TableWords.FindLineKey,
                    Forms.Line("FindLine_" + id, entry.Line, t => Edit("loot.line", d =>
                    {
                        LootDraft e = LootIn(d, table, id);
                        e.Line = t;
                        e.Speaks = t.Trim().Length > 0;
                    }, true)), null));
            }
            else if (entry.Kind == LootKind.Table)
            {
                var others = Draft.Tables.Loot.Where(t => t.Id != table).Select(t => t.Id).ToList();
                if (others.Count > 0)
                {
                    OptionButton next = Forms.Choice("RollsTable_" + id, others, others.Contains(entry.Table) ? entry.Table : others[0], _ => null,
                                                     t => Edit("loot", d => LootIn(d, table, id).Table = t));
                    for (int i = 0; i < others.Count; i++) next.SetItemText(i, Draft.Tables.NameOf("loot", others[i]));
                    column.AddChild(Forms.Field(TableWords.RollsTableKey, next, null));
                }
            }

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", (int)Ui.Px(24));
            margin.AddChild(column);
            return margin;
        }

        // one coin: its dice (a number, or dice), and in Advanced how many times over ("3d6 x 10")
        Control Coin(string table, string entry, string coin, CoinDraft purse)
        {
            LineEdit roll = Forms.Line($"Coin_{coin}_{entry}", purse.Roll, t =>
            {
                if (Pickers.IsDice(t.Trim())) Edit("loot.coin", d => Purse(LootIn(d, table, entry), coin).Roll = t.Trim(), true);
            });
            roll.CustomMinimumSize = new Vector2(Ui.Px(70), 0);

            var row = Ui.Row(4, Ui.Label(TableWords.Coin(coin)), roll);

            if (_advanced || purse.Times > 1)
            {
                row.AddChild(Ui.Label(TableWords.TimesKey));
                row.AddChild(Forms.Number($"CoinTimes_{coin}_{entry}", 1, 1000, Math.Max(1, purse.Times),
                                          v => Edit("loot.coin", d => Purse(LootIn(d, table, entry), coin).Times = v, true)));
            }

            return row;
        }

        static CoinDraft Purse(LootDraft entry, string coin) => coin switch { "gold" => entry.Gold, "silver" => entry.Silver, _ => entry.Copper };
    }
}
