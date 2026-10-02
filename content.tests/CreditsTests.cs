using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Content.Screens;

namespace Content.Tests
{
    // THE CREDITS (cc_task_f Part 3): one data file, the SRD's notice first and verbatim, and every third-party file the
    // game ships credited to its source
    public class CreditsTests
    {
        static readonly CreditsView Credits = CreditsView.Srd();

        static string Game([CallerFilePath] string path = "") => Path.Combine(Path.GetDirectoryName(path), "..", "game");

        // the folders of game/ that hold what someone else made, and the files in them that are art, sound or type -
        // not the scenes, materials and scripts that wrap them, which are ours
        static readonly string[] Folders = { "models", "textures", "audio/samples", "fonts", "ui/kenney" };

        static readonly HashSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase)
        {
            ".gltf", ".glb", ".bin", ".obj", ".mtl", ".fbx", ".png", ".jpg", ".jpeg", ".webp", ".wav", ".ogg", ".mp3",
            ".ttf", ".otf", ".woff", ".woff2", ".txt",
        };

        static IEnumerable<string> Shipped()
        {
            string root = Path.GetFullPath(Game());

            return Folders.Select(f => Path.Combine(root, f))
                          .Where(Directory.Exists)
                          .SelectMany(f => Directory.EnumerateFiles(f, "*", SearchOption.AllDirectories))
                          .Where(f => Kinds.Contains(Path.GetExtension(f)))
                          .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'));
        }

        [Fact]
        public void TheSrdNoticeComesFirstAndIsTheSrdsOwnWords()
        {
            Credit srd = Credits.Ruleset;

            Assert.Same(srd, Credits.All.First());
            Assert.StartsWith("This work includes material from the System Reference Document 5.2.1 (“SRD 5.2.1”) " +
                              "by Wizards of the Coast LLC, available at https://www.dndbeyond.com/srd.", srd.Text);
            Assert.EndsWith("licensed under the Creative Commons Attribution 4.0 International License, available at " +
                            "https://creativecommons.org/licenses/by/4.0/legalcode.", srd.Text);
            Assert.Equal(CreditLicence.CcBy4, srd.Licence);
        }

        // THE GUARD: every third-party file in game/ - models, textures, sounds, fonts, the UI kit - has a credit for its
        // source, and that credit ships
        [Fact]
        public void EveryThirdPartyFileTheGameShipsHasACredit()
        {
            List<string> files = Shipped().ToList();

            Assert.True(files.Count > 100, $"only {files.Count} files found under {Game()}");

            string[] uncredited = files.Where(f => !Credits.Covering(f).Any(c => c.Ships)).ToArray();

            Assert.True(uncredited.Length == 0, "no credit covers:\n" + string.Join("\n", uncredited));
        }

        // and the other way: a credit that says it ships covers something that does, and one that says it doesn't
        // covers nothing - so it's credited the day it ships, and not before
        [Fact]
        public void ACreditShipsExactlyWhenSomethingOfItIsInTheGame()
        {
            List<string> files = Shipped().ToList();

            foreach (Credit credit in Credits.All.Where(c => c.Kind is CreditKind.Asset or CreditKind.Font))
            {
                bool covers = files.Any(f => credit.Files.Any(p => f.StartsWith(p, StringComparison.Ordinal)));
                Assert.True(credit.Ships == covers, $"{credit.Id}: ships {credit.Ships}, covers a file {covers}");
            }
        }

        // every author credited, whatever the licence: a Freesound clip names its author, a font its authors
        [Fact]
        public void EveryClipAndFontNamesItsAuthors()
        {
            Assert.All(Credits.All.Where(c => c.Name == "Freesound" || c.Kind == CreditKind.Font),
                       c => Assert.False(string.IsNullOrWhiteSpace(c.By), c.Id));
            Assert.All(Credits.All.Where(c => c.Kind != CreditKind.Contributor), c => Assert.NotNull(c.Licence));
        }

        [Fact]
        public void ThePageRunsMadeByAssetsFontsCodeAndHasNoContributorsYet()
        {
            Assert.Equal(new[] { CreditKind.MadeBy, CreditKind.Asset, CreditKind.Font, CreditKind.Code },
                         Credits.Sections.Select(s => s.Kind));
            Assert.Empty(Credits.Contributors);
            Assert.DoesNotContain(Credits.Sections.SelectMany(s => s.Credits), c => !c.Ships);
            Assert.Contains(Credits.All, c => c.Id == "yarn_spinner" && c.Licence == CreditLicence.Mit);
        }

        [Fact]
        public void AStrayKeyOrKindIsRefused()
        {
            Assert.False(CreditsView.TryRead("{ \"credits\": [ { \"id\": \"x\", \"kind\": \"asset\", \"name\": \"X\", " +
                                             "\"role\": \"models\", \"licence\": \"cc0\", \"colour\": \"red\" } ] }",
                                             out _, out IReadOnlyList<string> problems));
            Assert.Contains(problems, p => p.Contains("colour"));

            Assert.False(CreditsView.TryRead("{ \"credits\": [ { \"id\": \"x\", \"kind\": \"rumour\", \"name\": \"X\", " +
                                             "\"role\": \"models\" } ] }", out _, out problems));
            Assert.Contains(problems, p => p.Contains("kind"));
        }
    }
}
