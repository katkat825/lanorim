using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Dialogue;

namespace Content.Workshop.Story
{
    // A NODE'S BODY AS BLOCKS, AND BACK (cc_task_o Part 3, "no surprises"). Read takes the lines between "---" and "==="
    // into blocks; Write writes blocks as those lines, in the sample's own style (four spaces a level, "#line:" after the
    // words, "<<if>>" bodies indented). THE EDITOR'S SUBSET IS WHAT COMES BACK BYTE FOR BYTE: a node whose blocks write
    // back to exactly its own lines opens as a card, and any other - a command the editor has no block for, a tab, a line
    // laid out another way - opens as a Text card and is kept as written
    public static class StoryText
    {
        public const string Indent = "    ";

        // --- reading ----------------------------------------------------------------------------------------------

        // the blocks a body is, or null when it isn't the editor's subset
        public static List<Block> Read(IReadOnlyList<string> body)
        {
            var lines = body.ToList();
            int at = 0;

            try
            {
                List<Block> blocks = List(lines, ref at, 0);

                if (at != lines.Count) return null;

                return Write(blocks).SequenceEqual(lines) ? blocks : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        static int Depth(string line)
        {
            int spaces = line.Length - line.TrimStart(' ').Length;

            if (line.TrimStart(' ').StartsWith('\t') || spaces % Indent.Length != 0) throw new FormatException("indent");

            return spaces / Indent.Length;
        }

        static List<Block> List(List<string> lines, ref int at, int depth)
        {
            var blocks = new List<Block>();

            while (at < lines.Count)
            {
                string line = lines[at];

                if (line.Trim().Length == 0)
                {
                    if (line.Length > 0) throw new FormatException("spaces on a blank line");

                    // a blank line belongs to the list it's in only if that list carries on after it
                    int next = at;
                    while (next < lines.Count && lines[next].Length == 0) next++;
                    if (next < lines.Count && Depth(lines[next]) < depth) return Tidy(blocks);

                    blocks.Add(new Blank());
                    at++;
                    continue;
                }

                int d = Depth(line);

                if (d < depth) break;
                if (d > depth) throw new FormatException("indent");

                string content = line.Substring(d * Indent.Length);

                if (content.StartsWith("//"))
                {
                    blocks.Add(new Comment { Text = content.Substring(2) });
                    at++;
                }
                else if (content.StartsWith("->")) blocks.Add(Options(lines, ref at, depth));
                else if (content.StartsWith("<<"))
                {
                    string inside = Command(content);
                    string verb = inside.Split(' ')[0];

                    if (verb is "elseif" or "else" or "endif") break;

                    at++;

                    if (verb == "if") blocks.Add(If(lines, ref at, depth, inside.Substring(3)));
                    else blocks.Add(One(verb, inside.Length > verb.Length ? inside.Substring(verb.Length + 1) : ""));
                }
                else
                {
                    blocks.Add(SayOf(content));
                    at++;
                }
            }

            return Tidy(blocks);
        }

        // "<<check athletics 12>>" -> "check athletics 12"; anything else on the line is not the subset
        static string Command(string content)
        {
            if (!content.EndsWith(">>") || content.IndexOf(">>", StringComparison.Ordinal) != content.Length - 2) throw new FormatException("command");

            return content.Substring(2, content.Length - 4);
        }

        static Block One(string verb, string words)
        {
            switch (verb)
            {
                case "jump" when words.Length > 0 && !words.Contains(' '): return new Jump { Target = words };
                case "stop" when words.Length == 0: return new Stop();
                case "set":
                    Match set = Regex.Match(words, @"^(\$\w+) to (.+)$");
                    if (!set.Success) throw new FormatException("set");
                    return new Set { Variable = set.Groups[1].Value, Value = set.Groups[2].Value };
            }

            if (!Request.Verbs.ContainsKey(verb)) throw new FormatException("an unknown command");

            return new Do { Verb = verb, Words = words };
        }

        static Branch If(List<string> lines, ref int at, int depth, string condition)
        {
            var branch = new Branch();
            branch.Arms.Add(new Arm { Condition = condition, Blocks = List(lines, ref at, depth + 1) });

            while (true)
            {
                if (at >= lines.Count || Depth(lines[at]) != depth) throw new FormatException("an if with no endif");

                string inside = Command(lines[at].Substring(depth * Indent.Length));
                at++;

                if (inside.StartsWith("elseif ")) branch.Arms.Add(new Arm { Condition = inside.Substring(7), Blocks = List(lines, ref at, depth + 1) });
                else if (inside == "else") branch.Else = List(lines, ref at, depth + 1);
                else if (inside == "endif") return branch;
                else throw new FormatException("if");
            }
        }

        static Choice Options(List<string> lines, ref int at, int depth)
        {
            var choice = new Choice();

            while (at < lines.Count && lines[at].Length > 0 && Depth(lines[at]) == depth &&
                   lines[at].Substring(depth * Indent.Length).StartsWith("->"))
            {
                string content = lines[at].Substring(depth * Indent.Length);
                if (!content.StartsWith("-> ")) throw new FormatException("option");

                Say words = SayOf(content.Substring(3), allowCondition: true, out string condition);
                at++;

                choice.Options.Add(new Option { Text = words.Text, Tags = words.Tags, Condition = condition, Blocks = List(lines, ref at, depth + 1) });
            }

            return choice;
        }

        static Say SayOf(string content) => SayOf(content, false, out _);

        // "words #line:x #speaker:y" (an option's may have " <<if cond>>" before its tags)
        static Say SayOf(string content, bool allowCondition, out string condition)
        {
            condition = "";
            int tags = content.IndexOf(" #", StringComparison.Ordinal);
            string words = tags < 0 ? content : content.Substring(0, tags);

            if (allowCondition)
            {
                int iff = words.IndexOf(" <<if ", StringComparison.Ordinal);

                if (iff >= 0)
                {
                    if (!words.EndsWith(">>")) throw new FormatException("an option's condition");
                    condition = words.Substring(iff + 6, words.Length - iff - 8);
                    words = words.Substring(0, iff);
                }
            }

            if (words.Contains("<<") || words.Length == 0) throw new FormatException("a line with a command in it");

            var say = new Say { Text = words };

            if (tags >= 0) say.Tags = content.Substring(tags + 2).Split(" #").ToList();

            if (say.Tags.Any(t => t.Length == 0 || t.Contains(' '))) throw new FormatException("tags");

            return say;
        }

        // a command and the "Only if" that reads what it answered, straight after it, are one block (Do.After)
        static List<Block> Tidy(List<Block> blocks)
        {
            for (int i = 0; i + 1 < blocks.Count; i++)
                if (blocks[i] is Do act && act.After == null && blocks[i + 1] is Branch after && Reads(act.Verb, after))
                {
                    act.After = after;
                    blocks.RemoveAt(i + 1);
                }

            return blocks;
        }

        // the variables each command answers into (Requests, DialogueBook's EngineVariables)
        public static IReadOnlyList<string> Answers(string verb) => verb switch
        {
            "check" or "save" => new[] { RequestVariables.Passed, RequestVariables.Total, RequestVariables.Natural, RequestVariables.Consequence },
            "roll" => new[] { RequestVariables.Roll },
            "fight" => new[] { RequestVariables.Fight, RequestVariables.FightBroke },
            "encounter" => new[] { RequestVariables.Encounter, RequestVariables.EncounterFight },
            "loot" => new[] { RequestVariables.LootGold, RequestVariables.LootCopper },
            _ => Array.Empty<string>(),
        };

        static bool Reads(string verb, Branch branch) =>
            Answers(verb).Count > 0 && branch.Arms.All(a => Answers(verb).Any(v => Regex.IsMatch(a.Condition, Regex.Escape(v) + @"\b")));

        // --- writing ----------------------------------------------------------------------------------------------

        public static List<string> Write(IEnumerable<Block> blocks, int depth = 0)
        {
            var lines = new List<string>();
            string ind = string.Concat(Enumerable.Repeat(Indent, depth));

            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case Blank:
                        lines.Add("");
                        break;

                    case Comment comment:
                        lines.Add(ind + "//" + comment.Text);
                        break;

                    case Say say:
                        lines.Add(ind + say.Text + Tags(say.Tags));
                        break;

                    case Choice choice:
                        foreach (Option option in choice.Options)
                        {
                            lines.Add(ind + "-> " + option.Text + (option.Condition.Length > 0 ? $" <<if {option.Condition}>>" : "") + Tags(option.Tags));
                            lines.AddRange(Write(option.Blocks, depth + 1));
                        }
                        break;

                    case Jump jump:
                        lines.Add($"{ind}<<jump {jump.Target}>>");
                        break;

                    case Stop:
                        lines.Add(ind + "<<stop>>");
                        break;

                    case Set set:
                        lines.Add($"{ind}<<set {set.Variable} to {set.Value}>>");
                        break;

                    case Do act:
                        lines.Add($"{ind}<<{act.Verb}{(act.Words.Length > 0 ? " " + act.Words : "")}>>");
                        if (act.After != null) lines.AddRange(Write(new Block[] { act.After }, depth));
                        break;

                    case Branch branch:
                        for (int i = 0; i < branch.Arms.Count; i++)
                        {
                            lines.Add($"{ind}<<{(i == 0 ? "if" : "elseif")} {branch.Arms[i].Condition}>>");
                            lines.AddRange(Write(branch.Arms[i].Blocks, depth + 1));
                        }

                        if (branch.Else != null)
                        {
                            lines.Add(ind + "<<else>>");
                            lines.AddRange(Write(branch.Else, depth + 1));
                        }

                        lines.Add(ind + "<<endif>>");
                        break;
                }
            }

            return lines;
        }

        static string Tags(IEnumerable<string> tags) => string.Concat(tags.Select(t => " #" + t));
    }
}
