using System;
using System.Linq;
using Content.Combat;
using Content.Inventory;
using Content.Items;
using Content.Screens;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Localization;
using Core.Resolution;
using Godot;

namespace Game.Screens
{
    // THE OVERLAYS THE TABLE OPENS OVER ITSELF (Tier 3b): each a centred card built from a view model
    // in content/Screens, closed by its own button or Esc. The table stays underneath; the rules are
    // idle while one is open.
    public abstract partial class Overlay : CenterContainer
    {
        protected VBoxContainer Body { get; private set; }

        public event Action Closed;

        protected float Width { get; set; } = 620;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;

            Body = Ui.Column(10);
            Body.CustomMinimumSize = new Vector2(Width, 0);

            AddChild(Ui.Panel(Body));
            Draw();
        }

        protected abstract void Draw();

        protected void Redraw()
        {
            Ui.Clear(Body);
            Draw();
            Ui.FocusFirst(Body);
        }

        public void Close()
        {
            Closed?.Invoke();
            QueueFree();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event.IsActionPressed("ui_cancel") && CanCancel)
            {
                GetViewport().SetInputAsHandled();
                Close();
            }
        }

        protected virtual bool CanCancel => true;
    }

    // LEVEL UP: what came, and the improvement spent before it closes
    public partial class LevelUpScreen : Overlay
    {
        readonly LevelUpView _view;

        public LevelUpScreen(LevelUpView view) => _view = view;

        protected override bool CanCancel => _view.Done;

        protected override void Draw()
        {
            Body.AddChild(Ui.Title(LevelUpView.TitleKey, _view.Level));
            Body.AddChild(Ui.Label(LevelUpView.HitPointsKey, _view.HitPointsGained));

            if (_view.NewFeatures.Count > 0)
            {
                Body.AddChild(Ui.Label(LevelUpView.FeaturesKey));

                foreach (var feature in _view.NewFeatures)
                {
                    Label name = Ui.Label(feature.NameKey);
                    name.TooltipText = Ui.Say(feature.DescriptionKey);
                    Body.AddChild(name);
                }
            }

            if (_view.Pending > 0)
            {
                Body.AddChild(Ui.Label(LevelUpView.ImprovementKey));

                AbilityImprovement suggested = _view.Suggested;

                Body.AddChild(Ui.Button(LevelUpView.SuggestedKey, () =>
                {
                    _view.TakeSuggested();
                    Redraw();
                }));

                Body.AddChild(Ui.Label(LevelUpView.TwoToOneKey));

                var row = Ui.Row(6);

                foreach (Ability ability in Abilities.All)
                {
                    Ability one = ability;
                    AbilityImprovement pick = AbilityImprovement.Two(one);

                    row.AddChild(Ui.Button($"{Ui.Say(one.ShortKey())} {_view.ScoreWith(pick, one)}", () =>
                    {
                        _view.Improve(pick, out _);
                        Redraw();
                    }, true));
                }

                Body.AddChild(row);
            }

            Body.AddChild(Ui.Button(LevelUpView.DoneKey, Close).Greyed(!_view.Done, _view.DoneWhyNotKey));
        }
    }

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
            Body.AddChild(Ui.Title(PackView.TitleKey));
            Body.AddChild(Ui.Row(24, Ui.Label(PackView.GoldKey, _view.Gold),
                                 Ui.Label(PackView.SlotsKey, _view.Used, _view.Capacity)));

            if (_view.AtTheCounter)
                Body.AddChild(Ui.Label(_view.MerchantLineKey));

            var mine = Ui.Column(4);

            foreach (PackRow row in _view.Rows)
            {
                PackRow r = row;
                var line = Ui.Row(6, Ui.Plain($"{Ui.Say(r.NameKey)}  ×{r.Count}{(r.Equipped ? "  ●" : "")}"));
                line.GetChild<Label>(0).SizeFlagsHorizontal = SizeFlags.ExpandFill;

                if (!r.Quest && !r.Item.Heals.IsNothing)
                    line.AddChild(Ui.Button(PackView.UseKey, () => { _view.Use(r.Item.Id, _dice); Redraw(); }));

                if (_view.AtTheCounter && r.SellsFor >= 0)
                {
                    line.AddChild(Ui.Button($"{Ui.Say(PackView.SellKey)} {r.SellsFor}", () =>
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

                mine.AddChild(line);

                if (_confirming == "sell:" + r.Item.Id) mine.AddChild(Ui.Label(PackView.SellEquippedKey));

                if (_confirming == "discard:" + r.Item.Id)
                {
                    mine.AddChild(Ui.Label(PackView.DiscardWarningKey));

                    var never = new CheckBox { Text = Ui.Say(PackView.DontWarnAgainKey), FocusMode = FocusModeEnum.All };
                    never.Toggled += on => { if (on) _view.DismissDiscardWarning(); };
                    mine.AddChild(never);
                }
            }

            if (!_view.AtTheCounter)
            {
                Body.AddChild(Ui.Scroll(mine, 360));
            }
            else
            {
                var shelf = Ui.Column(4);

                foreach (ShopRow row in _view.Shelf)
                {
                    ShopRow r = row;
                    shelf.AddChild(Ui.Row(6,
                        Ui.Plain($"{Ui.Say(r.NameKey)}  {r.Price}"),
                        Ui.Button(PackView.BuyKey, () => { _view.Buy(r.Item.Id); Redraw(); })
                          .Greyed(!r.Affordable, Merchant.LineFor(Rebuff.NoGold))));
                }

                Body.AddChild(Ui.Row(16, Ui.Scroll(mine, 360), Ui.Scroll(shelf, 360)));
            }

            Body.AddChild(Ui.Button(PackView.LeaveKey, Close));
        }
    }

    // THE CHARACTER SHEET, the short form: who, how hurt, how hard to hit, the scores, the spells,
    // the features with uses left
    public partial class SheetScreen : Overlay
    {
        readonly SheetView _sheet;

        public SheetScreen(SheetView sheet)
        {
            _sheet = sheet;
            Width = 760;
        }

        protected override void Draw()
        {
            Body.AddChild(Ui.Title(ScreenWords.SheetTitle));
            Body.AddChild(Ui.Plain($"{_sheet.Name} — {Ui.Say(_sheet.SpeciesKey)} {Ui.Say(_sheet.ClassKey)}"));
            Body.AddChild(Ui.Row(24,
                Ui.Label(ScreenWords.SheetLevel, _sheet.Level),
                Ui.Label(ScreenWords.SheetHp, _sheet.HitPoints, _sheet.MaxHitPoints),
                Ui.Label(ScreenWords.SheetAc, _sheet.ArmorClass)));

            var scores = Ui.Row(12);

            foreach (AbilityRow row in _sheet.Abilities)
                scores.AddChild(Ui.Plain($"{Ui.Say(row.ShortKey)} {row.Score} ({row.Modifier:+0;-0})"));

            Body.AddChild(scores);

            var more = Ui.Column(4);

            if (_sheet.Spells.Count > 0)
            {
                more.AddChild(Ui.Label(ScreenWords.SheetSpells));

                foreach (SpellCard card in _sheet.Spells)
                {
                    Label name = Ui.Label(card.NameKey);
                    name.ThemeTypeVariation = "CardLabel";
                    name.TooltipText = Ui.Say(card.DescriptionKey);
                    more.AddChild(name);
                }
            }

            if (_sheet.Special.Count > 0)
            {
                more.AddChild(Ui.Label(ScreenWords.SheetFeatures));

                foreach (SpecialRow row in _sheet.Special)
                {
                    Label name = Ui.Plain(Ui.Say(row.NameKey) + (row.UsesMost > 0 ? $"  {row.UsesLeft}/{row.UsesMost}" : ""));
                    name.TooltipText = Ui.Say(row.DescriptionKey);
                    more.AddChild(name);
                }
            }

            Body.AddChild(Ui.Scroll(more, 320));
            Body.AddChild(Ui.Button(ScreenWords.Back, Close));
        }
    }

    // A PLAIN CARD WITH A TITLE, A LINE AND SOME BUTTONS: the pause menu, the death screen, the end
    public partial class MenuCard : Overlay
    {
        readonly string _title;
        readonly string _line;
        readonly (string Key, Action Press, bool Enabled)[] _buttons;
        readonly bool _cancel;

        public MenuCard(string titleKey, string lineKey, bool cancel, params (string, Action, bool)[] buttons)
        {
            _title = titleKey;
            _line = lineKey;
            _cancel = cancel;
            _buttons = buttons;
            Width = 460;
        }

        protected override bool CanCancel => _cancel;

        protected override void Draw()
        {
            Body.AddChild(Ui.Title(_title));

            if (_line != null) Body.AddChild(Ui.Label(_line));

            foreach ((string key, Action press, bool enabled) in _buttons)
            {
                Action p = press;
                Button button = Ui.Button(key, () => p());
                button.Disabled = !enabled;
                Body.AddChild(button);
            }
        }
    }

    // THE ASK PROMPT (combat_ux.md): "Cast Shield? Armor Class 14 to 19. That turns the hit." Yes or
    // No, no timer
    public partial class AskCard : Overlay
    {
        public static readonly string TurnsTheHitKey =
            KeyConventions.Key(KeyConventions.UiNs, "reaction_prompt", "turns_the_hit", "name");

        readonly ReactionQuestion _question;
        readonly string _other;
        readonly Action<bool> _answer;
        bool _answered;

        public AskCard(ReactionQuestion question, string otherName, Action<bool> answer)
        {
            _question = question;
            _other = otherName ?? "";
            _answer = answer;
            Width = 420;
        }

        protected override bool CanCancel => true;

        protected override void Draw()
        {
            string name = Ui.Say(_question.NameKey);
            bool deflects = _question.Reaction.Deflects > 0;

            Body.AddChild(Ui.Title(_question.NameKey));
            Body.AddChild(deflects
                ? Ui.Label(_question.PromptKey, name, _question.ArmorClassNow, _question.ArmorClassWith)
                : Ui.Label(_question.PromptKey, name, _other));

            if (deflects && _question.TurnsTheHit) Body.AddChild(Ui.Label(TurnsTheHitKey));

            Body.AddChild(Ui.Row(12,
                Ui.Button(CombatHud.YesKey, () => Answer(true)),
                Ui.Button(CombatHud.NoKey, () => Answer(false))));
        }

        void Answer(bool yes)
        {
            if (_answered) return;

            _answered = true;
            _answer(yes);
            Close();
        }

        public override void _ExitTree()
        {
            // closed by Esc: that is No
            if (!_answered)
            {
                _answered = true;
                _answer(false);
            }
        }
    }
}
