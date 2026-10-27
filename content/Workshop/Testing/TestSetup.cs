using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Creation;
using Content.Schema;
using Content.Sheet;

namespace Content.Workshop.Testing
{
    // THE TEST HERO AND THE TEST STATE (cc_task_q Part 2), remembered per campaign (in its .draft/, which the game never
    // reads): the hero's class, species and level, the class's own gear, every spell at that level if wanted, extra items
    // and gold, and what the story remembers before the first line ("as if Tavin was brave and Korr trusts you 2")
    public sealed class TestSetup
    {
        public string Class { get; set; } = "fighter";

        public string Species { get; set; } = "human";

        public int Level { get; set; } = 1;

        public bool EverySpell { get; set; }

        // extra items: id -> how many
        public Dictionary<string, int> Items { get; set; } = new(StringComparer.Ordinal);

        public int Gold { get; set; }

        // what the story remembers when the test starts: "$tavin_was_brave" -> "true", "$korr_trusts_you" -> "2"
        public Dictionary<string, string> Memory { get; set; } = new(StringComparer.Ordinal);

        // where the test starts: a card, or "" for the campaign's own start
        public string Start { get; set; } = "";

        // Play the campaign, as a player would, through creation and the starting shop (false: the quick test hero)
        public bool AsAPlayer { get; set; }

        // --- remembered per campaign ---------------------------------------------------------------------------------

        public const string FileName = "test.json";

        static string PathIn(string campaign) => System.IO.Path.Combine(campaign, Autosave.Folder, FileName);

        static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        public static TestSetup For(string campaign)
        {
            try
            {
                string file = PathIn(campaign);
                return File.Exists(file) ? JsonSerializer.Deserialize<TestSetup>(File.ReadAllText(file), Options) ?? new TestSetup() : new TestSetup();
            }
            catch (Exception wrong) when (wrong is JsonException or IOException)
            {
                return new TestSetup();
            }
        }

        public void Keep(string campaign)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathIn(campaign)));
                File.WriteAllText(PathIn(campaign), JsonSerializer.Serialize(this, Options));
            }
            catch (IOException) { }
        }

        // --- the hero ---------------------------------------------------------------------------------------------

        // THE TEST HERO: made as `--begin` makes one (QuickHero: every default, the class's gear, Creation's suggested
        // traits), at the level and species asked for, then its extras. Every spell: each spell of its lists up to the
        // highest level it can cast, known (QuickHero learns the first ones it's offered)
        public Hero Hero(Library library, string name)
        {
            Hero hero = QuickHero.Make(library, Class, Level, name, species: Species);

            if (EverySpell) hero.KnowEverySpell(library.Spells);

            foreach ((string item, int count) in Items)
                if (library.Items.Find(item) is { } found)
                    hero.Pack.Take(found, Math.Max(1, count));

            if (Gold > 0) hero.Pack.Earn(Inventory.Coins.FromGold(Gold));

            // a quick test hero goes straight to the story, in the best of what it has; a player's way keeps the shop
            if (!AsAPlayer)
            {
                hero.ShopsFirst = false;
                hero.WearTheBest();
            }

            return hero;
        }

        // the same setup, started somewhere else
        public TestSetup Copy(string start)
        {
            TestSetup copy = JsonSerializer.Deserialize<TestSetup>(JsonSerializer.Serialize(this, Options), Options);
            copy.Start = start ?? "";
            return copy;
        }

        public override string ToString() => $"{Species} {Class} {Level}" + (Start.Length > 0 ? $" from {Start}" : "");
    }
}
