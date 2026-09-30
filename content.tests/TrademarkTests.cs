using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Schema;

namespace Content.Tests
{
    // NO WotC TRADEMARKS IN ANYTHING A PLAYER READS (cc_task_ui-issues-9-30.md 1.5). "Spell slots
    // (classic D&D)" broke the project's own rule (_design_docs/srd_legal_decisions.md, THIRD_PARTY.md):
    // build on the SRD, use no trademark and imply no affiliation. Every locale file and every
    // campaign's text is read for the marks. The one exception is the SRD's CC-BY attribution notice,
    // which must name Wizards of the Coast and link the SRD, and is allowed by its key alone
    public class TrademarkTests
    {
        static readonly Regex Marks = new Regex(
            @"\bD\s*&\s*D\b|\bDnD\b|Dungeons\s*(&|and)\s*Dragons|\b5e\b|\b5th\s+edition\b|" +
            @"Wizards\s+of\s+the\s+Coast|\bWotC\b|D&D\s*Beyond|dndbeyond|Forgotten\s+Realms",
            RegexOptions.IgnoreCase);

        // the exact CC-BY notice, copied verbatim from the SRD 5.2.1 legal page, when it lands
        static readonly HashSet<string> Attribution = new HashSet<string>(StringComparer.Ordinal)
        {
            "ui.credits.srd_attribution",
        };

        static IEnumerable<string> LocaleFiles() =>
            new[] { "game.csv" }.Concat(Directory.GetFiles(Campaigns, "*.csv", SearchOption.AllDirectories));

        static string Campaigns => Path.Combine(AppContext.BaseDirectory, "campaigns");

        [Fact]
        public void TheMarksAreCaught()
        {
            foreach (string bad in new[] { "classic D&D", "Dungeons and Dragons", "for 5e", "WotC", "D & D", "dnd" })
                Assert.Matches(Marks, bad);

            foreach (string fine in new[] { "5 feet", "the 5th level", "a D6", "Ddraig", "and", "damage" })
                Assert.DoesNotMatch(Marks, fine);
        }

        [Fact]
        public void NoLocaleLineCarriesATrademark()
        {
            var found = new List<string>();

            foreach (string file in LocaleFiles())
            {
                Assert.True(File.Exists(file), file);

                foreach ((string key, string english) in Locale.Read(File.ReadAllText(file)))
                    if (!Attribution.Contains(key) && Marks.IsMatch(english))
                        found.Add($"{Path.GetFileName(file)}: {key} = {english}");
            }

            Assert.True(found.Count == 0, string.Join("\n", found));
        }

        [Fact]
        public void NoCampaignTextCarriesATrademark()
        {
            var found = Directory.GetFiles(Campaigns, "*", SearchOption.AllDirectories)
                                 .Where(f => f.EndsWith(".yarn") || f.EndsWith(".json") || f.EndsWith(".map"))
                                 .SelectMany(f => File.ReadAllLines(f).Where(l => Marks.IsMatch(l))
                                                      .Select(l => $"{Path.GetFileName(f)}: {l.Trim()}"))
                                 .ToList();

            Assert.True(found.Count == 0, string.Join("\n", found));
        }
    }
}
