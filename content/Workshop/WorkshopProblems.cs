using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Content.Schema;

namespace Content.Workshop
{
    // EVERYTHING THE CAMPAIGN CHECK SAYS ABOUT A CAMPAIGN FOLDER (cc_task_m Part 5): Package.Read (pack.json, its
    // folders, maps, fights and tables), the story's own check (DialogueBook: it compiles, its lines are tagged, two
    // keys), and two cautions only the Workshop gives - a campaign with no name, and a chapter with no title - since a
    // form can leave one blank where a hand-written file wouldn't. Warnings never stop a save (principle 3)
    public static class WorkshopProblems
    {
        public static IReadOnlyList<ContentProblem> Of(string folder)
        {
            Package pack = Package.Read(folder);

            var problems = new List<ContentProblem>(pack.Problems);

            if (pack.Manifest == null) return problems;

            problems.AddRange(DialogueBook.Read(Path.Combine(folder, Package.DialogueFolder), pack.Id).Problems);

            LocaleSheet words = CampaignFiles.Sheet(folder, pack.Id);

            if (string.IsNullOrWhiteSpace(words.Get(pack.Manifest.NameKey)))
                problems.Add(ContentProblem.Caution(Package.LocaleFolder, "name", "the campaign has no name"));

            foreach (Chapter chapter in pack.Manifest.Chapters.Where(c => string.IsNullOrWhiteSpace(words.Get(Manifest.ChapterTitle(pack.Id, c.Id)))))
                problems.Add(ContentProblem.Caution(Package.LocaleFolder, $"chapters.{chapter.Id}.title", $"chapter '{chapter.Id}' has no title"));

            return problems;
        }

        // the errors: what stops the campaign loading ("Won't load yet: 2 problems" on its card)
        public static int Errors(IEnumerable<ContentProblem> problems) => problems.Count(p => p.IsAFault);
    }
}
