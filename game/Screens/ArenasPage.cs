using Content.Screens;
using Game.Play;

namespace Game.Screens
{
    // MORE THAN ONE ARENA (cc_task_ca Part 5): the contents' "Arenas" line opens this short picker, each arena campaign
    // by its own name, opening its two pages. With one arena the contents names it and opens it straight away, so this
    // page isn't a line of its own (InContents)
    public sealed class ArenasPage : IBookPage
    {
        public string Id => CampaignBook.ArenasPageId;

        public string NameKey => CampaignBook.ArenasKey;

        public bool InContents => false;

        public void Open(Launch launch, string open = null)
        {
            var book = new CampaignBook(GameState.Manifests, GameState.Saves);
            var list = Ui.Column(10, Ui.Title(CampaignBook.ArenasKey));

            foreach (BookPage arena in book.Arenas)
            {
                BookPage a = arena;
                list.AddChild(Ui.Button(a.NameKey, () => launch.ShowBook(a.Id)));
                list.AddChild(Ui.Label(a.DescriptionKey));
            }

            list.AddChild(Ui.Button(CampaignBook.ContentsKey, launch.ShowBook));

            launch.Show(Ui.Panel(list));
        }
    }
}
