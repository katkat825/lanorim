using Content.Campaigns;
using Content.Play;
using Content.Sheet;
using Content.Workshop;
using Content.Workshop.Testing;

namespace Game.Play
{
    public static partial class GameState
    {
        // --- a Workshop test (cc_task_q) -----------------------------------------------------------------------------

        // the test being played, and the campaign page it goes back to; null when nothing is being tested
        public static WorkshopTest Testing { get; private set; }

        // where a test's campaign and its saves go: never the author's folder, never the player's saves
        public const string TestFolder = "user://workshop_test";

        // the test that ended last: what it saw go wrong, listed on the campaign page with Go there
        public static WorkshopTest LastTest { get; private set; }

        // where the campaign page opens when a test comes back: the story tab, on this card, this block highlighted
        public static (WorkshopTab Tab, string Card, int[] Block)? EditingAt { get; set; }

        // ACHIEVEMENTS COUNT ONLY IN THE FULL GAME, AND NEVER IN A TEST (cc_task_q Part 4.2; Edition's rule)
        // (no achievement is counted anywhere yet; this is the one gate for when one is)
        public static bool AchievementsCount => Editions.From(Godot.OS.HasFeature(Editions.DemoFeature)).Achievements() && Testing == null;

        // THE TEST'S CAMPAIGN PLAYED WITH THIS HERO, its saves apart, the page kept to come back to
        public static CampaignRun Test(WorkshopTest test, Package pack, Hero hero)
        {
            Building = null;
            Testing = test;
            Editing = test.Editor;

            _savesBefore = SavesFolder;
            SavesFolder = TestFolder + "/saves";
            _saves = null;

            CampaignRun run = Begin(pack, hero, 0);
            test.Prepare(run);
            return run;
        }

        // BACK TO THE EDITOR: the run forgotten, the player's own saves back, and the page told where to open (the card
        // that was playing, its block highlighted). Nothing to save: a test is ended without a question (Part 4.3)
        public static void StopTesting()
        {
            WorkshopTest test = Testing;
            Testing = null;

            SavesFolder = _savesBefore ?? "user://saves";
            _saves = null;

            Leave();

            if (test == null) return;

            LastTest = test;
            Editing = test.Editor;
            EditingAt = test.Fight.Length > 0
                ? (WorkshopTab.Fights, test.Fight, null)
                : (WorkshopTab.Story, test.Card, test.Block(test.Editor.Draft.Story));
            BackTo = Game.Workshop.WorkshopPage.PageId;
        }
    }
}
