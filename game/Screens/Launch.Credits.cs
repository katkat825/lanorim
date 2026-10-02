using System.Linq;
using System.Text.RegularExpressions;
using Content.Screens;
using Godot;

namespace Game.Screens
{
    // THE BOOK'S "CREDITS" (cc_task_f Part 3): a scrolling page from content/srd/credits/credits.json (CreditsView). The
    // SRD's notice comes first and whole, verbatim, never cut; then that Lanorim isn't affiliated with Wizards of the
    // Coast; then who made it, the art and sound, the type and the code, only what ships. Every word is a key but the
    // notice and the names; a link is its address, and a click opens it (LinkButton.Uri). How it looks is Kathleen's
    public partial class Launch
    {
        void ShowCredits()
        {
            CreditsView credits = CreditsView.Srd();
            var page = Ui.Column(12, Ui.Title(CreditsView.TitleKey));

            Credit srd = credits.Ruleset;

            page.AddChild(Ui.Plain(srd.Text));

            // the notice's own addresses, each a link under it
            foreach (Match link in Regex.Matches(srd.Text, @"https?://\S+?(?=[.,;)]?(\s|$))"))
                page.AddChild(Link(link.Value));

            page.AddChild(Ui.Label(CreditsView.NotAffiliatedKey));

            foreach ((CreditKind kind, var lines) in credits.Sections)
            {
                page.AddChild(Ui.Title(CreditsView.SectionKey(kind)));

                foreach (Credit credit in lines) page.AddChild(Line(credit));
            }

            page.AddChild(Ui.Button(ScreenWords.Back, ShowBook));

            Show(Ui.Panel(Ui.Scroll(page, 560)), 860);
        }

        // "Quaternius - Ultimate Animated Animals, Fantasy Props MegaKit - 3D models - Quaternius Asset License", then
        // "by ..." for a clip's or a font's authors, and the link
        Control Line(Credit credit)
        {
            string what = string.Join(" - ", new[]
            {
                credit.Name,
                credit.Packs.Count > 0 ? string.Join(", ", credit.Packs) : null,
                string.IsNullOrEmpty(credit.By) ? null : Ui.Say(CreditsView.ByKey, credit.By),
                Ui.Say(CreditsView.RoleKey(credit.Role)),
                credit.Licence is CreditLicence licence ? Ui.Say(CreditsView.LicenceKey(licence)) : null,
            }.Where(s => !string.IsNullOrEmpty(s)));

            var line = Ui.Column(2, Ui.Plain(what));

            if (!string.IsNullOrEmpty(credit.Url)) line.AddChild(Link(credit.Url));

            return line;
        }

        // a LinkButton with a Uri opens it itself when pressed (OS.ShellOpen)
        static LinkButton Link(string url) => new LinkButton { Text = url, Uri = url, FocusMode = Control.FocusModeEnum.All };
    }
}
