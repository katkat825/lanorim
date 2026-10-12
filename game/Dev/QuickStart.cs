using System;
using System.Linq;
using Content.Campaigns;
using Content.Creation;
using Content.Maps;
using Content.Screens;
using Content.Sheet;
using Game.Builder;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // THE LAUNCH SCREEN, STARTED FROM THE COMMAND LINE: a character with every default and straight to the table
    // (`--begin`, how check-play drives a whole campaign), the map builder on a map (`--build`, `--map-probe`), the
    // settings scrolled to the Controls (`--show controls`) and a creation page part filled in (`--show create`).
    // Was part of Launch (Launch.cs, Launch.Builder.cs) until cc_task_j Part 2; the dev registry calls it (DevHooks)
    public static class QuickStart
    {
        // a character made with every default, for the headless checks
        // `--learn mage_armor,shield`: those spells learned first, the rest of the picks as usual (a
        // probe or a picture that needs a particular spell)
        // `--gold`: the class's and the background's gold instead of their gear, so the run opens in the starting shop
        public static void Begin(Launch launch, string campaign, string cls, string learn = null, bool gold = false)
        {
            Package pack = GameState.Package(campaign);

            if (pack == null)
            {
                GD.PushError($"launch: no campaign '{campaign}' - found " +
                             string.Join(", ", GameState.Manifests.Select(m => m.Id)));
                launch.GetTree().Quit(1);
                return;
            }

            var creation = new CreationScreen(GameState.Content);
            var making = creation.Making;

            making.Pick(GameState.Content.Class(cls));

            foreach (string id in (learn ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (making.SpellChoices.FirstOrDefault(s => s.Id == id) is { } wanted) making.Learn(wanted);
                else GD.PushWarning($"launch: '{id}' is not a spell a level 1 {cls} can learn");

            Defaults(making, cls, learn: true);

            if (gold)
            {
                making.PickClassKit(KitChoice.Gold);
                making.PickBackgroundKit(KitChoice.Gold);
            }

            making.Call("Probe");

            Hero hero = making.Finish();

            if (hero == null)
            {
                GD.PushError("launch: " + string.Join("; ", making.Problems));
                launch.GetTree().Quit(1);
                return;
            }

            GD.Print($"launch  {hero} in {pack.Id}");

            launch.Play(pack, hero, 0);
        }

        // every default a quick character takes: class, human, soldier, the first skills and expertise,
        // and (learn) the first spells
        static void Defaults(Creation making, string cls, bool learn)
        {
            // picking a class starts it over, so a class already picked (with spells learned) is kept
            if (making.Class?.Id != cls) making.Pick(GameState.Content.Class(cls));
            making.Pick(GameState.Content.Kind("human"));
            making.Pick(GameState.Content.Background("soldier"));
            making.SuggestTraits();

            foreach (var skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList()) making.Train(skill);
            foreach (var skill in making.Skills.Take(making.ExpertisePicksLeft).ToList()) making.Master(skill);

            if (!learn) return;

            foreach (var spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);
        }

        // `--show create --page spells --class mage`: a creation page part filled in, for a picture of it
        public static void Creating(Launch launch, string page, string cls)
        {
            launch.ShowCreation(GameState.Manifests.FirstOrDefault()?.Id ?? "");

            if (page == null || !Core.Words.EnumWords.TryName(page, out Step step)) return;

            CreationScreen creation = Nodes.Under<CreationScreen>(launch).FirstOrDefault();
            if (creation == null) return;

            Defaults(creation.Making, cls ?? "mage", learn: false);
            creation.Open(step);
        }

        // `--show controls`: the settings page scrolled down to its Controls, for a picture of them
        public static void Controls(Launch launch)
        {
            launch.PageCalled("settings")?.Open(launch);

            ScrollContainer scroll = Nodes.Under<ScrollContainer>(launch.Body).FirstOrDefault();

            if (scroll == null) return;

            int frames = 0;

            // a few frames: the page lays itself out in the first
            launch.GetTree().ProcessFrame += Once;

            void Once()
            {
                if (++frames < 3) return;

                launch.GetTree().ProcessFrame -= Once;

                if (Nodes.Under<ControlsSection>(scroll).FirstOrDefault() is { } controls)
                    scroll.ScrollVertical = (int)controls.Position.Y;
            }
        }

        // `--build <campaign> [map]`: that campaign's map opened in the builder, or a new one called `probe_map`, for a
        // picture of the builder; `--map-probe`: the builder driven (checks/check-mapbuilder.ps1). true when either
        public static bool Building(Launch launch)
        {
            // the map builder, driven: a new map in a throwaway campaign, at the table
            if (DevArgs.MapProbe)
            {
                MapBuilderPage.Build(launch, new MapBuilderView(MapBuilderView.Fresh(12, 10), MapBuilderProbe.MakeCampaign(),
                                                                MapBuilderProbe.MapId));
                return true;
            }

            if (DevArgs.Build == null) return false;

            Game.Campaigns.Loaded campaign = GameState.Campaigns.Campaign(DevArgs.Build);

            if (campaign == null)
            {
                GD.PushError($"map builder: no campaign '{DevArgs.Build}'");
                return false;
            }

            string map = DevArgs.BuildMap;

            if (map != null && MapFiles.Ids(campaign.Folder).Contains(map)) MapBuilderPage.OpenMap(launch, campaign.Folder, map);
            else MapBuilderPage.Build(launch, new MapBuilderView(MapBuilderView.Fresh(12, 10), campaign.Folder, map ?? "probe_map"));

            return true;
        }
    }
}
