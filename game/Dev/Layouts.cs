using System.Linq;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // WHAT `--layout` LAYS OUT (checks/check-layout.ps1, cc_task_ui-issues-9-30.md 2): the LayoutProbe for the launch
    // screen (every launch screen, the book's pages from BookMenu among them, so a new page is checked too) or for the
    // table in play (at the hero's first turn: the HUD, the pause menu, the sheet and the pack, with the board's place
    // on screen so the log and the strip can be held off it). Was in Launch.cs and PlayDirector.Layout.cs until
    // cc_task_j Part 2
    public static class Layouts
    {
        public static LayoutProbe OfLaunch(Launch launch)
        {
            var probe = new LayoutProbe { Name = "LayoutProbe" };

            probe.Screens.Add(("title", launch.ShowTitle));
            probe.Screens.Add(("book", launch.ShowBook));

            foreach (IBookPage page in launch.Pages)
            {
                IBookPage one = page;
                probe.Screens.Add((one.Id, () => one.Open(launch)));
            }

            // the Workshop's own screens past its hub (cc_task_m): the wizard's three pages, and a campaign's page at each tab
            // (the game's sample, opened and never saved)
            foreach (Content.Workshop.WizardPage page in System.Enum.GetValues<Content.Workshop.WizardPage>())
            {
                Content.Workshop.WizardPage one = page;
                probe.Screens.Add(($"wizard {Core.Words.EnumWords.Id(one)}", () => QuickStart.Wizard(launch, one)));
            }

            foreach (Content.Workshop.WorkshopTab tab in System.Enum.GetValues<Content.Workshop.WorkshopTab>())
            {
                Content.Workshop.WorkshopTab one = tab;
                probe.Screens.Add(($"campaign {Core.Words.EnumWords.Id(one)}", () =>
                {
                    string folder = System.IO.Path.Combine(Game.Campaigns.CampaignFolders.Local(), "sample_millbrook");
                    Game.Workshop.CampaignScreen.Show(launch, Content.Workshop.CampaignEditor.Open(folder), one, fresh: false);
                    GameState.Editing = null;
                }));
            }

            // the story editor (cc_task_o): a board of 40 cards (the sample's five and 35 more, never saved), a card open with
            // a note and a check; and the test hero panel (cc_task_q)
            Game.Workshop.CampaignScreen Sample(Content.Workshop.WorkshopTab tab)
            {
                string folder = System.IO.Path.Combine(Game.Campaigns.CampaignFolders.Local(), "sample_millbrook");
                Game.Workshop.CampaignScreen page = Game.Workshop.CampaignScreen.Show(launch, Content.Workshop.CampaignEditor.Open(folder), tab, fresh: false);
                GameState.Editing = null;
                return page;
            }

            probe.Screens.Add(("story 40 cards", () =>
            {
                Game.Workshop.CampaignScreen page = Sample(Content.Workshop.WorkshopTab.Story);
                for (int i = 0; i < 35; i++) page.Editor.NewCard($"Scene {i + 1}", 40 + (i % 7) * 300, 400 + (i / 7) * 160);
                page.Redraw();
            }));
            probe.Screens.Add(("story card", () => Sample(Content.Workshop.WorkshopTab.Story).OpenStory("cellar")));
            probe.Screens.Add(("test hero", () => Sample(Content.Workshop.WorkshopTab.Story).PlayFromHere("cellar", null)));

            probe.Screens.Add(("create", () => QuickStart.Creating(launch, "class", null)));
            probe.Screens.Add(("spells", () => QuickStart.Creating(launch, "spells", "mage")));
            probe.Screens.Add(("abilities", () => QuickStart.Creating(launch, "abilities", "mage")));

            // the pages that grow with what's picked, at their longest (cc_task_m3 Part 1)
            foreach (Content.Creation.Step step in CreationWorst.Pages)
            {
                Content.Creation.Step one = step;
                probe.Screens.Add(($"worst {Core.Words.EnumWords.Id(one)}", () => CreationWorst.Show(launch, one)));
            }

            return probe;
        }

        public static LayoutProbe OfPlay(PlayDirector director)
        {
            var probe = new LayoutProbe
            {
                Name = "LayoutProbe",
                WhenReady = () => director.Combat.CanAct && director.Showing == null &&
                                  (director.Table.Lift?.IsResting ?? true),
                Board = () => BoardOnScreen(director.Table),
            };

            probe.Screens.Add(("fight", () => director.Showing?.Close()));
            probe.Screens.Add(("pause", () => { director.Showing?.Close(); director.Pause(); }));
            probe.Screens.Add(("sheet", () => director.Swap(new SheetScreen(Content.Sheet.SheetView.Of(GameState.Run.Hero, leveling: GameState.Run.Pack.Manifest.Rules.Leveling)))));
            probe.Screens.Add(("pack", () => director.Swap(new PackScreen(new Content.Screens.PackView(GameState.Run.Hero, GameState.Run.Items), GameState.Resolver))));

            return probe;
        }

        // the board's mat, as a box on the screen: its four corners through the camera
        static Rect2? BoardOnScreen(Game.Table.Table table)
        {
            Game.Board.Board board = table.Board;
            Camera3D camera = table.Camera;

            if (board?.Map == null || camera == null) return null;

            float w = board.MatWidth * 0.5f, d = board.MatDepth * 0.5f;
            Vector2[] corners = new[] { new Vector3(-w, 0, -d), new Vector3(w, 0, -d), new Vector3(-w, 0, d), new Vector3(w, 0, d) }
                                .Select(c => camera.UnprojectPosition(board.ToGlobal(c))).ToArray();

            Vector2 min = new(corners.Min(c => c.X), corners.Min(c => c.Y));
            Vector2 max = new(corners.Max(c => c.X), corners.Max(c => c.Y));

            return new Rect2(min, max - min);
        }
    }
}
