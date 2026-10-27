using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Workshop.Story
{
    public enum Dot
    {
        None,

        // a hint (no way out, nothing leads here, a caution)
        Amber,

        // something the campaign check refuses
        Red,
    }

    // WHAT'S WRONG WITH A CARD (cc_task_o Part 3.5 and Part 5.2): the campaign check's own problems (the Yarn compiler's,
    // TwoKeys' warnings), each put on the card - and, when the line it names is one, the block - it's about; and two
    // hints the editor gives of its own: a card that leads nowhere, and one nothing leads to. Hints are amber and never
    // stop a save
    public static class StoryProblems
    {
        public sealed record OnCard(string Card, int Block, Dot Dot, ContentProblem Problem, string HintKey);

        public static IReadOnlyList<OnCard> Of(StoryDraft story, IEnumerable<ContentProblem> problems, string folder,
                                               IEnumerable<string> chapters)
        {
            var found = new List<OnCard>();
            var lines = Lines(folder);

            foreach (ContentProblem problem in problems.Where(p => p.File.StartsWith(Package.DialogueFolder, StringComparison.Ordinal)))
            {
                // TwoKeys names its node; the compiler names a line in a file
                CardDraft card = story.Card(problem.Where) ??
                                 lines.Where(l => l.File == problem.File && problem.Line >= l.From && problem.Line <= l.To)
                                      .Select(l => story.Card(l.Title)).FirstOrDefault();

                if (card == null) continue;

                int block = card.IsText ? -1 : BlockAt(card, problem.Line - (lines.FirstOrDefault(l => l.Title == card.Title).From + card.Headers.Count + 1));

                found.Add(new OnCard(card.Title, block, problem.IsACaution ? Dot.Amber : Dot.Red, problem, null));
            }

            HashSet<string> starts = chapters.ToHashSet(StringComparer.Ordinal);
            var led = story.Cards.SelectMany(StoryDraft.Leads).Select(l => l.To).ToHashSet(StringComparer.Ordinal);

            foreach (CardDraft card in story.Cards.Where(c => !c.IsText))
            {
                if (!Blocks.All(card.Blocks).Any(b => b is Jump or Stop) && !IsCamp(card))
                    found.Add(new OnCard(card.Title, -1, Dot.Amber, null, StoryWords.NoWayOutKey));

                if (!led.Contains(card.Title) && !starts.Contains(card.Title) && !IsCamp(card))
                    found.Add(new OnCard(card.Title, -1, Dot.Amber, null, StoryWords.NothingLeadsHereKey));
            }

            return found;
        }

        // a camp scene is offered on a night, not led to (DialogueBook's "topic:")
        static bool IsCamp(CardDraft card) => YarnNode.Header(card.Headers, Dialogue.DialogueBook.TopicHeader).Length > 0;

        public static Dot DotOf(IEnumerable<OnCard> problems, string card) =>
            problems.Where(p => p.Card == card).Select(p => p.Dot).DefaultIfEmpty(Dot.None).Max();

        // every node's lines in its file, 1-based, as the files are on disk (the compiler's line numbers are theirs)
        static List<(string File, string Title, int From, int To)> Lines(string folder)
        {
            var found = new List<(string, string, int, int)>();
            string dialogue = Path.Combine(folder ?? "", Package.DialogueFolder);

            if (!Directory.Exists(dialogue)) return found;

            foreach (string path in Directory.EnumerateFiles(dialogue, "*" + Dialogue.DialogueBook.Extension))
            {
                string file = Package.DialogueFolder + "/" + Path.GetFileName(path);

                foreach (YarnNode node in YarnText.Of(File.ReadAllText(path)).Nodes)
                    found.Add((file, node.Title, node.Line, node.Line + node.Headers.Count + node.Body.Count + 1));
            }

            return found;
        }

        // the top-level block a body line (0-based) was written by
        static int BlockAt(CardDraft card, int line)
        {
            int at = 0;

            for (int i = 0; i < card.Blocks.Count; i++)
            {
                at += StoryText.Write(new[] { card.Blocks[i] }).Count;
                if (line < at) return i;
            }

            return -1;
        }
    }
}
