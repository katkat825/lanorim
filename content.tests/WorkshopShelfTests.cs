using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Maps;
using Content.Schema;
using Content.Workshop;

namespace Content.Tests
{
    // cc_task_m Parts 1 and 2: My campaigns as a campaign root. A campaign id an earlier root already has isn't loaded
    // and its card says so (Rename gives it the next free id, folder and keys together); Recently deleted keeps a
    // campaign thirty days (move, restore, expire); a duplicate is a campaign of its own
    public class WorkshopShelfTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_shelf_" + Guid.NewGuid().ToString("N"));

        string Game => Path.Combine(_root, "game");

        string Mine => Path.Combine(_root, "mine");

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        string Make(string root, string name, Func<string, bool> taken = null)
        {
            CampaignWizard wizard = WorkshopTests.Filled(taken);
            wizard.Draft.Name = name;
            string folder = wizard.Finish(root, MapPlace.Library(Path.Combine(_root, "maps")), "Your story starts here.", out string problem);
            Assert.Null(problem);
            return folder;
        }

        [Fact]
        public void ALaterCampaignWithAnEarliersIdIsntLoadedAndItsCardSaysSo()
        {
            Make(Game, "Salt Road");
            string mine = Make(Mine, "Salt Road");
            Make(Mine, "Other Road");

            Shelf shelf = Shelf.Of(new[] { Game, Mine }.SelectMany(PackFolder.Folders).Select(Package.Read));

            Assert.Equal(3, shelf.Entries.Count);
            Assert.Equal(2, shelf.Loaded.Count());
            Assert.Equal(Path.Combine(Game, "salt_road"), shelf.Loaded.First(p => p.Id == "salt_road").Folder);
            Assert.Equal(Path.Combine(Game, "salt_road"), shelf.Entries.Single(e => e.Package.Folder == mine).ShadowedBy);
            Assert.Contains(shelf.Problems, p => p.Where == "id" && p.File == mine);

            var cards = WorkshopShelf.Cards(Mine, WorkshopShelf.Ids(new[] { Game }));
            Assert.True(cards.Single(c => c.Id == "salt_road").Shadowed);
            Assert.False(cards.Single(c => c.Id == "other_road").Shadowed);
            Assert.Equal("Salt Road", cards.Single(c => c.Id == "salt_road").Name);
            Assert.Equal(1, cards[0].Chapters);
            Assert.Equal(0, cards[0].Errors);

            // the shelf's sentence for it is the plain "another campaign already uses this name"
            Assert.Equal("same_id", ProblemWords.Plain(shelf.Problems.Single(p => p.Where == "id")).Kind);

            // Rename: the next free id, its folder, pack.json and every key spelled with it together
            string renamed = WorkshopShelf.Rename(mine, WorkshopShelf.Ids(new[] { Game, Mine }).Contains, out string problem);
            Assert.Null(problem);
            Assert.Equal(Path.Combine(Mine, "salt_road_2"), renamed);

            Package pack = Package.Read(renamed);
            Assert.True(pack.Sound, string.Join("\n", pack.Problems));
            Assert.Empty(WorkshopProblems.Of(renamed));
            Assert.Equal("Salt Road", CampaignFiles.Sheet(renamed, "salt_road_2").Get(pack.Manifest.NameKey));
            Assert.True(File.Exists(Path.Combine(renamed, "locale", "salt_road_2.csv")));
            Assert.Equal(3, Shelf.Of(new[] { Game, Mine }.SelectMany(PackFolder.Folders).Select(Package.Read)).Loaded.Count());
        }

        [Fact]
        public void RecentlyDeletedMovesRestoresAndExpires()
        {
            string salt = Make(Mine, "Salt Road");
            var monday = new DateTime(2026, 10, 12);

            string gone = RecentlyDeleted.Delete(salt, monday, out string problem);
            Assert.Null(problem);
            Assert.Equal(Path.Combine(Mine, ".deleted", "salt_road_2026-10-12"), gone);
            Assert.False(Directory.Exists(salt));

            // the loader and the hub skip the dot-folder
            Assert.Empty(PackFolder.Folders(Mine));
            Assert.Empty(WorkshopShelf.Cards(Mine, null));

            RecentlyDeleted.Item item = RecentlyDeleted.Items(Mine).Single();
            Assert.Equal("salt_road", item.Id);
            Assert.Equal(30, item.DaysLeft(monday));
            Assert.Equal(20, item.DaysLeft(monday.AddDays(10)));

            // restored: back where it was, and it loads
            string back = RecentlyDeleted.Restore(item, Mine, _ => false, out problem);
            Assert.Null(problem);
            Assert.Equal(salt, back);
            Assert.True(Package.Read(back).Sound);

            // deleted again the same day, and its id taken meanwhile: a second stamp, then restored under the next free id
            string again = RecentlyDeleted.Delete(salt, monday, out _);
            RecentlyDeleted.Delete(Make(Mine, "Salt Road"), monday, out _);
            Assert.Equal(2, RecentlyDeleted.Items(Mine).Count);
            Assert.EndsWith("salt_road_2026-10-12_2", RecentlyDeleted.Items(Mine).Last().Folder);

            Make(Mine, "Salt Road");
            string restored = RecentlyDeleted.Restore(RecentlyDeleted.Items(Mine).First(i => i.Folder == again), Mine, _ => false, out problem);
            Assert.Null(problem);
            Assert.Equal(Path.Combine(Mine, "salt_road_2"), restored);
            Assert.True(Package.Read(restored).Sound, string.Join("\n", Package.Read(restored).Problems));

            // thirty days on it's still there; the day after, it goes when the hub opens
            Assert.Equal(0, RecentlyDeleted.Expire(Mine, monday.AddDays(30)));
            Assert.Equal(1, RecentlyDeleted.Expire(Mine, monday.AddDays(31)));
            Assert.Empty(RecentlyDeleted.Items(Mine));

            // Delete forever
            RecentlyDeleted.Delete(restored, monday, out _);
            Assert.Null(RecentlyDeleted.Forget(RecentlyDeleted.Items(Mine).Single()));
            Assert.Empty(RecentlyDeleted.Items(Mine));
        }

