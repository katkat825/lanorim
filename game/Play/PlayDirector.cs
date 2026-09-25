using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Play;
using Content.Saves;
using Content.Screens;
using Core.Characters;
using Core.Combat;
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
        CombatDirector _combat;
        CanvasLayer _layer;
        Control _ui;
        DialoguePopup _dialogue;
        Label _prompt;
        Overlay _overlay;

        bool _auto;
        bool _autoStory;
        string _startAt;
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

            AddChild(new MainQueue { Name = "MainQueue" });

            _rules = new RulesThread();

            _dice = new TrayDice(Table.Tray, GameState.Gm)
            {
                AutoThrow = autoDice,
                Skip = () => GameState.Settings.SkipPhysicalDice,
            };
            _dice.Asked += _ => Prompt(true);
            _dice.Landed += _ => Prompt(false);
            GameState.HeroDice = _dice;

            BuildUi();

            _combat = new CombatDirector
            {
                Name = "Combat",
                Board = Table.Board,
                Camera = Table.Camera,
                Screen = Table.GmScreen,
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

            Table.GmScreen?.Wear(Run.Pack.Manifest?.GmScreen);
            LayChapterMap();

            bool starting = GameState.Starting;
            string from = _startAt;
            _rules.Post(() =>
            {
                if (starting) Run.Start(from);
                else Run.Continue();
            }, Refresh);

            GD.Print($"play    {Run.Pack.Id}: {Run.Hero}");
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

            _prompt = new Label
            {
                Name = "Prompt",
                ThemeTypeVariation = "HudLabel",
                Text = Ui.Say(ScreenWords.ThrowPrompt),
                AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -380, OffsetTop = 70, OffsetRight = -20,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visible = false,
            };
            _ui.AddChild(_prompt);
        }

        void Prompt(bool waiting) => _prompt.Visible = waiting && !_auto;

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
            }
        }

        // --- what the run says now ------------------------------------------------------------------

        void Refresh()
        {
            if (_rules.Busy || _overlay != null) return;

            // a level the story just gave: the level-up card first, whatever comes next
            if (Run.Hero.Level > _level || Run.Hero.PendingImprovements > 0)
            {
                var view = new LevelUpView(Run.Hero, _level, _maxHp);

                if (_auto)
                {
                    while (Run.Hero.PendingImprovements > 0) view.TakeSuggested();
                }
                else
                {
                    Open(new LevelUpScreen(view), () => { Mark(); Refresh(); });
                    return;
                }

                Mark();
            }

            switch (Run.Now)
            {
                case Scene.Line:
                case Scene.Choice:
                    _dialogue.Show(new DialogueView(Run, GameState.Companion));

                    if (_autoStory)
                    {
                        if (Run.Now == Scene.Line) _dialogue.Continue();
                        else _dialogue.Choose(0);
                    }

                    break;

                case Scene.Fight:
                    _dialogue.Visible = false;
                    if (!_fighting) StartFight();
                    break;

                case Scene.Shop:
                    _dialogue.Visible = false;
                    OpenShop();
                    break;

                case Scene.Dead:
                    _dialogue.Visible = false;
                    Died();
                    break;

                case Scene.Over:
                    _dialogue.Visible = false;
                    TheEnd();
                    break;
            }
        }

        void Mark()
        {
            _level = Run.Hero.Level;
            _maxHp = Run.Hero.Actor.Health.Maximum;
        }

        void Open(Overlay overlay, Action closed = null)
        {
            _overlay = overlay;
            overlay.Closed += () =>
            {
                _overlay = null;
                closed?.Invoke();
            };
            _ui.AddChild(overlay);
        }

        // --- a fight ---------------------------------------------------------------------------------

        void StartFight()
        {
            _fighting = true;

            var asker = new TableAsker(this, _auto);
            var chooser = new PolicyChooser(GameState.Settings.Reactions, asker);

            _rules.Post(() =>
            {
                var log = new FightLog();
                log.Listen(GameState.Resolver);

                BoardShow show = _combat.Show(a => ReferenceEquals(a, Run.Hero.Actor));
                log.Wrote += show.Log;

                Battle battle = Run.BattleFor(GameState.Resolver, chooser, new Observers(log, show));

                if (battle == null)
                {
                    // a fight with no map to stand on reads as won (the author sees why in the log)
                    GD.PushWarning("play: the fight has no map - counted as won");
                    Run.EndFight(Outcome.HeroesWon);
                    MainQueue.Post(() => _fighting = false);
                    return;
                }

                var places = battle.Fight.Actors
                                   .Select(a => (Actor: a, At: battle.Fight.Field.Where(a)))
                                   .Where(p => p.At.HasValue)
                                   .Select(p => (p.Actor, p.At.Value))
                                   .ToList();

                IReadOnlyList<Content.Maps.Prop> props = Run.Pack.PropsOn(Run.Fight?.MapId);
                MainQueue.Post(() => _combat.Lay(battle, places, props));

                var session = new CombatSession(battle, GameState.Content.Items, GameState.Content.Forms);
                session.Start();

                MainQueue.Post(() => _combat.Started(session));
            }, Refresh);
        }

        void FightOver(Outcome outcome)
        {
            if (outcome == Outcome.HeroesLost) _deaths++;

            _rules.Post(() => Run.EndFight(outcome == Outcome.Open ? Outcome.Fled : outcome), () =>
            {
                _fighting = false;
                _combat.Clear();
                LayChapterMap();
                Refresh();
            });
        }

        // the question behind a reaction set to Ask: a card, and the rules wait for the answer
        sealed class TableAsker : IReactionAsker
        {
            readonly PlayDirector _director;
            readonly bool _auto;

            public TableAsker(PlayDirector director, bool auto)
            {
                _director = director;
                _auto = auto;
            }

            public bool Ask(ReactionQuestion question)
            {
                if (_auto || MainQueue.OnMain) return true;

                return MainQueue.Ask<bool>(answer =>
                {
                    string other = LogText.NameOf(question.Other, _director._combat.Battle, _director.Run.Hero.Name);
                    _director._ui.AddChild(new AskCard(question, other, answer));
                    return true;
                });
            }
        }

        // --- a shop ------------------------------------------------------------------------------------

        void OpenShop()
        {
            if (_auto)
            {
                Do(() => Run.LeaveShop());
                return;
            }

            var view = new PackView(Run.Hero, Run.Items, Run.Shop.Open(Run.Items));
            Open(new PackScreen(view, GameState.Resolver), () => Do(() => Run.LeaveShop()));
        }

        // --- death and the end -------------------------------------------------------------------

        void Died()
        {
            var death = new DeathView(GameState.Saves, Run.Pack.Id, Run.Slot);

            if (_auto)
            {
                GD.Print($"play    died ({_deaths})");

                if (_deaths > 12 || !death.CanReload)
                {
                    GD.PrintErr("play    FAILED - died too often to finish");
                    Quit(1);
                    return;
                }

                Reload(death.Reload);
                return;
            }

            Open(new MenuCard(DeathView.TitleKey, death.CanReload ? null : DeathView.NoSaveKey, false,
                (DeathView.ReloadKey, () => Reload(death.Reload), death.CanReload),
                (DeathView.BookKey, ToTheBook, true)));
        }

        void Reload(SaveShelf.Saved saved)
        {
            if (saved == null || GameState.Resume(saved.Game) == null) return;

            // the table comes up again around the loaded run
            GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
        }

        void TheEnd()
        {
            if (_auto)
            {
                GD.Print($"play    the end - {Run.Hero}, {_deaths} deaths");
                Quit(0);
                return;
            }

            Open(new MenuCard(ScreenWords.TheEnd, ScreenWords.TheEndBlurb, false,
                (ScreenWords.ToTheBook, ToTheBook, true)));
        }

        void ToTheBook()
        {
            GameState.Leave();
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://launch.tscn");
        }

        // --- the pause menu -------------------------------------------------------------------------

        void Pause()
        {
            bool idle = !_rules.Busy;

            Open(new MenuCard(ScreenWords.PauseTitle, null, true,
                (ScreenWords.Resume, () => _overlay?.Close(), true),
                (ScreenWords.Save, SaveNow, idle),
                (ScreenWords.Sheet, () => Swap(new SheetScreen(Content.Sheet.SheetView.Of(Run.Hero))), idle),
                (ScreenWords.Pack, () => Swap(new PackScreen(new PackView(Run.Hero, Run.Items), GameState.Resolver)), idle),
                (CampaignBook.SettingsKey, () => Swap(SettingsCard()), true),
                (ScreenWords.ToTheBook, ToTheBook, true)), Refresh);
        }

        void Swap(Overlay next)
        {
            Overlay was = _overlay;
            _overlay = null;
            was?.QueueFree();
            Open(next, Refresh);
        }

        Overlay SettingsCard()
        {
            var card = new SettingsOverlay();
            return card;
        }

        void SaveNow()
        {
            string path = Run.Save(SaveKind.Manual);
            GD.Print("play    saved " + path);
            _overlay?.Close();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

            // Space: throw what the tray is waiting on; otherwise the fight's "go on"
            if (key.IsActionPressed("throw_dice"))
            {
                if (_dice.Waiting != null && !_dice.Thrown)
                {
                    _dice.Go();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (_overlay == null && _combat.GoOn())
                {
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // never the table's demonstration throw while a campaign is played
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("ui_cancel") && _overlay == null)
            {
                Pause();
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Process(double delta)
        {
            // a throw asked for while the tray was still settling the last one goes as soon as it can
            if (_auto && _dice.Waiting != null && !_dice.Thrown) _dice.Go();

            if (_auto)
            {
                _clock += delta;

                // every half minute, where it is - a stalled headless run says what it waits on
                if ((int)(_clock / 30) != (int)((_clock - delta) / 30))
                    GD.Print($"play    {_clock:0}s: {Run.Now}, rules {(_rules.Busy ? "busy" : "idle")}, " +
                             $"dice {(_dice.Waiting == null ? "-" : _dice.Thrown ? "thrown" : "waiting")}, " +
                             $"tray {(Table.Tray.IsThrowing ? "throwing" : "still")}, {_combat.Describe()}");

                if (_clock > 900)
                {
                    GD.PrintErr($"play    FAILED - still going after {_clock:0} s, at {Run}");
                    Quit(1);
                }
            }

            if (_rules.Failed != null && !_failed)
            {
                _failed = true;
                GD.PrintErr("play    FAILED - " + _rules.Failed);
                if (_auto) Quit(1);
            }
        }
    }

    // settings from the pause menu: the same panel as the book's, in a card
    public partial class SettingsOverlay : Overlay
    {
        protected override void Draw()
        {
            var panel = new SettingsPanel();
            panel.Done += Close;
            Body.AddChild(Ui.Scroll(panel, 520));
        }
    }
}
