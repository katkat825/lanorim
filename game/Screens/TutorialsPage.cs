using Content.Screens;
using Game.Play;

namespace Game.Screens
{
    // THE BOOK'S "TUTORIALS": each tutorial campaign, available or why not, and the test campaigns under them. Picking
    // one starts a character in it. Was part of Launch.cs (cc_task_j Part 1)
    public sealed class TutorialsPage : IBookPage
    {
        public string Id => "tutorials";

        public string NameKey => CampaignBook.TutorialsKey;

        public void Open(Launch launch, string open = null)
        {
            var picker = new TutorialPicker(GameState.Manifests);
            var list = Ui.Column(10, Ui.Title(TutorialPicker.TitleKey));

            foreach (TutorialChoice choice in picker.Choices)
            {
                TutorialChoice c = choice;
                list.AddChild(Ui.Button(c.NameKey, () => launch.ShowCreation(c.CampaignId)).Greyed(!c.Available, c.WhyNotKey));
                list.AddChild(Ui.Why(!c.Available, c.WhyNotKey));
                list.AddChild(Ui.Label(c.BlurbKey));
            }

            if (picker.Tests.Count > 0)
            {
                list.AddChild(Ui.Label(TutorialPicker.TestsKey));

                foreach (BookPage test in picker.Tests)
                {
                    BookPage t = test;
                    list.AddChild(Ui.Button(t.NameKey, () => launch.ShowCreation(t.Id)));
                }
            }

            list.AddChild(Ui.Button(Content.Screens.CampaignBook.ContentsKey, launch.ShowBook));

            launch.Show(Ui.Panel(list));
        }
    }
}