        [Fact]
        public void ADuplicateIsACampaignOfItsOwn()
        {
            string salt = Make(Mine, "Salt Road");
            CampaignEditor editor = CampaignEditor.Open(salt);
            editor.Edit("about", d => d.Description = "unsaved");
            editor.KeepDraft();

            string copy = WorkshopShelf.Duplicate(salt, _ => false, "Copy of Salt Road", out string problem);
            Assert.Null(problem);
            Assert.Equal(Path.Combine(Mine, "salt_road_2"), copy);
            Assert.False(Autosave.Has(copy));

            Package pack = Package.Read(copy);
            Assert.True(pack.Sound, string.Join("\n", pack.Problems));
            Assert.Empty(WorkshopProblems.Of(copy));
            Assert.Equal("Copy of Salt Road", CampaignFiles.Sheet(copy, "salt_road_2").Get(pack.Manifest.NameKey));
            Assert.Equal("Salt Road", CampaignFiles.Sheet(salt, "salt_road").Get("campaign.salt_road.name"));
        }

        // cc_task_m2 Part 6: one of the game's own campaigns, copied into My campaigns to be edited - a new id, based on
        // the original, and the original left exactly as it was
        [Fact]
        public void AnEditableCopyOfAGameCampaignIsMineAndBasedOnIt()
        {
            string original = Path.Combine(Game, "sample_millbrook");
            WorkshopTests.Copy(SampleCampaignTests.Folder, original);
            string before = string.Join("\n", Directory.EnumerateFiles(original, "*", SearchOption.AllDirectories)
                                                       .OrderBy(f => f).Select(File.ReadAllText));

            string copy = WorkshopShelf.EditableCopy(original, Mine, id => id == "sample_millbrook",
                                                     "Copy of Millbrook", out string problem);

            Assert.Null(problem);
            Assert.Equal(Path.Combine(Mine, "sample_millbrook_2"), copy);

            Package pack = Package.Read(copy);
            Assert.True(pack.Sound, string.Join("\n", pack.Problems));
            Assert.Equal("sample_millbrook_2", pack.Id);

            CampaignEditor editor = CampaignEditor.Open(copy);
            Assert.Equal("sample_millbrook", editor.Draft.BasedOn);
            Assert.Equal("Copy of Millbrook", editor.Draft.Name);
            Assert.Contains("\"based_on\": \"sample_millbrook\"", File.ReadAllText(Path.Combine(copy, WorkshopNotes.FileName)));

            // saved again, it's still based on it
            editor.Edit("about", d => d.Description = "mine now");
            Assert.Null(editor.Save());
            Assert.Equal("sample_millbrook", CampaignEditor.Open(copy).Draft.BasedOn);

            string after = string.Join("\n", Directory.EnumerateFiles(original, "*", SearchOption.AllDirectories)
                                                      .OrderBy(f => f).Select(File.ReadAllText));
            Assert.Equal(before, after);
        }

        [Fact]
        public void ARenameRespellsOnlyTheKeysSpelledWithTheCampaign()
        {
            Assert.Equal("campaign.new.name", CampaignRename.Key("campaign.old.name", "old", "new"));
            Assert.Equal("quest.new.old.title", CampaignRename.Key("quest.old.old.title", "old", "new"));
            Assert.Equal("dialogue.dm.line.new.old", CampaignRename.Key("dialogue.dm.line.old.old", "old", "new"));
            Assert.Equal("dialogue.wolf.bark.victory.new.001", CampaignRename.Key("dialogue.wolf.bark.victory.old.001", "old", "new"));
            Assert.Equal("actor.old.name", CampaignRename.Key("actor.old.name", "old", "new"));
            Assert.Equal("encounter.old.line.x", CampaignRename.Key("encounter.old.line.x", "old", "new"));
        }
    }
}
