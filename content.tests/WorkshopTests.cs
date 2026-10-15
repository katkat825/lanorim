using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Creation;
using Content.Maps;
using Content.Play;
using Content.Schema;
using Content.Sheet;
using Content.Workshop;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Content.Tests
{
    // cc_task_m Parts 3, 4 and 6: the new-campaign wizard writes a pack that loads with no errors and plays to its one
    // line; the campaign page's edits are one undo step each and save into the same files a hand-made campaign has;
    // the autosave is offered back when it's newer than the files
    public class WorkshopTests : IDisposable
    {
        static readonly Library Srd = Library.Srd();

        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_workshop_" + Guid.NewGuid().ToString("N"));

        MapPlace MyMaps => MapPlace.Library(Path.Combine(_root, "my_maps"));

        string Campaigns => Path.Combine(_root, "campaigns");

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        internal static CampaignWizard Filled(Func<string, bool> taken = null)
        {
            var wizard = new CampaignWizard(taken ?? (_ => false));
            wizard.Draft.Name = "The Salt Road";
            wizard.Draft.Description = "A short road with a long shadow.";
            wizard.Draft.Author = "Kathleen";
            wizard.Tag("mystery", true);
            wizard.Tag(CampaignWizard.TagFrom("Sea Shanties!"), true);
            wizard.Draft.Mature = true;
            wizard.ChapterTitle = "The Toll House";
            return wizard;
        }

        string Finish(CampaignWizard wizard)
        {
            string folder = wizard.Finish(Campaigns, MyMaps, "Your story starts here.", out string problem);
            Assert.Null(problem);
            return folder;
        }

        [Fact]
        public void TheWizardsPackLoadsWithNoErrorsAndPlaysToItsOneLine()
        {
            CampaignWizard wizard = Filled();

            Assert.Equal("the_salt_road", wizard.Id);
            string folder = Finish(wizard);
            Assert.Equal(Path.Combine(Campaigns, "the_salt_road"), folder);

            Package pack = Package.Read(folder);
            Assert.True(pack.Sound, string.Join("\n", pack.Problems));
            Assert.Empty(WorkshopProblems.Of(folder));

            Manifest manifest = pack.Manifest;
            Assert.Equal(PackKind.Campaign, manifest.Kind);
            Assert.Equal(ContentFormat.Current, manifest.Format);
            Assert.Equal(Core.EngineVersion.Current, manifest.Engine);
            Assert.Equal(new[] { "mystery", "sea_shanties", "mature" }, manifest.Tags);
            Assert.Equal("the_toll_house", manifest.Start);
            Assert.Equal(new[] { "the_toll_house" }, manifest.Chapters.Single().Maps);
            Assert.True(pack.Maps.ContainsKey("the_toll_house"));

            LocaleSheet words = CampaignFiles.Sheet(folder, "the_salt_road");
            Assert.Equal("The Salt Road", words.Get(manifest.NameKey));
            Assert.Equal("A short road with a long shadow.", words.Get(manifest.DescriptionKey));
            Assert.Equal("The Toll House", words.Get(Manifest.ChapterTitle("the_salt_road", "the_toll_house")));

            // played: one GM line, then the end
            Hero hero = QuickHero.Make(Srd, "fighter", 1, "Tester");
            var run = new CampaignRun(Srd, pack, hero, new StandardResolver(new SeededRng(1)), new SeededRng(1));
            Assert.Empty(run.Book.Problems);

            // a new hero opens on the starting shop (the campaign left the game's default), then the story
            if (run.Start() == Scene.Shop) run.LeaveShop();
            Assert.Equal(Scene.Line, run.Now);
            Assert.Equal("dialogue.dm.line.the_salt_road.start", run.Talk.Saying.Key);
            Assert.Equal("Your story starts here.", words.Get(run.Talk.Saying.Key));

            run.Next();
            Assert.Equal(Scene.Over, run.Now);
        }

        [Fact]
        public void AnIdThatsTakenGetsANumberAndAPickedMapIsCopiedIn()
        {
            Directory.CreateDirectory(MyMaps.Folder);
            Assert.Null(MapFiles.Write(MyMaps, "harbour", Screens.MapBuilderView.Fresh(10, 8, "village_street").Draft));

            CampaignWizard wizard = Filled(id => id == "the_salt_road");
            wizard.FirstMap = FirstMap.Pick;
            Assert.Equal(CampaignWizard.NeedsMapKey, wizard.Missing(WizardPage.FirstChapter));
            wizard.Picked = "harbour";

            Assert.Equal("the_salt_road_2", wizard.Id);

            string folder = Finish(wizard);
            Package pack = Package.Read(folder);
            Assert.True(pack.Sound, string.Join("\n", pack.Problems));
            Assert.Equal(new[] { "harbour" }, pack.Manifest.Chapters.Single().Maps);
            Assert.True(pack.Maps.ContainsKey("harbour"));
        }

        [Fact]
        public void TheWizardWalksItsPagesAndWontGoOnWithoutAName()
        {
            var wizard = new CampaignWizard(_ => false);

            Assert.Equal(CampaignWizard.NeedsNameKey, wizard.Missing(WizardPage.About));
            Assert.False(wizard.Next());

            wizard.Draft.Name = "Anything";
            Assert.True(wizard.Next());
            Assert.Equal(WizardPage.Rules, wizard.Page);
            Assert.True(wizard.Next());
            Assert.Equal(CampaignWizard.NeedsChapterKey, wizard.Missing(WizardPage.FirstChapter));
            Assert.False(wizard.CanFinish);

            wizard.ChapterTitle = "One";
            Assert.True(wizard.CanFinish);
            Assert.True(wizard.Back());
            Assert.Equal(WizardPage.Rules, wizard.Page);

            // the id in Advanced: a typed one is used when it's an id, the name's otherwise
            wizard.TypedId = "not an id!";
            Assert.Equal("anything", wizard.Id);
            wizard.TypedId = "my_own";
            Assert.Equal("my_own", wizard.Id);
        }

        [Fact]
        public void EachEditIsOneUndoStepAndSaveWritesTheSameFilesAHandMadeCampaignHas()
        {
            string folder = Finish(Filled());
            CampaignEditor editor = CampaignEditor.Open(folder);

            Assert.False(editor.Unsaved);
            Assert.Equal("The Salt Road", editor.Draft.Name);

            editor.Edit("about", d => d.Name = "The Salt Road, Again");
            string second = editor.AddChapter("The Lighthouse");
            Assert.Equal("the_lighthouse", second);
            Assert.Equal("the_lighthouse_2", editor.AddChapter("The Lighthouse"));
            editor.DeleteChapter("the_lighthouse_2");

            string map = editor.MakeMap("Lamp Room", "dungeon_room", out string problem);
            Assert.Null(problem);
            editor.AddMap(second, map);
            editor.StartAt(second);
            editor.MoveChapter(1, 0);

            string oda = editor.AddPerson("Oda Venn");
            editor.NoteOn(oda, "keeps the light; lies about the wreck");
            Assert.False(editor.Edit("about", d => d.Name = d.Name));    // nothing changed, no step

            // typing into one field is one step
            editor.Edit("about.author", d => d.Author = "K", typing: true);
            editor.Edit("about.author", d => d.Author = "Ka", typing: true);
            editor.Edit("about.author", d => d.Author = "Kat", typing: true);
            Assert.True(editor.Undo.Undo());
            Assert.Equal("Kathleen", editor.Draft.Author);

            Assert.True(editor.Unsaved);
            Assert.Equal(new[] { "the_lighthouse", "the_toll_house" }, editor.Draft.Chapters.Select(c => c.Id));

            // undo walks back one step at a time, redo forward again
            Assert.True(editor.Undo.Undo());
            Assert.Equal("", editor.Draft.Character(oda).Note);
            Assert.True(editor.Undo.Undo());
            Assert.Null(editor.Draft.Character(oda));
            Assert.True(editor.Undo.Redo());
            Assert.True(editor.Undo.Redo());
            Assert.Equal("keeps the light; lies about the wreck", editor.Draft.Character(oda).Note);

            Assert.Null(editor.Save());
            Assert.False(editor.Unsaved);

            Package pack = Package.Read(folder);
            Assert.True(pack.Sound, string.Join("\n", pack.Problems));
            Assert.Equal("the_lighthouse", pack.Manifest.Start);
            Assert.Equal(new[] { "the_lighthouse", "the_toll_house" }, pack.Manifest.Chapters.Select(c => c.Id));
            Assert.Equal(new[] { "lamp_room" }, pack.Manifest.Chapters[0].Maps);

            LocaleSheet words = CampaignFiles.Sheet(folder, "the_salt_road");
            Assert.Equal("The Salt Road, Again", words.Get(pack.Manifest.NameKey));
            Assert.Equal("The Lighthouse", words.Get(Manifest.ChapterTitle("the_salt_road", "the_lighthouse")));
            Assert.False(words.Has(Manifest.ChapterTitle("the_salt_road", "the_lighthouse_2")));
            Assert.Equal("Oda Venn", words.Get("actor.oda_venn.name"));
            Assert.True(File.Exists(Path.Combine(folder, WorkshopNotes.FileName)));

            // and open again, it's the same draft
            CampaignEditor again = CampaignEditor.Open(folder);
            Assert.Equal(editor.Draft.Snapshot(), again.Draft.Snapshot());

            // the story's start node is the first chapter's: a chapter the story uses says so before it's deleted
            Assert.True(again.StoryUses("the_toll_house"));
            Assert.False(again.StoryUses("the_lighthouse"));
        }

        [Fact]
        public void ACampaignThatDoesntLoadYetStillOpensWithEverythingOnItsForms()
        {
            string folder = Finish(Filled());
            CampaignEditor editor = CampaignEditor.Open(folder);

            // a chapter with no map yet: the loader refuses the pack, the forms keep it
            editor.AddChapter("The Lighthouse");
            Assert.Null(editor.Save());
            Assert.False(Package.Read(folder).Sound);
            Assert.Contains(editor.Problems, p => ProblemWords.Plain(p).Kind == "chapter_empty");

            CampaignEditor again = CampaignEditor.Open(folder);
            Assert.True(again.Draft.PackRead);
            Assert.Equal(new[] { "the_toll_house", "the_lighthouse" }, again.Draft.Chapters.Select(c => c.Id));
            Assert.Equal("The Lighthouse", again.Draft.Chapter("the_lighthouse").Title);

            // and a pack.json that isn't JSON at all opens too, and is never written over
            File.WriteAllText(Path.Combine(folder, "pack.json"), "{ not json");
            CampaignEditor broken = CampaignEditor.Open(folder);
            Assert.False(broken.Draft.PackRead);
            Assert.Equal("The Salt Road", broken.Draft.Name);
            broken.Edit("about", d => d.Name = "Renamed anyway");
            Assert.Null(broken.Save());
            Assert.Equal("{ not json", File.ReadAllText(Path.Combine(folder, "pack.json")));
        }

        [Fact]
        public void KathleensSampleCampaignOpensAndSavesUnchanged()
        {
            string copy = Path.Combine(_root, "sample_millbrook");
            Copy(SampleCampaignTests.Folder, copy);

            string pack = File.ReadAllText(Path.Combine(copy, "pack.json")).Replace("\r\n", "\n");
            string csv = File.ReadAllText(Path.Combine(copy, "locale", "sample_millbrook.csv")).Replace("\r\n", "\n");

            CampaignEditor editor = CampaignEditor.Open(copy);
            Assert.Equal("Millbrook (test campaign)", editor.Draft.Name);
            Assert.Equal("The Old Mill", editor.Draft.Chapters.Single().Title);
            Assert.Equal("Oda", editor.Draft.Character("oda").Name);
            Assert.Empty(editor.Problems.Where(p => p.IsAFault));

            editor.Edit("about", d => d.Author = d.Author);
            Assert.Null(editor.Save());

            Assert.Equal(pack, File.ReadAllText(Path.Combine(copy, "pack.json")));
            Assert.Equal(csv, File.ReadAllText(Path.Combine(copy, "locale", "sample_millbrook.csv")));
            Assert.False(File.Exists(Path.Combine(copy, WorkshopNotes.FileName)));
        }

        [Fact]
        public void AnAutosaveNewerThanTheFilesIsOfferedBackAndSaveThrowsItAway()
        {
            string folder = Finish(Filled());
            CampaignEditor editor = CampaignEditor.Open(folder);

            Assert.False(editor.HasDraftFromLastTime);
            Assert.Null(editor.KeepDraft());            // nothing unsaved: nothing kept
            Assert.False(Autosave.Has(folder));

            editor.Edit("about", d => d.Description = "Changed and never saved.");
            Assert.Null(editor.KeepDraft());
            File.SetLastWriteTimeUtc(Autosave.PathOf(folder), DateTime.UtcNow.AddMinutes(1));

            // the next time it's opened: asked, and kept
            CampaignEditor later = CampaignEditor.Open(folder);
            Assert.True(later.HasDraftFromLastTime);
            Assert.Equal("A short road with a long shadow.", later.Draft.Description);
            later.TakeDraft();
            Assert.Equal("Changed and never saved.", later.Draft.Description);
            Assert.True(later.Unsaved);

            // a draft is never a campaign the loader sees
            Assert.True(Package.Read(folder).Sound, string.Join("\n", Package.Read(folder).Problems));

            Assert.Null(later.Save());
            Assert.False(Autosave.Has(folder));

            // thrown away instead
            later.Edit("about", d => d.Description = "Again.");
            later.KeepDraft();
            File.SetLastWriteTimeUtc(Autosave.PathOf(folder), DateTime.UtcNow.AddMinutes(1));
            CampaignEditor third = CampaignEditor.Open(folder);
            Assert.True(third.HasDraftFromLastTime);
            third.DropDraft();
            Assert.False(third.HasDraftFromLastTime);
            Assert.Equal("Changed and never saved.", third.Draft.Description);
        }

        internal static void Copy(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.EnumerateFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            foreach (string dir in Directory.EnumerateDirectories(from)) Copy(dir, Path.Combine(to, Path.GetFileName(dir)));
        }
    }
}
