using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Campaigns;
using Content.Dialogue;

namespace Content.Workshop.Story
{
    // WHAT THE STORY REMEMBERS (cc_task_p Part 4): "Tavin was brave", "Korr trusts you" - Yarn variables the author never
    // sees. A phrase makes its variable ($tavin_was_brave, shown only in Advanced); the phrase as typed is kept in
    // workshop.json beside it. The memory list is every one the campaign's cards set or check, where, and what's odd
    // about it: set and never checked (amber), checked and never set (red: it's always false). No declaration is
    // written: Yarn infers a variable's type from the first <<set>> (the stories so far declare none either)
    public static class Memory
    {
        public sealed record Remembered(string Variable, bool Count, IReadOnlyList<string> SetIn, IReadOnlyList<string> CheckedIn)
        {
            public bool NeverChecked => SetIn.Count > 0 && CheckedIn.Count == 0;

            public bool NeverSet => CheckedIn.Count > 0 && SetIn.Count == 0;
        }

        // "$tavin_was_brave" from "Tavin was brave"
        public static string Variable(string phrase) => "$" + ContentId.From(phrase, "something");

        // the phrase a variable is shown as: the author's own, or its words
        public static string Phrase(string variable, IReadOnlyDictionary<string, string> phrases) =>
            phrases != null && phrases.TryGetValue(variable, out string said) && said.Length > 0 ? said
                : Tables.TablesDraft.Plain((variable ?? "").TrimStart('$'));

        static readonly Regex Variables = new(@"\$[a-z_][a-z0-9_]*", RegexOptions.Compiled);

        // the engine's and the hero's own: never the author's to remember
        static bool Engine(string variable) =>
            HeroVariables.Is(variable) || typeof(RequestVariables).GetFields().Any(f => (string)f.GetValue(null) == variable);

        // EVERY THING THE STORY REMEMBERS, by its variable: the cards that set it and the cards that check it
        public static IReadOnlyList<Remembered> List(StoryDraft story)
        {
            var set = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            var check = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            var counts = new HashSet<string>(StringComparer.Ordinal);

            void Add(Dictionary<string, SortedSet<string>> into, string variable, string card)
            {
                if (Engine(variable)) return;
                if (!into.TryGetValue(variable, out SortedSet<string> cards)) into[variable] = cards = new SortedSet<string>(StringComparer.Ordinal);
                cards.Add(card);
            }

            foreach (CardDraft card in story.Cards)
            {
                if (card.IsText)
                {
                    foreach (Match m in Regex.Matches(card.Text, @"<<set\s+(\$\w+)\s+to\s+([^>]*)>>"))
                    {
                        Add(set, m.Groups[1].Value, card.Title);
                        if (m.Groups[2].Value.Trim() is not ("true" or "false")) counts.Add(m.Groups[1].Value);
                    }

                    foreach (Match m in Regex.Matches(card.Text, @"<<(?:if|elseif)\s+([^>]*)>>"))
                        foreach (Match v in Variables.Matches(m.Groups[1].Value)) Add(check, v.Value, card.Title);

                    continue;
                }

                foreach (Block block in Blocks.All(card.Blocks))
                {
                    if (block is Set s)
                    {
                        Add(set, s.Variable, card.Title);
                        if (s.Value is not ("true" or "false")) counts.Add(s.Variable);
                    }

                    foreach (string condition in Conditions(block))
                        foreach (Match v in Variables.Matches(condition)) Add(check, v.Value, card.Title);
                }
            }

            return set.Keys.Concat(check.Keys).Distinct().OrderBy(v => v, StringComparer.Ordinal)
                      .Select(v => new Remembered(v, counts.Contains(v),
                                                  set.TryGetValue(v, out var s) ? s.ToList() : new List<string>(),
                                                  check.TryGetValue(v, out var c) ? c.ToList() : new List<string>()))
                      .ToList();
        }

        // the conditions a block holds: a branch's arms, a command's after, a note's options
        static IEnumerable<string> Conditions(Block block) => block switch
        {
            Branch branch => branch.Arms.Select(a => a.Condition),
            Do { After: { } after } => after.Arms.Select(a => a.Condition),
            Choice choice => choice.Options.Select(o => o.Condition),
            _ => Enumerable.Empty<string>(),
        };

        // --- declaring them ----------------------------------------------------------------------------------------

        // THE WORKSHOP'S OWN NODE (cc_task_p 4.4): the Yarn compiler wants every variable declared before it's read
        // ("used but not declared"; it infers nothing), so the editor writes one node it owns, never shown as a card, with
        // a <<declare>> for each thing the story remembers - false for a thing, 0 for a count. One a hand-written card
        // already declares is the author's, and isn't declared twice
        public const string File = Campaigns.Package.DialogueFolder + "/workshop_memory.yarn";

        public const string Node = "workshop_memory";

        public static string Declarations(StoryDraft story)
        {
            var own = story.Cards.Where(c => c.IsText).SelectMany(c => Regex.Matches(c.Text, @"<<declare\s+(\$\w+)").Select(m => m.Groups[1].Value))
                           .ToHashSet(StringComparer.Ordinal);

            List<Remembered> all = List(story).Where(r => !own.Contains(r.Variable)).ToList();

            if (all.Count == 0) return "";

            var lines = new List<string>
            {
                "title: " + Node,
                StoryWords.SpeakerHeader + ": " + StoryWords.Gm,
                "---",
                "// what the story remembers, declared for the Yarn compiler. The Workshop writes this node: edits to it are lost",
            };

            lines.AddRange(all.Select(r => $"<<declare {r.Variable} = {(r.Count ? "0" : "false")}>>"));
            lines.Add("===");

            return string.Join("\n", lines) + "\n";
        }

        // RENAMED: every block that sets or checks it, a Text card's Yarn too, and its phrase. A published campaign's
        // saves remember the old name: the page warns before this when the campaign has been shared (R)
        public static void Rename(StoryDraft story, IDictionary<string, string> phrases, string from, string phrase)
        {
            string to = Variable(phrase);
            var word = new Regex(Regex.Escape(from) + @"\b");

            foreach (CardDraft card in story.Cards)
            {
                if (card.IsText)
                {
                    card.Text = word.Replace(card.Text, to);
                    continue;
                }

                foreach (Block block in Blocks.All(card.Blocks))
                {
                    switch (block)
                    {
                        case Set s:
                            s.Variable = s.Variable == from ? to : s.Variable;
                            s.Value = word.Replace(s.Value, to);
                            break;
                        case Branch b:
                            foreach (Arm arm in b.Arms) arm.Condition = word.Replace(arm.Condition, to);
                            break;
                        case Do { After: { } after }:
                            foreach (Arm arm in after.Arms) arm.Condition = word.Replace(arm.Condition, to);
                            break;
                        case Choice c:
                            foreach (Option option in c.Options) option.Condition = word.Replace(option.Condition, to);
                            break;
                    }
                }
            }

            phrases.Remove(from);
            phrases[to] = phrase.Trim();
        }
    }
}
