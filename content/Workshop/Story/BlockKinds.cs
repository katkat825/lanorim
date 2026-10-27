using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Content.Workshop.Tables;

namespace Content.Workshop.Story
{
    // THE ADD BLOCK PALETTE (cc_task_o Part 2, cc_task_p Part 5): each kind of block by the word its name is keyed by,
    // grouped as the author sees them, and what a new one of each starts as. Every one is a Say, a Choice, a Jump, a
    // Stop, a Do (an engine command), a Set or a Branch underneath (Blocks.cs), so the palette is words and starting
    // values, not new shapes
    public static class BlockKinds
    {
        public static readonly (string Group, string[] Kinds)[] Palette =
        {
            ("talk", new[] { "say", "note" }),
            ("rolls", new[] { "check", "save", "roll" }),
            ("fights", new[] { "fight", "encounter", "loot" }),
            ("rewards", new[] { "coins", "give", "level" }),
            ("places", new[] { "rest", "shop", "armory" }),
            ("companion", new[] { "bark" }),
            ("remember", new[] { "remember", "forget", "count", "only_if" }),
            ("go", new[] { "jump", "stop" }),
        };

        public static IEnumerable<string> All => Palette.SelectMany(p => p.Kinds).Concat(new[] { "comment", "blank", "xp" });

        // the palette word for a command: <<gold>>, <<silver>> and <<copper>> are all "coins"; <<level>> and <<xp>> are
        // "level" (the one the campaign's levelling uses is offered)
        public static string Of(Do act) => act.Verb switch
        {
            "gold" or "silver" or "copper" => "coins",
            "xp" => "xp",
            _ => act.Verb,
        };

        // remember (true), forget (false), or a count (a number, or the variable plus or minus one)
        public static string Of(Set set) => set.Value switch
        {
            "true" => "remember",
            "false" => "forget",
            _ => "count",
        };

        // the words a new block starts with: a line's and an option's (the game's locale, said by the page), and the
        // first card that isn't this one (a go-to's)
        public sealed record Words(string Line, string Option, string OtherCard, string Memory, string Number);

        // A NEW BLOCK OF A KIND: its lines given ids now (made from the card's name), its ids the campaign's first of
        // their kind where it names one (the first fight, the first loot table...)
        public static Block Make(string kind, CampaignDraft draft, CardDraft card, Words words)
        {
            StoryDraft story = draft.Story;
            TablesDraft tables = draft.Tables;

            string First(IEnumerable<string> ids, string otherwise) => ids.FirstOrDefault() ?? otherwise;

            return kind switch
            {
                "say" => new Say { Text = words.Line, LineId = story.NewLineId(card.Title) },
                "note" => Note(story, card, words),
                "jump" => new Jump { Target = words.OtherCard.Length > 0 ? words.OtherCard : card.Title },
                "stop" => new Stop(),

                "check" => Test("check", "athletics 15"),
                "save" => Test("save", "dex 15"),
                "roll" => new Do { Verb = "roll", Words = "1d20" },

                "fight" => new Do { Verb = "fight", Words = First(tables.Fights.Select(f => f.Fight.Id), "a_fight") },
                "encounter" => new Do { Verb = "encounter", Words = First(tables.RandomTables(_ => true).Select(t => t.Id), "a_table") },
                "loot" => new Do { Verb = "loot", Words = First(tables.Loot.Select(t => t.Id), "a_table") },

                "coins" => new Do { Verb = "gold", Words = "10" },
                "give" => new Do { Verb = "give", Words = "torch" },
                "level" => draft.Leveling == Leveling.Xp ? new Do { Verb = "xp", Words = "100" } : new Do { Verb = "level", Words = "2" },
                "xp" => new Do { Verb = "xp", Words = "100" },

                "rest" => new Do { Verb = "rest", Words = "short" },
                "shop" => new Do { Verb = "shop", Words = First(tables.Shops.Where(s => !s.Armory).Select(s => s.Id), MerchantReader.StartingShopId) },
                "armory" => new Do { Verb = "armory", Words = First(tables.Shops.Where(s => s.Armory).Select(s => s.Id), "an_armory") },

                "bark" => new Do { Verb = "bark", Words = "danger" },

                "remember" => new Set { Variable = Memory.Variable(words.Memory), Value = "true" },
                "forget" => new Set { Variable = Memory.Variable(words.Memory), Value = "false" },
                "count" => new Set { Variable = Memory.Variable(words.Number), Value = Memory.Variable(words.Number) + " + 1" },
                "only_if" => new Branch { Arms = { new Arm { Condition = RequestVariables.Passed } } },

                "comment" => new Comment { Text = " " },
                _ => new Blank(),
            };
        }

        // a check or a save, with its pass and its fail (the editor writes the "Only if" after it)
        static Do Test(string verb, string words) => new()
        {
            Verb = verb,
            Words = words,
            After = new Branch { Arms = { new Arm { Condition = RequestVariables.Passed } }, Else = new List<Block>() },
        };

        static Choice Note(StoryDraft story, CardDraft card, Words words)
        {
            var note = new Choice();
            var made = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < 2; i++)
            {
                var option = new Option { Text = words.Option };
                option.LineId = story.NewLineId(card.Title, made);
                made.Add(option.LineId);
                note.Options.Add(option);
            }

            return note;
        }
    }
}
