using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;
using Content.Workshop.Story;
using Content.Workshop.Tables;
using Core.Localization;

namespace Content.Workshop
{
    // A CAMPAIGN FOLDER, OPENED IN THE WORKSHOP AND SAVED BACK (cc_task_m Part 4): pack.json through its reader and
    // ManifestWriter, the campaign's words through its locale CSV (LocaleSheet, every other row kept), and the
    // Workshop's own notes through workshop.json (WorkshopNotes). The same files a hand-made campaign has, so
    // Kathleen's campaigns open here too and what's saved plays with no conversion (WORKSHOP_DESIGN.md principle 1)
    public static class CampaignFiles
    {
        public const string PackFile = ManifestReader.PackFileName;

        // a campaign's words: the locale CSV that names it, else locale/<id>.csv (the one the wizard writes)
        public static string LocaleFile(string folder, string id)
        {
            string locale = Path.Combine(folder, Package.LocaleFolder);
            string name = KeyConventions.Key(KeyConventions.CampaignNs, id, "name");

            if (Directory.Exists(locale))
                foreach (string csv in Directory.EnumerateFiles(locale, "*.csv").OrderBy(f => f, StringComparer.Ordinal))
                    if (LocaleSheet.Of(File.ReadAllText(csv)).Has(name)) return csv;

            return Path.Combine(locale, id + ".csv");
        }

        public static LocaleSheet Sheet(string folder, string id)
        {
            string file = LocaleFile(folder, id);

            return File.Exists(file) ? LocaleSheet.Of(File.ReadAllText(file)) : LocaleSheet.Empty();
        }

        // --- open -------------------------------------------------------------------------------------------------

        // the draft a folder holds, and what stopped any of it being read. never null: pack.json is read as far as it
        // can be (ManifestReader's lenient read: a chapter with no map yet is still a chapter on the form), and one that
        // isn't JSON at all gives a draft named for its folder with PackRead false, so the page still opens and says why
        public static CampaignDraft Open(string folder, List<ContentProblem> problems)
        {
            string id = new DirectoryInfo(folder).Name;
            string pack = Path.Combine(folder, PackFile);

            CampaignDraft draft = null;

            if (File.Exists(pack))
            {
                Read<Manifest> read = ManifestReader.Parse(File.ReadAllText(pack), PackFile, id, lenient: true);
                problems.AddRange(read.Problems);

                if (read.Value != null) draft = CampaignDraft.Of(read.Value);
            }
            else problems.Add(new ContentProblem(id, "", $"a campaign folder needs a {PackFile} in it"));

            draft ??= new CampaignDraft { Id = id, PackRead = false };

            LocaleSheet sheet = Sheet(folder, draft.Id);

            draft.Name = sheet.Get(NameKey(draft.Id));
            draft.Description = sheet.Get(DescriptionKey(draft.Id));

            foreach (ChapterDraft chapter in draft.Chapters)
                chapter.Title = sheet.Get(Manifest.ChapterTitle(draft.Id, chapter.Id));

            WorkshopNotes notes = WorkshopNotes.Read(folder, problems);
            draft.BasedOn = notes.BasedOn;

            draft.Tables = TableFiles.Open(folder, sheet, draft.Unread);
            draft.Tables.Names = new Dictionary<string, string>(notes.Names, StringComparer.Ordinal);

            draft.Story = StoryFiles.Open(folder);
            draft.Story.Looks = new Dictionary<string, CardLook>(notes.Looks, StringComparer.Ordinal);
            draft.Story.View = notes.View;
            draft.Story.Phrases = new Dictionary<string, string>(notes.Phrases, StringComparer.Ordinal);

            draft.People = Actors(sheet).Concat(notes.People.Keys).Distinct().OrderBy(a => a, StringComparer.Ordinal)
                .Select(actor => new Person
                {
                    Id = actor,
                    Name = sheet.Get(KeyConventions.ActorName(actor)),
                    Note = notes.Note(actor),
                    Portrait = notes.Portrait(actor),
                })
                .ToList();

            return draft;
        }

        // every actor.<id>.name in the campaign's words: the people it names
        static IEnumerable<string> Actors(LocaleSheet sheet) =>
            sheet.Keys.Select(k => k.Split('.'))
                 .Where(p => p.Length == 3 && p[0] == KeyConventions.ActorNs && p[2] == "name")
                 .Select(p => p[1]);

        // --- save ---------------------------------------------------------------------------------------------------

        // THE REAL FILES WRITTEN (the Save button; the autosave is Autosave's): null when they were, or the system's own
        // reason they weren't, which is the disk's and not the game's to translate. problems or not, it saves
        // (principle 3, warn never refuse)
        public static string Save(CampaignDraft draft, string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);

                if (draft.PackRead) File.WriteAllText(Path.Combine(folder, PackFile), ManifestWriter.Write(draft.ToManifest()));

                string csv = LocaleFile(folder, draft.Id);
                LocaleSheet sheet = File.Exists(csv) ? LocaleSheet.Of(File.ReadAllText(csv)) : LocaleSheet.Empty();

                Words(draft, sheet);
                TableFiles.Save(draft.Tables, folder, sheet);
                StoryFiles.Save(draft.Story, folder, draft.Id, sheet);

                Directory.CreateDirectory(Path.GetDirectoryName(csv));
                File.WriteAllText(csv, sheet.Text());

                WorkshopNotes.Of(draft).Write(folder);

                return null;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                return could.Message;
            }
        }

        // the campaign's words into its sheet: its name and description, each chapter's title, each person's name. a
        // chapter or a person taken out takes its words with it
        public static void Words(CampaignDraft draft, LocaleSheet sheet)
        {
            sheet.Set(NameKey(draft.Id), draft.Name);
            sheet.Set(DescriptionKey(draft.Id), draft.Description);

            string chapters = KeyConventions.Key(KeyConventions.QuestNs, draft.Id, "");

            foreach (string gone in sheet.Keys.Where(k => k.StartsWith(chapters, StringComparison.Ordinal) && k.EndsWith(".title"))
                                              .Where(k => draft.Chapters.All(c => Manifest.ChapterTitle(draft.Id, c.Id) != k)).ToList())
                sheet.Remove(gone);

            foreach (ChapterDraft chapter in draft.Chapters)
                sheet.Set(Manifest.ChapterTitle(draft.Id, chapter.Id), chapter.Title);

            foreach (string gone in Actors(sheet).Where(a => draft.Character(a) == null).ToList())
                sheet.Remove(KeyConventions.ActorName(gone));

            foreach (Person person in draft.People)
                sheet.Set(KeyConventions.ActorName(person.Id), person.Name);
        }

        public static string NameKey(string id) => KeyConventions.Key(KeyConventions.CampaignNs, id, "name");

        public static string DescriptionKey(string id) => KeyConventions.Key(KeyConventions.CampaignNs, id, "description");

        // --- when it was last changed -------------------------------------------------------------------------------

        // the newest of its files, the Workshop's dot-folders left out: the hub card's "last edited", and what the
        // autosave is held against
        public static DateTime Edited(string folder)
        {
            if (!Directory.Exists(folder)) return DateTime.MinValue;

            return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                            .Where(f => !Path.GetRelativePath(folder, f).Split('/', '\\').Any(PackFolder.IsHidden))
                            .Select(File.GetLastWriteTimeUtc)
                            .DefaultIfEmpty(Directory.GetLastWriteTimeUtc(folder))
                            .Max();
        }
    }
}
