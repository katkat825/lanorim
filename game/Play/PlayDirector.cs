using System;
using System.Linq;
using Content.Campaigns;
using Content.Play;
using Core.Space;
using Game.Screens;
using Godot;

namespace Game.Play
{
    // THE TABLE WHILE A CAMPAIGN IS PLAYED (Tier 3b). The story on the dialogue card; a fight handed to
    // the combat director and back; a shop, a level-up, the pause menu, death and the end as cards over
    // the table. The run itself (content/Play/CampaignRun) is the game loop; this is its presentation,
    // and every call into it goes to the rules thread, because any of them can throw the hero's dice.
    //
    // `-- --auto` plays itself: first choices, the AutoPlayer in fights, dice thrown as soon as asked.
    // With `--begin` on the launch screen that is a whole campaign played headless (check-play.ps1).
    public partial class PlayDirector : Node
    {
        public Game.Table.Table Table { get; set; }

        RulesThread _rules;
        TrayDice _dice;
        Game.Tray.TrayLift _lift;
        CombatDirector _combat;
        CanvasLayer _layer;
        Control _ui;
        DialoguePopup _dialogue;
        Label _prompt;
        Label _notice;
        Overlay _overlay;

        bool _auto;
        bool _autoStory;
        bool _pauseOnTurn;
        string _startAt;
        string _gmScreen;
        bool _fighting;
        bool _failed;
        int _level;
        int _maxHp;
        static int _deaths;
        double _clock;

        CampaignRun Run => GameState.Run;

        public override void _Ready()
        {
            string[] args = OS.GetCmdlineUserArgs();
            _auto = args.Contains("--auto");

            // the dice thrown as soon as asked, without the rest of --auto: for screenshots of a
            // real turn, and for a player who wants the tray to do it
            bool autoDice = _auto || args.Contains("--autodice");

            // the story's lines and choices taken as they come, the fights left to the player
            _autoStory = _auto || args.Contains("--autostory");

            // start the story at a node other than the chapter's first (a probe, a screenshot)
            int at = Array.IndexOf(args, "--start");
            _startAt = at >= 0 && at + 1 < args.Length ? args[at + 1] : null;

            // the pause menu opened on the hero's first turn in a fight: for a screenshot of it
            _pauseOnTurn = args.Contains("--pause");

            // `--gm-screen plains`: another of the GM screens, for a picture of each under the lamp
            int skin = Array.IndexOf(args, "--gm-screen");
            _gmScreen = skin >= 0 && skin + 1 < args.Length ? args[skin + 1] : null;

            AddChild(new MainQueue { Name = "MainQueue" });

            _rules = new RulesThread();

            _dice = new TrayDice(Table.Tray, GameState.Gm)
            {
                AutoThrow = autoDice,
                Skip = () => GameState.Settings.SkipPhysicalDice,
            };
            // the tray comes to the player for their own dice, and goes back once they're read
            _lift = Table.Lift;
            _dice.Ready = () => _lift.IsStill;

            _dice.Asked += _ =>
            {
                if (GameState.Settings.BringTrayToMe) _lift.Rise();
                _mind?.Watches();
                Prompt(true);
            };
            _dice.Landed += _ =>
            {
                _lift.Settle();
                Prompt(false);
            };
            GameState.HeroDice = _dice;

            BuildUi();

            _combat = new CombatDirector
            {
                Name = "Combat",
                Board = Table.Board,
                Camera = Table.Camera,
                Screen = Table.GmScreen,
                Companion = Table.Companion,
                Rules = _rules,
                Dice = _dice,
                Auto = _auto,
                HeroName = Run.Hero.Name,
            };
            AddChild(_combat);
            _combat.Mount(_ui);
            _combat.Finished += FightOver;

            // the GM's rolls behind the screen: heard, captioned, never shown
            GameState.Resolver.Rolled += roll =>
            {
                if (!roll.OnTheTable)
                    MainQueue.Post(() =>
                    {
                        Table.Captions?.Says(Game.Audio.Sound.Behind);
                        Table.GmScreen?.Rattle();
                    });
            };

            _level = Run.Hero.Level;
            _maxHp = Run.Hero.Actor.Health.Maximum;

            Table.GmScreen?.Wear(_gmScreen ?? Run.Pack.Manifest?.GmScreen);
            WakeCompanion();
            LayChapterMap();

            bool starting = GameState.Starting;
            string from = _startAt;
            _rules.Post(() =>
            {
                if (starting) Run.Start(from);
                else Run.Continue();
            }, Refresh);

            GD.Print($"play    {Run.Pack.Id}: {Run.Hero}");

            CheckLayoutWhenAsked();
        }

