using System.Collections.Generic;
using System.Linq;
using Content.Spells;
using Core.Magic;

namespace Content.Tests
{
    // "if spell exists in the game then it should be available wherever srd says it should be available"
    // (Kathleen, 2026-10-05; cc_task_f 1.2). every spell the game ships is on every v1 class list the SRD 5.2.1
    // gives it. the SRD's lists are in content/srd/reference/spell_names.json, from the SRD text
    public class SpellClassListTests
    {
        static readonly SpellBook Book = SpellBook.Srd();

        // which SRD class lists feed which v1 class (docs/v1_class_roster.md): the Mage is merged from the
        // Wizard, the Warlock and the Sorcerer, and the Bard's spells are on its list (decisions_checklist.md §2,
        // `09-25 Q19`). the Ranger is route-covered by the Rogue and the Druid, but its list feeds no v1 class:
        // Hunter's Mark on the Druid's is the one spell carried over, by hand (v1_class_roster.md "kept on purpose")
        static readonly Dictionary<string, string> Feeds = new()
        {
            ["wizard"] = "mage", ["warlock"] = "mage", ["sorcerer"] = "mage", ["bard"] = "mage",
            ["cleric"] = "cleric", ["druid"] = "druid", ["paladin"] = "paladin",
        };

        // spell id -> v1 class it may leave off, and why. empty: every shipped spell is on every list today
        static readonly Dictionary<(string Spell, string Class), string> Allowed = new();

        [Fact]
        public void EveryShippedSpellIsOnEveryV1ClassListTheSrdGivesIt()
        {
            var missing = new List<string>();

            foreach (Spell spell in Book.All)
            {
                IReadOnlyList<string> srd = SrdSpellNames.ClassesOf(spell.Id);

                if (srd == null)
                {
                    missing.Add($"{spell.Id}: no SRD spell by this id - say which, or allow it here");
                    continue;
                }

                foreach (string v1 in srd.Where(Feeds.ContainsKey).Select(c => Feeds[c]).Distinct())
                    if (!spell.Classes.Contains(v1) && !Allowed.ContainsKey((spell.Id, v1)))
                        missing.Add($"{spell.Id}: the SRD has it on {string.Join(", ", srd)}, so it goes on the {v1}'s");
            }

            Assert.True(missing.Count == 0, string.Join("\n", missing));
        }

        [Fact]
        public void TheSrdListsAreRead()
        {
            Assert.Equal(new[] { "bard", "druid", "ranger", "wizard" }, SrdSpellNames.ClassesOf("longstrider"));
            Assert.Equal(new[] { "ranger" }, SrdSpellNames.ClassesOf("hunters_mark"));
            Assert.Null(SrdSpellNames.ClassesOf("cube_of_force"));
        }

        [Fact]
        public void TheWoodElfsSpellsAreOnTheDruidsListAndLongstriderOnTheMages()
        {
            Assert.Equal(new[] { "druid" }, Book.Find("druidcraft").Classes);
            Assert.Equal(new[] { "druid", "mage" }, Book.Find("longstrider").Classes);
        }
    }
}
