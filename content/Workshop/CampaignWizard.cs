using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Maps;

namespace Content.Workshop
{
    // the wizard's three pages (cc_task_m Part 3)
    public enum WizardPage
    {
        About,
        Rules,
        FirstChapter,
    }

    // how the first chapter's first map comes about: made from a template (and opened in the builder), or one of My maps
    // copied in
    public enum FirstMap
    {
        Make,
        Pick,
    }

    // THE NEW-CAMPAIGN WIZARD, AS LOGIC (cc_task_m Part 3): three short pages filling a CampaignDraft - its name and
    // blurb, how it plays, and its first chapter and map - and Finish, which writes the pack: pack.json, the locale CSV
    // with its name, description and first chapter's title, maps/, and a dialogue/story.yarn whose start node is one GM
    // line. It loads and plays as it is: one line, then the end
    public sealed class CampaignWizard
    {
        readonly Func<string, bool> _taken;

        // `taken`: an id some campaign in a root has already (the book's own and My campaigns)
        public CampaignWizard(Func<string, bool> taken) => _taken = taken ?? (_ => false);

        public CampaignDraft Draft { get; } = new() { Author = "" };

        public WizardPage Page { get; private set; } = WizardPage.About;

        // the short list of tags to pick from; the author may type their own (TagFrom). Mature is its own tick box
        public static readonly IReadOnlyList<string> Tags = new[]
        {
            "horror", "comedy", "mystery", "combat_heavy", "story_heavy", "short", "long", "beginner_friendly",
        };

        // a tag typed in: made a word the way an id is (a tag is one, ManifestReader.Tags), or null for nothing usable
        public static string TagFrom(string typed)
        {
            string tag = ContentId.From(typed, "");
            return tag.Length > 0 ? tag : null;
        }

        public bool Tag(string tag, bool on)
        {
            if (tag == null || tag == CampaignDraft.MatureTag) return false;

            Draft.Tags.Remove(tag);
            if (on) Draft.Tags.Add(tag);
            return true;
        }

        // --- the id ----------------------------------------------------------------------------------------------

        // what the author typed in Advanced, or null for the one made from the name
        public string TypedId { get; set; }

        // THE ID: made from the name (lowercase, underscores), and if a campaign has it already, the next free number
        public string Id => ContentId.Free(TypedId is { Length: > 0 } typed && ContentId.IsCampaign(typed) ? typed : ContentId.From(Draft.Name, "campaign"), _taken);

        // --- the first chapter -------------------------------------------------------------------------------------

        public string ChapterTitle { get; set; } = "";

        public FirstMap FirstMap { get; set; } = FirstMap.Make;

        // with Make: the template it starts as
        public string Template { get; set; } = MapTemplates.Standard;

        // with Pick: one of My maps
        public string Picked { get; set; } = "";

        // --- walking the pages -------------------------------------------------------------------------------------

        // what keeps a page from going on: its key, or null. (the forms warn and never refuse - but a campaign needs a
        // name to be made from, and a chapter a title and a map to start on)
        public string Missing(WizardPage page) => page switch
        {
            WizardPage.About when string.IsNullOrWhiteSpace(Draft.Name) => NeedsNameKey,
            WizardPage.About when !Maps.MapName.Allowed(Draft.Name) => Maps.MapName.RuleKey,
            WizardPage.FirstChapter when string.IsNullOrWhiteSpace(ChapterTitle) => NeedsChapterKey,
            WizardPage.FirstChapter when FirstMap == FirstMap.Pick && string.IsNullOrEmpty(Picked) => NeedsMapKey,
            _ => null,
        };

        public bool Next()
        {
            if (Missing(Page) != null || Page == WizardPage.FirstChapter) return false;

            Page++;
            return true;
        }

        public bool Back()
        {
            if (Page == WizardPage.About) return false;

            Page--;
            return true;
        }

