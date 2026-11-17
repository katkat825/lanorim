using Content.Dialogue;

namespace Content.Play
{
    // <<tip section>> (cc_task_nt Part 4): the story stops while the F1 help card is open at that section, and goes on
    // when the player closes it. The section was checked against the card's own (HelpCard.Sections) as the story was read
    public sealed partial class CampaignRun
    {
        // the section the card is open at, while Now is Scene.Tip
        public string Tip { get; private set; }

        Scene? TipAsked(Request request)
        {
            Tip = request.Id;
            return Scene.Tip;
        }

        // the card was closed: the story goes on
        public Scene TipClosed()
        {
            if (Now != Scene.Tip) return Now;

            Tip = null;
            Reply(new Answer());

            return Settle();
        }
    }
}
