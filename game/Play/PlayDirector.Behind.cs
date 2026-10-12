using Content.Play;
using Godot;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- behind the screen (cc_task_i Part 1) ---------------------------------------------------------
        //
        // The screen used to rattle on every roll the GM made, every enemy's attack and damage included: dozens a
        // fight, so the suspense had gone by level 2. Now only a secret the run says was heard (an encounter table,
        // a <<roll ... heard>>) rattles, and not more than once every CampaignRun.RattleSeconds of play. The
        // caption comes only with the rattle, so it says exactly what a hearing player hears, and the companion's
        // Secret bark follows it, so it is as rare.

        readonly Cooldown _rattle = new Cooldown(CampaignRun.RattleSeconds);

        // seconds of play at this table
        double _played;

        // raised on the rules thread
        void HeardBehind() => MainQueue.Post(Rattle);

        void Rattle()
        {
            if (!_rattle.Take(_played)) return;

            Table.Captions?.Says(Game.Audio.Sound.Behind);
            Table.GmScreen?.Rattle();
            _mind?.HearsASecretRoll();

            GD.Print($"play    the screen rattles at {_played:0}s");
        }
    }
}