        public bool CanFinish => Enum.GetValues<WizardPage>().All(p => Missing(p) == null);

        // the first chapter's id and its first map's: made from its title (a picked map keeps its own name)
        public string ChapterId => ContentId.From(ChapterTitle, "chapter");

        public string MapId => FirstMap == FirstMap.Pick ? Picked : ChapterId;

        // the start node's one line (the story editor, task O, writes the rest)
        public const string Speaker = "dm";

        public const string LineId = "start";

        // --- finish -----------------------------------------------------------------------------------------------

        // THE PACK, WRITTEN into root/<id>: where it is, or null and why not. `firstLine` is the start node's words in
        // the author's language ("Your story starts here."), which the screen says from its own key; `library` is My
        // maps, for a picked map
        public string Finish(string root, MapPlace library, string firstLine, out string problem)
        {
            problem = null;

            if (!CanFinish)
            {
                problem = "the wizard isn't finished";
                return null;
            }

            string id = Id;
            string folder = Path.Combine(root, id);

            Draft.Id = id;
            Draft.Chapters = new List<ChapterDraft> { new() { Id = ChapterId, Title = ChapterTitle.Trim(), Maps = new List<string> { MapId } } };
            Draft.Start = ChapterId;

            try
            {
                Directory.CreateDirectory(folder);

                problem = WriteMap(MapPlace.OfCampaign(folder), library);
                if (problem != null) return null;

                problem = CampaignFiles.Save(Draft, folder);
                if (problem != null) return null;

                // the start node's line, in the campaign's words beside its name
                string csv = CampaignFiles.LocaleFile(folder, id);
                var sheet = Schema.LocaleSheet.Of(File.ReadAllText(csv));
                sheet.Set(Dialogue.DialogueKeys.Line(Speaker, id, LineId), firstLine ?? "");
                File.WriteAllText(csv, sheet.Text());

                Directory.CreateDirectory(Path.Combine(folder, Package.DialogueFolder));
                File.WriteAllText(Path.Combine(folder, Package.DialogueFolder, "story" + Dialogue.DialogueBook.Extension), Story(ChapterId, firstLine));

                return folder;
            }
            catch (Exception could) when (could is IOException || could is UnauthorizedAccessException)
            {
                problem = could.Message;
                return null;
            }
        }

        string WriteMap(MapPlace campaign, MapPlace library)
        {
            if (FirstMap == FirstMap.Pick)
            {
                if (!MapFiles.Read(library, Picked, out MapDraft picked, out string problem)) return problem;
                return MapFiles.Write(campaign, MapId, picked);
            }

            MapDraft drawn = MapTemplates.Read(Template) ?? MapTemplates.Read(MapTemplates.Standard);
            MapDraft made = MapTemplates.Make(Template, drawn.Columns, drawn.Rows);

            // a made map is called what its chapter is, when the title keeps the name rule (cc_task_m2 Part 7, MapName)
            made.Name = MapName.Allowed(ChapterTitle) ? ChapterTitle.Trim() : "";

            return MapFiles.Write(campaign, MapId, made);
        }

        // the story's one node: the first chapter's, one GM line, then the end
        public static string Story(string chapter, string line) =>
            $"title: {chapter}\n{Dialogue.DialogueBook.SpeakerHeader}: {Speaker}\n---\n{(line ?? "").Replace("\n", " ")} #line:{LineId}\n===\n";

        static readonly Func<string, string> K = Screens.ScreenKeys.For("workshop");

        public static readonly string NeedsNameKey = K("wizard_needs_name");
        public static readonly string NeedsChapterKey = K("wizard_needs_chapter");
        public static readonly string NeedsMapKey = K("wizard_needs_map");

        public static string TagKey(string tag) => K("tag_" + tag);

        public static IEnumerable<string> Keys() =>
            new[] { NeedsNameKey, NeedsChapterKey, NeedsMapKey }.Concat(Tags.Select(TagKey));
    }
}
