using System;
using System.Collections.Generic;
using System.Linq;
using Content.Creation;
using Content.Maps;
using Content.Monsters;
using Core.Tables;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // THE "PLAY THIS MAP" PANEL (cc_task_l Part 5): the hero (a class and a level), who stands at each spawn (a goblin
    // unless changed), and the solo budget's readout for that level with its warnings, live; then Play, or Cancel. The
    // warnings never stop it; a map with problems or no spawn does, and the panel says why. Plain, from the Ui helpers:
    // its look is Kathleen's (#5)
    public partial class TrialPanel : PanelContainer
    {
        public MapBuilder Builder { get; set; }

        public MapTrial Trial { get; private set; }

        // what the panel offers in its lists, kept so a probe can choose by id
        public IReadOnlyList<string> Classes { get; private set; }

        public IReadOnlyList<Monster> Roster { get; private set; }

        Label _readout;
        VBoxContainer _warnings;
        Button _play;
        Label _why;

        public event Action Closed;

        public override void _Ready()
        {
            Name = "Trial";
            Trial = new MapTrial(Builder.View.Draft, GameState.Content);
            Classes = QuickHero.Classes(GameState.Content).ToList();
            Roster = Trial.Roster.ToList();

            int gap = BuilderLayout.Current.Gap;
            VBoxContainer column = Ui.Column(gap, Ui.Title(MapTrial.TrialKey));

            column.AddChild(Ui.Row(gap, Ui.Label(MapTrial.HeroKey), HeroChoice(), Ui.Label(MapTrial.LevelKey), LevelChoice()));

            foreach (int slot in Trial.Slots)
                column.AddChild(Ui.Row(gap, Ui.Label(MapTrial.AtSpawnKey, slot), MonsterChoice(slot)));

            column.AddChild(_readout = Ui.Plain(""));
            column.AddChild(_warnings = Ui.Column(gap / 2));
            column.AddChild(_why = Ui.Plain(""));

            _play = Ui.Button(MapTrial.FightKey, () => Builder.Play(Trial));
            _play.Name = "PlayTrial";
            column.AddChild(Ui.Row(gap, _play, Ui.Button(Content.Screens.MapBuilderView.CancelKey, Close)));

            AddChild(column);
            Refresh();
            Ui.FocusLater(_play);
        }

        OptionButton HeroChoice()
        {
            var classes = new OptionButton { Name = "TrialClass" };

            foreach (string id in Classes) classes.AddItem(Ui.Say(GameState.Content.Class(id).NameKey));

            classes.Selected = Math.Max(0, Classes.ToList().IndexOf(Trial.Class));
            classes.ItemSelected += i =>
            {
                Trial.Class = Classes[(int)i];
                Refresh();
            };

            return classes;
        }

        SpinBox LevelChoice()
        {
            var level = new SpinBox { Name = "TrialLevel", MinValue = 1, MaxValue = 20, Value = Trial.Level, Rounded = true };
            level.ValueChanged += v =>
            {
                Trial.Level = (int)v;
                Refresh();
            };
            return level;
        }

        OptionButton MonsterChoice(int slot)
        {
            var monsters = new OptionButton { Name = $"TrialSpawn{slot}" };

            foreach (Monster monster in Roster) monsters.AddItem(Ui.Say(monster.NameKey));

            monsters.Selected = Math.Max(0, Roster.ToList().FindIndex(m => m.Id == Trial.At(slot)));
            monsters.ItemSelected += i =>
            {
                Trial.Put(slot, Roster[(int)i].Id);
                Refresh();
            };

            return monsters;
        }

        // the readout and the warnings, again, after any choice
        void Refresh()
        {
            _readout.Text = Game.Access.AccessDesk.Words(Trial.Readout);

            Ui.Clear(_warnings);
            foreach (Content.Screens.Said warning in Trial.Warnings)
                _warnings.AddChild(Ui.Plain(Game.Access.AccessDesk.Words(warning)));

            string refused = Trial.Refusal;
            _play.Disabled = refused != null;
            _why.Text = refused == null ? "" : Ui.Say(refused);
        }

        void Close()
        {
            Closed?.Invoke();
            QueueFree();
        }
    }
}
