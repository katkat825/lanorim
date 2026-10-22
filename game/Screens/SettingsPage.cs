using Content.Screens;

namespace Game.Screens
{
    // THE BOOK'S "SETTINGS": the settings panel, scrolled, with Back to the book. Was part of Launch.cs (cc_task_j
    // Part 1)
    public sealed class SettingsPage : IBookPage
    {
        public string Id => "settings";

        public string NameKey => CampaignBook.SettingsKey;

        public void Open(Launch launch, string open = null) =>
            launch.Show(Ui.Panel(SettingsPanel.Scrolled(launch.ShowBook, 560, Content.Screens.CampaignBook.ContentsKey)));
    }
}
