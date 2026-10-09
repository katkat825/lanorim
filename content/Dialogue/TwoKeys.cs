using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Schema;
using Core.Characters;
using Core.Words;

namespace Content.Dialogue
{
    // TWO KEYS (cc_task_h 3.5; docs/campaign_authoring_rules.md): a solo hero has one set of skills, so a check the
    // story can't get past on a fail is a door the wrong class can never open. The campaign check WARNS (a caution,
    // never a refusal) where it can tell a check blocks: its `<<if $passed>>` block is all the node has left, the pass
    // branch jumps on, and the fail branch neither jumps nor carries on - or it `<<stop>>`s. It stays quiet when the
    // same node offers another check on a different ability, or a choice (`->` lines) - another way through.
    //
    // It reads the .yarn text, one node at a time, and only that shape. A dead end built any other way (a fail that
    // jumps to a node that ends the story, a variable set on the fail and read three nodes later) it can't tell, and
    // it says nothing rather than guess.
    public static class TwoKeys
    {
        static readonly Regex Check = new(@"^\s*<<check\s+(\w+)", RegexOptions.Compiled);
        static readonly Regex IfPassed = new(@"^\s*<<if\s+\$passed\s*>>", RegexOptions.Compiled);
        static readonly Regex If = new(@"^\s*<<if\b", RegexOptions.Compiled);
        static readonly Regex Else = new(@"^\s*<<else(if)?\b", RegexOptions.Compiled);
        static readonly Regex EndIf = new(@"^\s*<<endif>>", RegexOptions.Compiled);
        static readonly Regex Onward = new(@"<<(jump|stop)\b", RegexOptions.Compiled);
        static readonly Regex Stop = new(@"<<stop>>", RegexOptions.Compiled);

        public static IEnumerable<ContentProblem> Warnings(string file, string source)
        {
            foreach ((string title, List<string> body) in Nodes(source))
            {
                bool choices = body.Any(l => l.TrimStart().StartsWith("->", StringComparison.Ordinal));
                var abilities = body.Select(l => Check.Match(l))
                                    .Where(m => m.Success && EnumWords.TryParse(m.Groups[1].Value, out Skill _))
                                    .Select(m => { EnumWords.TryParse(m.Groups[1].Value, out Skill s); return s.Governs(); })
                                    .Distinct()
                                    .Count();

                for (int i = 0; i < body.Count; i++)
                {
                    Match check = Check.Match(body[i]);

                    if (!check.Success || !Blocks(body, i)) continue;

                    if (choices || abilities > 1) continue;

                    yield return ContentProblem.Caution(file, title,
                        $"the {check.Groups[1].Value} check in '{title}' has no way past a fail: offer another skill on a " +
                        "different ability, or a way through without a check (two keys)");
                }
            }
        }

        // the check at `at`, followed by an if $passed block: does a fail stop the story there?
        static bool Blocks(List<string> body, int at)
        {
            int open = body.FindIndex(at + 1, l => l.Trim().Length > 0);

            if (open < 0 || !IfPassed.IsMatch(body[open])) return false;

            int depth = 0, elseAt = -1, end = -1;

            for (int i = open; i < body.Count && end < 0; i++)
            {
                if (If.IsMatch(body[i])) depth++;
                else if (EndIf.IsMatch(body[i]) && --depth == 0) end = i;
                else if (depth == 1 && elseAt < 0 && Else.IsMatch(body[i])) elseAt = i;
            }

            if (end < 0) return false;

            IEnumerable<string> passed = body.Skip(open + 1).Take((elseAt < 0 ? end : elseAt) - open - 1);
            IEnumerable<string> failed = elseAt < 0 ? Enumerable.Empty<string>() : body.Skip(elseAt + 1).Take(end - elseAt - 1);
            IEnumerable<string> after = body.Skip(end + 1).Where(l => l.Trim().Length > 0);

            if (failed.Any(l => Stop.IsMatch(l))) return true;

            // the pass goes on, the fail doesn't, and the node has nothing after the block to carry a fail on
            return passed.Any(l => Onward.IsMatch(l)) && !failed.Any(l => Onward.IsMatch(l)) && !after.Any();
        }

        static IEnumerable<(string Title, List<string> Body)> Nodes(string source)
        {
            string title = "";
            List<string> body = null;

            foreach (string line in (source ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (line.StartsWith("title:", StringComparison.Ordinal)) title = line.Substring(6).Trim();
                else if (line.Trim() == "---") body = new List<string>();
                else if (line.Trim() == "===")
                {
                    if (body != null) yield return (title, body);
                    body = null;
                }
                else body?.Add(line);
            }
        }
    }
}
