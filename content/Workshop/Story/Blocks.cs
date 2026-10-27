using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Content.Workshop.Story
{
    // WHAT A CARD IS MADE OF (cc_task_o Part 2, cc_task_p): blocks, top to bottom, each a line or a command of the card's
    // Yarn node - and a block that branches (a note's options, a check's pass and fail, an "Only if") holds block lists of
    // its own. The editor's model is this list; StoryWriter writes it as Yarn and StoryReader reads Yarn back into it.
    //
    // P's blocks are all a Do (an engine command, its words, and what happens after) or a Set or a Branch, so P adds forms
    // over these, not new shapes (cc_task_o: "O's model must already have room for P's blocks")
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "block")]
    [JsonDerivedType(typeof(Say), "say")]
    [JsonDerivedType(typeof(Choice), "choice")]
    [JsonDerivedType(typeof(Jump), "jump")]
    [JsonDerivedType(typeof(Stop), "stop")]
    [JsonDerivedType(typeof(Do), "do")]
    [JsonDerivedType(typeof(Set), "set")]
    [JsonDerivedType(typeof(Branch), "branch")]
    [JsonDerivedType(typeof(Blank), "blank")]
    [JsonDerivedType(typeof(Comment), "comment")]
    public abstract class Block
    {
        // every block list this one holds (an option's, a branch arm's), for a walk over a whole card
        public virtual IEnumerable<List<Block>> Inside => Enumerable.Empty<List<Block>>();
    }

    // SOMEONE SAYS... a line: its words and its tags (line:arrive, speaker:oda, and any others, in the order written)
    public sealed class Say : Block
    {
        public string Text { get; set; } = "";

        public List<string> Tags { get; set; } = new();

        [JsonIgnore]
        public string LineId
        {
            get => Tag(Tags, StoryWords.LineTag);
            set => SetTag(Tags, StoryWords.LineTag, value, first: true);
        }

        // the line's own speaker, or "" for the card's
        [JsonIgnore]
        public string Speaker
        {
            get => Tag(Tags, StoryWords.SpeakerTag);
            set => SetTag(Tags, StoryWords.SpeakerTag, value, first: false);
        }

        public static string Tag(List<string> tags, string name) =>
            tags.FirstOrDefault(t => t.StartsWith(name + ":"))?.Substring(name.Length + 1) ?? "";

        public static void SetTag(List<string> tags, string name, string value, bool first)
        {
            int at = tags.FindIndex(t => t.StartsWith(name + ":"));

            if (string.IsNullOrEmpty(value))
            {
                if (at >= 0) tags.RemoveAt(at);
                return;
            }

            string tag = name + ":" + value;

            if (at >= 0) tags[at] = tag;
            else if (first) tags.Insert(0, tag);
            else
            {
                int line = tags.FindIndex(t => t.StartsWith(StoryWords.LineTag + ":"));
                tags.Insert(line + 1, tag);
            }
        }
    }

    // THE GM HANDS A NOTE: options A, B, C..., each with the blocks under it. After them the card carries on
    public sealed class Choice : Block
    {
        public List<Option> Options { get; set; } = new();

        public override IEnumerable<List<Block>> Inside => Options.Select(o => o.Blocks);
    }

    public sealed class Option
    {
        public string Text { get; set; } = "";

        // "" always shown; otherwise the Yarn condition it's shown under ("$hero_class == \"wizard\"", P's Only if)
        public string Condition { get; set; } = "";

        public List<string> Tags { get; set; } = new();

        public List<Block> Blocks { get; set; } = new();

        [JsonIgnore]
        public string LineId
        {
            get => Say.Tag(Tags, StoryWords.LineTag);
            set => Say.SetTag(Tags, StoryWords.LineTag, value, first: true);
        }
    }

    // GO TO CARD...
    public sealed class Jump : Block
    {
        public string Target { get; set; } = "";
    }

    // THE STORY ENDS
    public sealed class Stop : Block
    {
    }

    // AN ENGINE COMMAND (P): <<check athletics 12>>, <<fight cellar_rats>>, <<gold 25>>... its verb, the words after it as
    // written, and what happens after it: the "Only if" that reads what it answered ($passed, $fight, $roll, $loot_gold,
    // $encounter), when one follows it straight away
    public sealed class Do : Block
    {
        public string Verb { get; set; } = "";

        public string Words { get; set; } = "";

        public Branch After { get; set; }

        [JsonIgnore]
        public string[] Args => Words.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

        public override IEnumerable<List<Block>> Inside => After?.Inside ?? Enumerable.Empty<List<Block>>();
    }

    // REMEMBER THAT... (P): <<set $tavin_was_brave to true>>, a count's <<set $korr_trusts_you to $korr_trusts_you + 1>>
    public sealed class Set : Block
    {
        public string Variable { get; set; } = "";

        public string Value { get; set; } = "";
    }

    // ONLY IF... (P): <<if>>, any <<elseif>>, maybe <<else>>, each arm a block list
    public sealed class Branch : Block
    {
        public List<Arm> Arms { get; set; } = new();

        // null: no <<else>>
        public List<Block> Else { get; set; }

        public override IEnumerable<List<Block>> Inside => Arms.Select(a => a.Blocks).Concat(Else == null ? Enumerable.Empty<List<Block>>() : new[] { Else });
    }

    public sealed class Arm
    {
        public string Condition { get; set; } = "";

        public List<Block> Blocks { get; set; } = new();
    }

    // a blank line, kept where the author left one
    public sealed class Blank : Block
    {
    }

    // a // note to self, kept as written
    public sealed class Comment : Block
    {
        public string Text { get; set; } = "";
    }

    public static class Blocks
    {
        // every block in a list and in every list inside it, depth first
        public static IEnumerable<Block> All(IEnumerable<Block> blocks)
        {
            foreach (Block block in blocks ?? Enumerable.Empty<Block>())
            {
                yield return block;

                foreach (List<Block> inside in block.Inside)
                    foreach (Block deeper in All(inside))
                        yield return deeper;
            }
        }

        // every line a list speaks (says and options), with the speaker of each ("" for the card's)
        public static IEnumerable<(string Id, string Text, string Speaker)> Lines(IEnumerable<Block> blocks)
        {
            foreach (Block block in All(blocks))
            {
                if (block is Say say && say.LineId.Length > 0) yield return (say.LineId, say.Text, say.Speaker);

                if (block is Choice choice)
                    foreach (Option option in choice.Options.Where(o => o.LineId.Length > 0))
                        yield return (option.LineId, option.Text, Say.Tag(option.Tags, StoryWords.SpeakerTag));
            }
        }
    }
}
