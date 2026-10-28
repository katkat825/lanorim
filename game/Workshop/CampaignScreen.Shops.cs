using System;
using System.Linq;
using Content.Campaigns;
using Content.Workshop;
using Content.Workshop.Tables;
using Core.Combat;
using Core.Characters;
using Core.Words;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE SHOPS TAB (cc_task_n Part 4): each shop's name, whether it sells or lends (an armory), what it pays back, and
    // its stock in the order the shop shows it, from the item picker - an armory's greying out what it can't lend.
    // And THE LUCKY ROLLS TAB (Part 5): the campaign's natural-1 and natural-20 moments
    public partial class CampaignScreen
    {
        void Shops(VBoxContainer page)
        {
            page.AddChild(Forms.Hint(TableWords.ShopsHintKey));

            if (Draft.Tables.Shops.Count == 0) page.AddChild(Forms.Hint(TableWords.NoShopsKey));

            WhereUsed uses = Editor.Uses;

            foreach (ShopDraft shop in Draft.Tables.Shops)
            {
                string id = shop.Id;
                string summary = Ui.Say(shop.Armory ? TableWords.LendsKey : TableWords.SellsKey) + ", " +
                                 string.Join(", ", shop.Stock.Take(6).Select(Pickers.ItemName)) + (shop.Stock.Count > 6 ? "..." : "");

                page.AddChild(Card("shop", id, shop.Name.Length > 0 ? shop.Name : TablesDraft.Plain(id), summary, uses.OfShop(id),
                                   () => Editor.DeleteShop(id), body => Shop(body, id)));
            }

            page.AddChild(Maker("NewShop", TableWords.NewShopKey, TableWords.NewShopKey, n => Editor.NewShop(n, false), "shop"));

            TableJson(page, Draft.Tables.Shops.Select(s => s.ToJson()));
        }

        void Shop(VBoxContainer form, string id)
        {
            ShopDraft shop = Draft.Tables.Shop(id);
            if (shop == null) return;

            form.AddChild(Forms.Field(TableWords.NameKey,
                Forms.Line("ShopName_" + id, shop.Name, t => Edit($"shop.{id}.name", d => d.Tables.Shop(id).Name = t, true)), null,
                _advanced ? Forms.Note(id) : null));

            form.AddChild(Forms.Choice("ShopKind_" + id, new[] { false, true }, shop.Armory, a => a ? TableWords.LendsKey : TableWords.SellsKey,
                a => Change(() => Editor.Edit("shop", d => { d.Tables.Shop(id).Armory = a; if (a) d.Tables.Shop(id).SellPercent = -1; }))));

            if (!shop.Armory)
            {
                var pays = new[] { -1, 25, 50, 75, 100 }.Union(new[] { shop.SellPercent }).OrderBy(p => p).ToList();
                OptionButton paid = Forms.Choice("ShopPays_" + id, pays, shop.SellPercent, p => p < 0 ? TableWords.PaysGameKey : p == 50 ? TableWords.PaysHalfKey : null,
                                                 p => Edit("shop", d => d.Tables.Shop(id).SellPercent = p));
                for (int i = 0; i < pays.Count; i++) if (pays[i] >= 0 && pays[i] != 50) paid.SetItemText(i, Ui.Say(TableWords.PaysPercentKey, pays[i]));
                form.AddChild(Forms.Field(TableWords.PaysKey, paid, null));
            }

            // the stock, in order
            var stock = Ui.Column(4);
            stock.Name = "Stock_" + id;

            for (int i = 0; i < shop.Stock.Count; i++)
            {
                int at = i;
                string item = shop.Stock[i];
                Label name = Ui.Plain(Pickers.ItemName(item) + (Things.Items.Find(item) is { Cost: > 0 } priced ? "   " + Ui.Money(priced.Cost) : ""));
                name.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                var row = Ui.Row(8, name,
                    Ui.Button(WorkshopWords.UpKey, () => Change(() => Editor.Edit("shop", d => Swap(d.Tables.Shop(id).Stock, at, at - 1)))).Greyed(at == 0, null),
                    Ui.Button(WorkshopWords.DownKey, () => Change(() => Editor.Edit("shop", d => Swap(d.Tables.Shop(id).Stock, at, at + 1)))).Greyed(at == shop.Stock.Count - 1, null),
                    Forms.Removal($"RemoveStock_{id}_{at}", () => Change(() => Editor.Edit("shop", d => d.Tables.Shop(id).Stock.RemoveAt(at)))));
                row.Name = $"StockItem_{id}_{at}";
                stock.AddChild(row);
            }

            form.AddChild(Forms.Field(TableWords.StockKey, stock, null));
            form.AddChild(Pickers.Item("AddStock_" + id, Things.Items,
                item => Change(() => Editor.Edit("shop", d => { if (!d.Tables.Shop(id).Stock.Contains(item)) d.Tables.Shop(id).Stock.Add(item); })),
                shop.Armory ? MerchantDef.CanLend : null, shop.Armory ? TableWords.ArmoryRefusesKey : null));
        }

        // --- lucky and unlucky rolls ---------------------------------------------------------------------------------

        void Lucky(VBoxContainer page)
        {
            page.AddChild(Forms.Hint(TableWords.LuckHintKey));
            page.AddChild(Forms.Hint(Draft.Consequences == Layering.Replace ? TableWords.LuckReplacesKey : TableWords.LuckAddsKey));

            if (Draft.Consequences == Layering.Replace && (Draft.Tables.Luck.All(c => c.Lucky) || Draft.Tables.Luck.All(c => !c.Lucky)))
            {
                Label both = Ui.Label(TableWords.NeedsBothKey);
                both.AddThemeColorOverride("font_color", Forms.Wrong);
                page.AddChild(both);
            }

            foreach (ConsequenceDraft moment in Draft.Tables.Luck)
            {
                string id = moment.Id;
                string summary = Ui.Say(moment.Lucky ? TableWords.LuckyKey : TableWords.UnluckyKey) + ", " +
                                 Ui.Say(TableWords.MomentKind(moment.Kind.Length == 0 ? "flavour" : moment.Kind));

                page.AddChild(Card("luck", id, moment.Name.Length > 0 ? moment.Name : TablesDraft.Plain(id), summary, null,
                                   () => Editor.DeleteMoment(id), body => Moment(body, id)));
            }

            page.AddChild(Maker("NewLucky", TableWords.NewLuckyKey, TableWords.NewLuckyKey, n => Editor.NewMoment(n, true), "luck"));
            page.AddChild(Maker("NewUnlucky", TableWords.NewUnluckyKey, TableWords.NewUnluckyKey, n => Editor.NewMoment(n, false), "luck"));

            TableJson(page, Draft.Tables.Luck.Select(c => c.ToJson()));
        }

        void Moment(VBoxContainer form, string id)
        {
            ConsequenceDraft moment = Draft.Tables.Moment(id);
            if (moment == null) return;

            ConsequenceDraft In(CampaignDraft d) => d.Tables.Moment(id);

            form.AddChild(Forms.Field(TableWords.NameKey,
                Forms.Line("MomentName_" + id, moment.Name, t => Edit("luck.name", d => In(d).Name = t, true)), null, _advanced ? Forms.Note(id) : null));

            form.AddChild(Forms.Choice("MomentSide_" + id, new[] { true, false }, moment.Lucky, l => l ? TableWords.LuckyKey : TableWords.UnluckyKey,
                l => Change(() => Editor.Edit("luck", d => In(d).Polarity = l ? "boon" : "bane"))));

            string kind = TableWords.MomentKinds.Contains(moment.Kind) ? moment.Kind : "flavour";
            form.AddChild(Forms.Field(TableWords.WhatHappensKey,
                Forms.Choice("MomentKind_" + id, TableWords.MomentKinds, kind, TableWords.MomentKind, k => Change(() => Editor.Edit("luck", d =>
                {
                    ConsequenceDraft c = In(d);
                    c.Kind = k;
                    if (k != "ability_shift") c.Ability = "";
                    if (k != "condition") c.Condition = "";
                    if (k == "flavour" || k == "condition") c.Amount = "";
                    if (k == "ability_shift" && c.Ability.Length == 0) { c.Ability = "str"; c.Amount = c.Lucky ? "1" : "-1"; }
                    if (k == "condition" && c.Condition.Length == 0) c.Condition = "prone";
                    if ((k == "health" || k == "gold") && c.Amount.Length == 0) c.Amount = "1d4";
                }))), null));

            if (kind is "health" or "gold" or "ability_shift")
                form.AddChild(Forms.Field(TableWords.AmountKey, Pickers.Dice("MomentAmount_" + id, moment.Amount, true, t =>
                {
                    if (Pickers.IsDice(t)) Edit("luck.amount", d => In(d).Amount = t, true);
                }), null));

            if (kind == "ability_shift")
            {
                var abilities = Enum.GetValues<Ability>().ToList();
                EnumWords.TryParse(moment.Ability.Length == 0 ? "str" : moment.Ability, out Ability now);
                form.AddChild(Forms.Field(TableWords.AbilityKey,
                    Forms.Choice("MomentAbility_" + id, abilities, now, a => Core.Localization.KeyConventions.Key("ability", a.Id(), "name"), a => Edit("luck", d => In(d).Ability = a.Id())), null));
            }

            if (kind == "condition")
            {
                var conditions = Enum.GetValues<Condition>().Where(c => c != Condition.None).ToList();
                EnumWords.TryParse(moment.Condition, out Condition now);
                form.AddChild(Forms.Field(TableWords.ConditionKey,
                    Forms.Choice("MomentCondition_" + id, conditions, now, c => Core.Localization.KeyConventions.Key("condition", c.Id(), "name"),
                                 c => Edit("luck", d => In(d).Condition = c.Id())), null));
            }

            if (kind is "health" or "gold")
                form.AddChild(Forms.Tick("MomentScales_" + id, TableWords.ScalesKey, moment.ScalesWithLevel, on => Edit("luck", d => In(d).ScalesWithLevel = on)));

            int total = Draft.Tables.Luck.Where(c => c.Lucky == moment.Lucky).Sum(c => Math.Max(1, c.Weight));
            form.AddChild(Pickers.Likely("MomentLikely_" + id, Math.Max(1, moment.Weight), total, v => Edit("luck.weight", d => In(d).Weight = v, true)));

            form.AddChild(Forms.Field(TableWords.MomentLineKey,
                Forms.Line("MomentLine_" + id, moment.Line, t => Edit("luck.line", d => In(d).Line = t, true)), null));
        }
    }
}
