namespace Game.Screens
{
    // A PAGE OF THE BOOK THAT ISN'T A CAMPAIGN (cc_task_j-split-classes-dev-hooks.md Part 1): its line in the contents
    // and on the fallback panel, and what it shows when it's turned to. Each was a partial of Launch; the launch
    // screen reads BookPages.Make() for the contents and the panel, and `--show <id>` opens one, so a new page is one
    // new file and one line in that list
    public interface IBookPage
    {
        // its name on the command line (`--show credits`) and in the contents' entries
        string Id { get; }

        // its line in the contents
        string NameKey { get; }

        // turned to, on the launch screen. `open` is what `--open` named (a campaign), when anything did
        void Open(Launch launch, string open = null);
    }
}
