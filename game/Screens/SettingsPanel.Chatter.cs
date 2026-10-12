using System;
using Content.Companions;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    public partial class SettingsPanel
    {
        // HOW MUCH THE COMPANION SAYS (cc_task_i 2.5): Off, Some or All (the default). Some halves the barks that come
        // round most often (a fight won, a natural 1, a natural 20); the companion still reacts with its body either way
        void Chatters(GameSettings settings)
        {
            var choice = new OptionButton { FocusMode = FocusModeEnum.All };

            foreach (CompanionChatter one in Enum.GetValues<CompanionChatter>())
                choice.AddItem(Ui.Say(GameSettings.ChatterKey(one)), (int)one);

            choice.Selected = (int)settings.Chatter;
            choice.ItemSelected += index =>
            {
                settings.Chatter = (CompanionChatter)(int)index;
                GameState.SaveSettings();
            };

            AddChild(Ui.Row(12, Ui.Label(GameSettings.ChatterTitleKey), choice));
        }
    }
}
