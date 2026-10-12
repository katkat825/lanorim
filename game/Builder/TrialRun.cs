using System.Linq;
using Content.Campaigns;
using Content.Creation;
using Content.Maps;
using Content.Sheet;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // PLAY THIS MAP, STARTED (cc_task_l Part 5): the trial's throwaway campaign written to user://map_trial (MapTrial.Write),
    // read back as a campaign is, the hero made with every default at the trial's class and level (QuickHero, as
    // `--begin` makes one), and the table comes up to play it. PlayDirector sends the table back here when the fight
    // ends (GameState.Trying); the map in the builder is the same object, as it was. Nothing touches a real campaign
    public static class TrialRun
    {
        // false, with a notice on the builder, when it couldn't start
        public static bool Start(MapBuilder builder, MapTrial trial)
        {
            if (trial.Refusal is string why)
            {
                builder.Notice(Ui.Say(why));
                return false;
            }

            string folder = ProjectSettings.GlobalizePath($"{GameState.TrialFolder}/{MapTrial.CampaignId}");
            string couldNot = trial.Write(folder);

            if (couldNot != null)
            {
                builder.Notice(Ui.Say(MapTrial.CouldNotKey, couldNot));
                return false;
            }

            Package pack = Package.Read(folder);

            if (!pack.Maps.ContainsKey(MapTrial.MapId))
            {
                string problems = string.Join("; ", pack.Problems.Where(p => !p.IsACaution).Select(p => p.What));
                GD.PushError("trial: the map's campaign wouldn't load - " + problems);
                builder.Notice(Ui.Say(MapTrial.CouldNotKey, problems));
                return false;
            }

            Hero hero = QuickHero.Make(GameState.Content, trial.Class, trial.Level,
                                       Ui.Say(GameState.Content.Class(trial.Class)?.NameKey ?? trial.Class));

            GD.Print($"trial   {hero} on {builder.View.Id}: " +
                     string.Join(", ", trial.Slots.Select(s => $"{s} {trial.At(s)}")));

            GameState.Try(builder.View, pack, hero);
            Scenes.Go(builder.GetTree(), Scenes.Table);
            return true;
        }
    }
}
