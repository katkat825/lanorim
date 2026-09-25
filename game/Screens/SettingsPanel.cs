using System;
using System.Linq;
using Content.Combat;
using Content.Screens;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // SETTINGS (Tier 2.8/3b), the game half: enemy-turn speed, skip the physical dice, the camera,
    // and a policy for each reaction a hero can have. Saved the moment it changes. The Access half
    // (text size, contrast, captions, read-aloud, key bindings) is game/Access and sits beside it.
    public partial class SettingsPanel : VBoxContainer
    {
        // the reactions a hero can be offered, by id - what the Reactions list shows
        public static readonly string[] Reactions =
        {
            "opportunity_attack", "shield", "counterspell", "hellish_rebuke", "divine_smite", "searing_smite",
        };

        public event Action Done;

        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 10);
            Draw();
        }

        void Draw()
        {
            Ui.Clear(this);

            GameSettings settings = GameState.Settings;

            AddChild(Ui.Title(GameSettings.TitleKey));
            AddChild(Ui.Label(GameSettings.GameTabKey));

            var speed = new OptionButton { FocusMode = FocusModeEnum.All };

            foreach (CombatSpeed one in Enum.GetValues<CombatSpeed>())
                speed.AddItem(Ui.Say(GameSettings.SpeedKey(one)), (int)one);

            speed.Selected = (int)settings.EnemySpeed;
            speed.ItemSelected += index =>
            {
                settings.EnemySpeed = (CombatSpeed)(int)index;
                GameState.SaveSettings();
            };

            AddChild(Ui.Row(12, Ui.Label(GameSettings.EnemySpeedKey), speed));

            AddChild(Toggle(GameSettings.SkipDiceKey, settings.SkipPhysicalDice, on => settings.SkipPhysicalDice = on));
            AddChild(Toggle(GameSettings.FollowKey, settings.FollowEnemies, on => settings.FollowEnemies = on));

            AddChild(Ui.Label(GameSettings.ReactionsKey));

            foreach (string reaction in Reactions)
            {
                string one = reaction;
                var policy = new OptionButton { FocusMode = FocusModeEnum.All };

                foreach (ReactionPolicy each in ReactionPolicies.All)
                    policy.AddItem(Ui.Say(each.NameKey()), (int)each);

                policy.Selected = ReactionPolicies.All.ToList().IndexOf(settings.Reactions.For(one));
                policy.ItemSelected += index =>
                {
                    settings.Reactions.Set(one, ReactionPolicies.All[(int)index]);
                    GameState.SaveSettings();
                };

                AddChild(Ui.Row(12, Ui.Label(ReactionName(one)), new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill },
                                policy));
            }

            AddChild(Ui.Button(ScreenWords.Back, () => Done?.Invoke()));
        }

        // a reaction's name is its spell's, or the opportunity attack's own
        static string ReactionName(string id) =>
            id == "opportunity_attack"
                ? Core.Localization.KeyConventions.Key(Core.Localization.KeyConventions.UiNs, "reaction", id, "name")
                : Core.Localization.KeyConventions.SpellName(id);

        CheckBox Toggle(string key, bool on, Action<bool> set)
        {
            var box = new CheckBox { Text = Ui.Say(key), ButtonPressed = on, FocusMode = FocusModeEnum.All };

            box.Toggled += value =>
            {
                set(value);
                GameState.SaveSettings();
            };

            return box;
        }
    }
}