        public override void _ExitTree()
        {
            _dice?.Abandon();
            _rules?.Dispose();

            if (GameState.HeroDice == _dice) GameState.HeroDice = null;

        }

        bool _quitting;

        void Quit(int code)
        {
            _quitting = true;
            GameState.ForgetProbeSaves();
            GetTree().Quit(code);
        }

        void BuildUi()
        {
            _layer = new CanvasLayer { Name = "Ui" };
            AddChild(_layer);

            _ui = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
            _ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _layer.AddChild(_ui);

            _dialogue = new DialoguePopup { Name = "Dialogue" };
            _dialogue.Continue = () => Do(() => Run.Next());
            _dialogue.Choose = option => Do(() => Run.Choose(option));
            _ui.AddChild(_dialogue);

            // THE TOP RIGHT CORNER (cc_task_ui-issues-9-30.md 2): the turn hint, the tray's caption and the
            // notices, one under another in a column anchored to the corner and HudLayout.HintWidth wide,
            // each wrapping inside it. They were three labels at fixed offsets, and the hint ran into
            // the turn strip; the strip now keeps out of this corner (CombatHudUi)
            HudLayout layout = HudLayout.Current;

            var corner = new VBoxContainer
            {
                Name = "Corner",
                AnchorLeft = 1,
                AnchorRight = 1,
                OffsetLeft = -(layout.HintWidth + layout.EdgeMargin),
                OffsetRight = -(layout.EdgeMargin + layout.TextInset),
                OffsetTop = layout.EdgeMargin + layout.TextInset,
                GrowHorizontal = Control.GrowDirection.Begin,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            corner.AddThemeConstantOverride("separation", 8);
            _ui.AddChild(corner);

            // nothing said E turns the table, so turning it looked like a fault
            // (cc_ui_issues_9-25-2026.md): the keys, as they are bound
            corner.AddChild(CornerLabel("TurnHint", Ui.Say(ScreenWords.TurnHint, Game.Access.Keyboard.Named("turn_left"),
                                                             Game.Access.Keyboard.Named("turn_right"), Game.Access.Keyboard.Named("zoom_out"),
                                                             Game.Access.Keyboard.Named("zoom_in")), true, 0.7f));

            _prompt = CornerLabel("Prompt", Ui.Say(ScreenWords.ThrowPrompt), false);
            corner.AddChild(_prompt);

            _notice = CornerLabel("Notice", "", false);
            corner.AddChild(_notice);
        }

        static Label CornerLabel(string name, string text, bool visible, float alpha = 1f) =>
            new Label
            {
                Name = name,
                ThemeTypeVariation = "HudLabel",
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Right,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Visible = visible,
                Modulate = new Color(1f, 1f, 1f, alpha),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };

        // a call into the run, off the main thread, then the screen redrawn from what it says now
        void Do(Action call)
        {
            _dialogue.Visible = false;
            _rules.Post(call, Refresh);
        }

        void LayChapterMap()
        {
            Package pack = Run.Pack;
            string id = Run.MapId;

            if (string.IsNullOrEmpty(id))
                id = pack.Manifest?.Chapters.FirstOrDefault()?.Maps.FirstOrDefault() ?? pack.Maps.Keys.FirstOrDefault();

            if (id != null && pack.Maps.TryGetValue(id, out MapLayout map))
            {
                Table.Board.ClearAll();
                Table.Board.Lay(map);
                Table.Board.Dress(pack.PropsOn(id));
                Table.GmScreen?.StandBehind(Table.Board);
                Table.Companion?.StandBy(Table.Board, Table.GmScreen);
            }
        }
    }

}
