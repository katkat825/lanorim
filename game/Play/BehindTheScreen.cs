using System;
using Content.Play;
using Godot;

namespace Game.Play
{
    // BEHIND THE SCREEN (cc_task_i Part 1). The screen used to rattle on every roll the GM made, every enemy's attack and
    // damage included: dozens a fight, so the suspense had gone by level 2. Now only a secret the run says was heard (an
    // encounter table, a <<roll ... heard>>) rattles, and not more than once every CampaignRun.RattleSeconds of play.
    // The caption comes only with the rattle, so it says exactly what a hearing player hears, and the companion's
    // Secret bark follows it (Rattled), so it is as rare.
    //
    // Was PlayDirector.Behind.cs; its own helper, with its own cooldown, since cc_task_j Part 1. The director hands it
    // the table and its clock of seconds of play, and hears it on CampaignRun.Heard
    public sealed class BehindTheScreen
    {
        readonly Cooldown _rattle = new Cooldown(CampaignRun.RattleSeconds);
        readonly Game.Table.Table _table;
        readonly Func<double> _played;

        public BehindTheScreen(Game.Table.Table table, Func<double> played)
        {
            _table = table;
            _played = played;
        }

        // the screen rattled: the companion hears it
        public event Action Rattled;

        // a secret was heard. raised on the rules thread
        public void Heard() => MainQueue.Post(Rattle);

        void Rattle()
        {
            double played = _played();

            if (!_rattle.Take(played)) return;

            _table.Captions?.Says(Game.Audio.Sound.Behind);
            _table.GmScreen?.Rattle();
            Rattled?.Invoke();

            GD.Print($"play    the screen rattles at {played:0}s");
        }
    }
}
