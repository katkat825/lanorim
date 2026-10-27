using System;
using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Content.Workshop.Tables;
using Core.Characters;
using Core.Resolution;
using Core.Words;

namespace Content.Workshop.Story
{
    // CHECKS AND SAVES, MADE EASY (cc_task_p Part 1): how hard in words (the SRD's ladder Requests.ReadDc reads), TWO KEYS
    // built in - where TwoKeys warns, the fix that adds another way through (a choice: this skill, or one on another
    // ability; or a way without a roll) - and FAILING MOVES FORWARD: starters for an empty fail
    public static class Rolls
    {
        // Easy, Medium, Hard, Very hard: the DCs the ladder reads (Difficulties.Ladder), the first four
        public static IReadOnlyList<(Difficulty Rung, int Dc)> Ladder =>
            Difficulties.Ladder.Where(d => d.Dc() is >= 10 and <= 25).Select(d => (d, d.Dc())).ToList();

        // "athletics 15" -> athletics, 15 (a DC written as a word read through the ladder)
        public static (string What, int Dc) Read(Do test)
        {
            string[] args = test.Args;
            int dc = 15;

            if (args.Length > 1 && !int.TryParse(args[1], out dc))
                dc = Difficulties.Ladder.FirstOrDefault(d => d.Id() == args[1]).Dc();

            return (args.Length > 0 ? args[0] : "", dc);
        }

        public static void Write(Do test, string what, int dc) => test.Words = $"{what} {dc}";

        // the ability a skill (or an ability) is on
        public static Ability AbilityOf(string what) =>
            EnumWords.TryParse(what, out Skill skill) ? skill.Governs() : EnumWords.TryParse(what, out Ability ability) ? ability : Ability.Strength;

        // DOES TWO KEYS WARN ABOUT THIS CHECK? the card written alone, as the campaign check reads it
        public static bool NoWayPast(CardDraft card, Do check) =>
            check.Verb == "check" && TwoKeys.Warnings("x", card.Write("\n")).Any(w => w.What.Contains($"the {Read(check).What} check"));

        // skills on another ability than this one, for "another way"
        public static IEnumerable<Skill> OtherWays(string what)
        {
            Ability not = AbilityOf(what);
            return Enum.GetValues<Skill>().Where(s => s.Governs() != not);
        }

        // ADD ANOTHER WAY: the check becomes one option of a note, the other a check of another skill with the same pass.
        // `words` are the options' words ("Climb it", "Find another way"), already said by the page
        public static Choice AnotherWay(StoryDraft story, CardDraft card, Do check, Skill other, string mine, string theirs)
        {
            Do second = Copy(check);
            Write(second, EnumWords.Id(other), Read(check).Dc);
            Renumber(story, card, second);

            return Note(story, card, (mine, new List<Block> { check }), (theirs, new List<Block> { second }));
        }

        // ADD A WAY WITHOUT A ROLL: the check one option, the other its pass, no roll
        public static Choice WithoutARoll(StoryDraft story, CardDraft card, Do check, string mine, string round)
        {
            List<Block> pass = check.After?.Arms.FirstOrDefault()?.Blocks.Select(Copy).ToList() ?? new List<Block>();
            Renumber(story, card, pass.ToArray());

            return Note(story, card, (mine, new List<Block> { check }), (round, pass));
        }

        static Choice Note(StoryDraft story, CardDraft card, params (string Words, List<Block> Blocks)[] options)
        {
            var note = new Choice();
            var made = new HashSet<string>(StringComparer.Ordinal);

            foreach ((string words, List<Block> blocks) in options)
            {
                var option = new Option { Text = words, Blocks = blocks };
                option.LineId = story.NewLineId(card.Title, made);
                made.Add(option.LineId);
                note.Options.Add(option);
            }

            return note;
        }

        static T Copy<T>(T block) where T : Block =>
            (T)System.Text.Json.JsonSerializer.Deserialize<Block>(System.Text.Json.JsonSerializer.Serialize<Block>(block));

        static void Renumber(StoryDraft story, CardDraft card, params Block[] blocks)
        {
            var made = new HashSet<string>(StringComparer.Ordinal);

            foreach (Block block in Story.Blocks.All(blocks))
            {
                if (block is Say say && say.LineId.Length > 0) made.Add(say.LineId = story.NewLineId(card.Title, made));
                if (block is Choice choice)
                    foreach (Option option in choice.Options.Where(o => o.LineId.Length > 0)) made.Add(option.LineId = story.NewLineId(card.Title, made));
            }
        }

        // --- failing moves forward ----------------------------------------------------------------------------------

        public static readonly string[] Starters = { "hurt", "time", "spotted", "lose" };

        // A FAIL STARTER'S BLOCKS: the GM's line for it (said by the page) and what it does. "hurt" is a line only: no
        // command takes hit points outside a fight (the lucky and unlucky rolls are the game's own way)
        public static List<Block> Starter(string starter, string line, StoryDraft story, CardDraft card, TablesDraft tables)
        {
            var say = new Say { Text = line, LineId = story.NewLineId(card.Title) };
            var blocks = new List<Block> { say };

            switch (starter)
            {
                case "time":
                    string table = tables.RandomTables(_ => true).Select(t => t.Id).FirstOrDefault() ?? "a_table";
                    blocks.Add(new Do { Verb = "encounter", Words = table });
                    break;
                case "spotted":
                    blocks.Add(new Do { Verb = "fight", Words = tables.Fights.Select(f => f.Fight.Id).FirstOrDefault() ?? "a_fight" });
                    break;
                case "lose":
                    blocks.Add(new Do { Verb = "silver", Words = "-5" });
                    break;
            }

            return blocks;
        }
    }
}
