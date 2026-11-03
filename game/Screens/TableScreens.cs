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
    // THE OVERLAYS THE TABLE OPENS OVER ITSELF (Tier 3b): each a centred card built from a view model
    // in content/Screens, closed by its own button or Esc. The table stays underneath; the rules are
    // idle while one is open.
    public abstract partial class Overlay : CenterContainer
    {
        protected VBoxContainer Body { get; private set; }

        public event Action Closed;

        protected float Width { get; set; } = 620;

        // A PAGE THE SIZE OF THE WINDOW (the character sheet, cc_task_cs 1.3): no scroll of its own, its body the window
        // less a margin, so what doesn't fit runs off the screen where check-layout sees it rather than scrolling away
        // out of sight. A section that may grow (a long spell list) scrolls inside itself
        protected bool FillsTheScreen { get; set; }

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;

            Body = Ui.Column(10);
            Body.CustomMinimumSize = new Vector2(Ui.Px(Width), 0);

            if (FillsTheScreen)
            {
                AddChild(Ui.Panel(Body));
                Fill();
                Draw();
                return;
            }

            // A CARD TALLER THAN THE WINDOW SCROLLS inside it rather than running off the bottom
            // (cc_task_ui-issues-9-30.md 2: the sheet was 1205 tall on a 1080 canvas)
            _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _scroll.AddChild(Body);

            AddChild(Ui.Panel(_scroll));
            Draw();
        }

        ScrollContainer _scroll;

        // the room a card may take: the window's height less a margin top and bottom
        static float Margin => Ui.Px(24);

        // the window less a margin each side and the panel's own padding
        void Fill()
        {
            Vector2 window = GetViewportRect().Size;
            var size = new Vector2(window.X - 2 * Margin - 2 * 24, window.Y - 2 * Margin - 2 * 24);

            if (Body.CustomMinimumSize != size) Body.CustomMinimumSize = size;
        }

        public override void _Process(double delta)
        {
            if (FillsTheScreen) Fill();

            if (_scroll == null) return;

            float most = GetViewportRect().Size.Y - 2 * Margin - 2 * 24;
            float wants = Body.GetCombinedMinimumSize().Y;

            var size = new Vector2(Body.CustomMinimumSize.X, Mathf.Min(wants, most));
            if (_scroll.CustomMinimumSize != size) _scroll.CustomMinimumSize = size;
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

                // what each one does, written under it rather than on hover
                foreach (var feature in _view.NewFeatures)
                {
                    Body.AddChild(Ui.Label(feature.NameKey));

                    Label does = Ui.Label(feature.DescriptionKey);
                    does.ThemeTypeVariation = "CardLabel";
                    Body.AddChild(does);
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
            Body.AddChild(Ui.Why(!_view.Done, _view.DoneWhyNotKey));
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
