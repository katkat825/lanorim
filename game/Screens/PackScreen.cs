using System;
using System.Linq;
using Content.Combat;
using Content.Inventory;
using Content.Screens;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Localization;
using Core.Resolution;
using Godot;

namespace Game.Screens
{
    // THE PACK, and the merchant's counter beside it when there is one
    public partial class PackScreen : Overlay
    {
        readonly PackView _view;
        readonly IResolver _dice;
        string _confirming;

        public PackScreen(PackView view, IResolver dice)
        {
            _view = view;
            _dice = dice;
            Width = 820;
        }

        protected override void Draw()
        {
            Body.AddChild(Ui.Title(_view.Armory ? PackView.ArmoryKey : PackView.TitleKey));
            Body.AddChild(Ui.Row(24, Ui.Plain(Ui.Money(_view.Copper)),
                                 Ui.Label(PackView.SlotsKey, _view.Used, _view.Capacity)));

            if (_view.AtTheCounter)
                Body.AddChild(Ui.Label(_view.MerchantLineKey));

            var mine = Ui.Column(4);

            foreach (PackRow row in _view.Rows)
            {
                mine.AddChild(Line(row));
                Confirming(mine, row);
            }

            Worn(mine);

            if (_view.Armory)
                Body.AddChild(Ui.Row(16, Ui.Scroll(mine, 360), Ui.Scroll(Racks(), 360)));
            else if (!_view.AtTheCounter)
                Body.AddChild(Ui.Scroll(mine, 360));
            else
                Body.AddChild(Ui.Row(16, Ui.Scroll(mine, 360), Ui.Scroll(Shelf(), 360)));

            Body.AddChild(Ui.Button(PackView.LeaveKey, Close));
        }

        // ONE THING IN THE PACK, with what can be done with it here: use, equip, hold in the off hand, sell at a counter,
        // discard. cc_task_j Part 3 took the steps of Draw out into Line, Confirming and Shelf
        HBoxContainer Line(PackRow r)
        {
            var line = Ui.Row(6, Ui.Plain($"{Ui.Say(r.NameKey)}  ×{r.Count}{(r.Equipped ? "  ●" : "")}"));
            line.GetChild<Label>(0).SizeFlagsHorizontal = SizeFlags.ExpandFill;

            if (!r.Quest && !r.Item.Heals.IsNothing)
                line.AddChild(Ui.Button(PackView.UseKey, () => { _view.Use(r.Item.Id, _dice); Redraw(); }));

            // put it on, or hold a Light weapon in the off hand (cc_task_open-questions-answers.md 3.1)
            if (_view.CanEquip(r.Item.Id))
                line.AddChild(Ui.Button(PackView.EquipKey, () => { _view.Equip(r.Item.Id); Redraw(); }));

            if (_view.CanHoldInOffHand(r.Item.Id))
                line.AddChild(Ui.Button(PackView.OffHandKey, () => { _view.HoldInOffHand(r.Item.Id); Redraw(); }));

            if (_view.AtTheCounter && r.SellsFor >= 0)
            {
                line.AddChild(Ui.Button($"{Ui.Say(PackView.SellKey)} {Ui.Money(r.SellsFor)}", () =>
                {
                    if (_view.NeedsSellConfirmation(r.Item.Id) && _confirming != "sell:" + r.Item.Id)
                    {
                        _confirming = "sell:" + r.Item.Id;
                        Redraw();
                        return;
                    }

                    _confirming = null;
                    _view.Sell(r.Item.Id, 1, confirmed: true);
                    Redraw();
                }, true));
            }

            if (!r.Quest)
                line.AddChild(Ui.Button(PackView.DiscardKey, () =>
                {
                    if (_view.NeedsDiscardWarning && _confirming != "discard:" + r.Item.Id)
                    {
                        _confirming = "discard:" + r.Item.Id;
                        Redraw();
                        return;
                    }

                    _confirming = null;
                    _view.Discard(r.Item.Id);
                    Redraw();
                }));

            return line;
        }

