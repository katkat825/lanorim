using System;
using System.IO;
using System.Linq;
using Content.Saves;
using Content.Schema;

namespace Content.Tests
{
    // Tier 2.7 of the 2026-09-24 run: autosave on events, every manual save kept, the last ten
    // autosaves per character, reload on death, five characters per campaign - and the story's
    // place and variables coming back with the rest
    public class SaveLibraryTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "lanorim_saves_" + Guid.NewGuid().ToString("N"));

        DateTime _now = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

        SaveLibrary Library() => new SaveLibrary(_root, () => _now = _now.AddSeconds(1));

        static SaveGame Game(int slot = 0, string node = "gate") =>
            new SaveGame { Campaign = "sample", Slot = slot, Node = node };

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Fact]
        public void AutosavesKeepTheLastTenAndManualSavesAllStay()
        {
            SaveLibrary saves = Library();

            for (int i = 0; i < 3; i++) saves.Save(Game(), SaveKind.Manual, $"mine {i}");
            for (int i = 0; i < 14; i++) saves.Save(Game(node: $"n{i}"), SaveKind.FightStart);

            var all = saves.Of("sample", 0);

            Assert.Equal(3, all.Count(s => s.Game.Kind == SaveKind.Manual));
            Assert.Equal(SaveLibrary.AutosavesKept, all.Count(s => s.Game.Kind != SaveKind.Manual));
            Assert.Equal("n13", all.First().Game.Node);
        }

        [Fact]
        public void DeathReloadsTheNewestSave()
        {
            SaveLibrary saves = Library();

            saves.Save(Game(node: "road"), SaveKind.ChapterStart);
            saves.Save(Game(node: "ambush"), SaveKind.FightStart);

            Assert.Equal("ambush", saves.ForReload("sample", 0).Game.Node);
            Assert.Equal("ambush", saves.Newest().Game.Node);
        }

        [Fact]
        public void FiveCharactersACampaignAndNoSixth()
        {
            SaveLibrary saves = Library();

            for (int slot = 0; slot < 5; slot++)
            {
                Assert.Equal(slot, saves.FreeSlot("sample"));
                saves.Save(Game(slot), SaveKind.ChapterStart);
            }

            Assert.Equal(-1, saves.FreeSlot("sample"));
            Assert.Throws<ArgumentOutOfRangeException>(() => saves.Save(Game(5), SaveKind.Manual));

            saves.Forget("sample", 2);
            Assert.Equal(2, saves.FreeSlot("sample"));
        }

        [Fact]
        public void TheStoryComesBackWithTheSave()
        {
            SaveGame game = Game(node: "camp");
            game.Numbers["$gold_found"] = 12;
            game.Words["$fight"] = "won";
            game.Flags["$met_the_hermit"] = true;

            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(game));

            Assert.True(read.Ok, string.Join("\n", read.Problems));
            Assert.Equal("camp", read.Value.Node);
            Assert.Equal(12f, read.Value.Numbers["$gold_found"]);
            Assert.Equal("won", read.Value.Words["$fight"]);
            Assert.True(read.Value.Flags["$met_the_hermit"]);
        }
    }
}
