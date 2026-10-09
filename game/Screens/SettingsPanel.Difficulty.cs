using System;
using Content.Screens;
using Content.SoloPlay;
using Game.Play;
using Godot;

namespace Game.Screens
{
    public partial class SettingsPanel
    {
        // HOW HARD THE FIGHTS ARE (cc_task_h 3.3): Story, Standard or Hard, changeable at any time, with a line under
        // it saying what the chosen one is for. Only the enemies change; Standard is the game as it always was
        void Difficulties(GameSettings settings)
        {
            var choice = new OptionButton { FocusMode = FocusModeEnum.All };

            foreach (PlayDifficulty one in Enum.GetValues<PlayDifficulty>())
                choice.AddItem(Ui.Say(GameSettings.DifficultyKey(one)), (int)one);

            Label about = Ui.Plain(Ui.Say(GameSettings.DifficultyAboutKey(settings.Difficulty)));

            choice.Selected = (int)settings.Difficulty;
            choice.ItemSelected += index =>
            {
                settings.Difficulty = (PlayDifficulty)(int)index;
                about.Text = Ui.Say(GameSettings.DifficultyAboutKey(settings.Difficulty));
                GameState.SaveSettings();
            };

            AddChild(Ui.Row(12, Ui.Label(GameSettings.DifficultyTitleKey), choice));
            AddChild(about);
        }
    }
}
