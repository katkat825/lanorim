using System;
using System.IO;
using System.Linq;
using Content.Screens;

namespace Content.Tests
{
    // THE BOOK'S CONTENTS PAGE IS THE MAIN MENU (cc_task_m2 Part 6): every way back to it says "Main menu", and nothing
    // the player reads calls it the contents or the book any more. The keys keep their names (backend names stay)
    public class MainMenuWordsTests
    {
        static readonly string[] Rows = File.ReadAllLines("game.csv");

        static string English(string key) =>
            Rows.Select(r => r.Split(',', 2)).Where(r => r[0] == key).Select(r => r[1].Trim('"')).Single();

        [Theory]
        [InlineData("ui.book.contents")]     // the campaign pages, the Workshop hub, Settings, Credits, Tutorials
        [InlineData("ui.pause.to_the_book")] // the pause menu, the starting shop
        [InlineData("ui.death.to_the_book")] // the death screen
        public void EveryWayBackSaysMainMenu(string key) => Assert.Equal("Main menu", English(key));

        [Fact]
        public void TheKeyTheScreensUseIsTheOneTested() => Assert.Equal("ui.book.contents", CampaignBook.ContentsKey);

        [Fact]
        public void NoScreenWordsCallItTheContentsOrTheBookAnyMore()
        {
            string[] said = Rows.Where(r => r.StartsWith("ui.", StringComparison.Ordinal))
                                .Select(r => r.Split(',', 2))
                                .Where(r => r[0] != "ui.end.blurb" &&
                                            (r[1].Contains("Contents", StringComparison.OrdinalIgnoreCase) ||
                                             r[1].Contains("back to the book", StringComparison.OrdinalIgnoreCase) ||
                                             r[1].Contains("open the book", StringComparison.OrdinalIgnoreCase) ||
                                             r[1].Contains("in the book", StringComparison.OrdinalIgnoreCase)))
                                .Select(r => string.Join(",", r))
                                .ToArray();

            // the end's "Your character waits in the book" is the campaign book itself, not its contents page
            Assert.True(said.Length == 0, string.Join("\n", said));
        }
    }
}