        // under the line, the question a sell or a discard asks first
        void Confirming(VBoxContainer mine, PackRow r)
        {
            if (_confirming == "sell:" + r.Item.Id) mine.AddChild(Ui.Label(PackView.SellEquippedKey));

            if (_confirming != "discard:" + r.Item.Id) return;

            mine.AddChild(Ui.Label(PackView.DiscardWarningKey));

            var never = new CheckBox { Text = Ui.Say(PackView.DontWarnAgainKey), FocusMode = FocusModeEnum.All };
            never.Toggled += on => { if (on) _view.DismissDiscardWarning(); };
            mine.AddChild(never);
        }

        // the merchant's shelf, each with Buy (greyed when the gold isn't there)
        VBoxContainer Shelf()
        {
            var shelf = Ui.Column(4);

            foreach (ShopRow row in _view.Shelf)
            {
                ShopRow r = row;
                shelf.AddChild(Ui.Row(6,
                    Ui.Plain($"{Ui.Say(r.NameKey)}  {Ui.Money(r.Price)}"),
                    Ui.Button(PackView.BuyKey, () => { _view.Buy(r.Item.Id); Redraw(); })
                      .Greyed(!r.Affordable, Merchant.LineFor(Rebuff.NoGold))));
            }

            return shelf;
        }

        // WHAT IS WORN AND HELD, each with Unequip: it goes back in the pack, or to the armory if it was lent
        // (cc_task_open-questions-answers.md 3.1); at a counter, each with its own Sell, which asks every time
        // (inventory_decisions.md; cc_task_i 5.1)
        void Worn(VBoxContainer mine)
        {
            if (_view.Worn.Count > 0) mine.AddChild(Ui.Label(PackView.WornKey));

            foreach ((Content.Items.Slot slot, Content.Items.Item item) in _view.Worn)
            {
                Content.Items.Slot s = slot;
                string lent = _view.IsLoaned(item) ? "  " + Ui.Say(PackView.LoanedKey) : "";
                var worn = Ui.Row(6, Ui.Plain($"{Ui.Say(PackView.SlotKey(s))}: {Ui.Say(item.NameKey)}{lent}"));
                worn.GetChild<Label>(0).SizeFlagsHorizontal = SizeFlags.ExpandFill;
                worn.AddChild(Ui.Button(PackView.UnequipKey, () => { _view.TakeOff(s); Redraw(); }));

                // selling it off your back: asks every time (inventory_decisions.md; cc_task_i 5.1)
                if (_view.SellsForWorn(s) >= 0)
                    worn.AddChild(Ui.Button($"{Ui.Say(PackView.SellKey)} {Ui.Money(_view.SellsForWorn(s))}", () =>
                    {
                        if (_confirming != "sell_worn:" + s)
                        {
                            _confirming = "sell_worn:" + s;
                            Redraw();
                            return;
                        }

                        _confirming = null;
                        _view.SellWorn(s, confirmed: true);
                        Redraw();
                    }, true));

                mine.AddChild(worn);

                if (_confirming == "sell_worn:" + s) mine.AddChild(Ui.Label(PackView.SellEquippedKey));
            }
        }

        // THE ARMORY'S RACKS (cc_task_i 3.3): everything it lends, each put on for free; what it replaces goes back
        // (lent) or into the pack (owned)
        VBoxContainer Racks()
        {
            var racks = Ui.Column(4, Ui.Label(PackView.RacksKey));

            foreach (Content.Items.Item item in _view.Racks)
            {
                string id = item.Id;
                var line = Ui.Row(6, Ui.Plain(Ui.Say(item.NameKey)));
                line.GetChild<Label>(0).SizeFlagsHorizontal = SizeFlags.ExpandFill;

                line.AddChild(Ui.Button(PackView.EquipKey, () => { _view.Borrow(id); Redraw(); }).Greyed(!_view.CanBorrow(id), null));

                if (_view.CanBorrowInOffHand(id))
                    line.AddChild(Ui.Button(PackView.OffHandKey, () => { _view.BorrowInOffHand(id); Redraw(); }));

                racks.AddChild(line);
            }

            return racks;
        }
    }
}
