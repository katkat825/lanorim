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

        // every default a quick character takes (QuickHero.Defaults): class, human, soldier, the first skills and
        // expertise, and (learn) the first spells
        static void Defaults(Creation making, string cls, bool learn)
        {
            // picking a class starts it over, so a class already picked (with spells learned) is kept
            if (making.Class?.Id != cls) making.Pick(GameState.Content.Class(cls));

            QuickHero.Defaults(making, GameState.Content, learn);
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

        // `--build <campaign|mine> [map]`: that map opened in the builder (mine: My maps), or a new one called `probe_map`
        // (`--template`: from that template), for a picture of the builder; `--map-probe`: the builder driven
        // (checks/check-mapbuilder.ps1), from the book's own page. true when either
        public static bool Building(Launch launch)
        {
            if (DevArgs.MapProbe)
            {
                MapBuilderProbe.AtLaunch(launch);
                return true;
            }

            if (DevArgs.Build == null) return false;

            MapPlace place = DevArgs.Build == "mine" ? MapBuilderPage.Library : null;

            if (place == null)
            {
                Game.Campaigns.Loaded campaign = GameState.Campaigns.Campaign(DevArgs.Build);

                if (campaign == null)
                {
                    GD.PushError($"map builder: no campaign '{DevArgs.Build}'");
                    return false;
                }

                place = MapPlace.OfCampaign(campaign.Folder, campaign.Manifest?.NameKey);
            }

            string map = DevArgs.BuildMap;

            if (map != null && MapFiles.Has(place, map)) MapBuilderPage.OpenMap(launch, place, map);
            else
            {
                // a template at its own size, as the book's page offers it
                MapDraft drawn = MapTemplates.Read(DevArgs.Template ?? MapTemplates.Standard);
                MapBuilderPage.Build(launch, new MapBuilderView(
                    MapBuilderView.Fresh(drawn?.Columns ?? 12, drawn?.Rows ?? 10, DevArgs.Template ?? MapTemplates.Standard),
                    place, map ?? "probe_map"));
            }

            return true;
        }
    }
}
