using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Content.Schema;

namespace Content.Dialogue
{
    // THE STORY'S OWN VERBS, CHECKED AS THE STORY IS READ (cc_task_nt Part 4): a verb written wrong (<<tip nowhere>>, a
    // <<bark>> pool there isn't) is a campaign check error that names the file, the node and the line, where before it
    // was only a complaint in a playthrough. A command with a {$variable} in it is the story's to fill at play time, so it
    // is left to the playthrough. Requests() hands every well-formed one on, for a check that needs the pack (a map id)
    public static class VerbCheck
    {
        static readonly Regex Command = new(@"<<\s*([a-z_]+)([^>]*)>>", RegexOptions.Compiled);

        public sealed record Found(string File, string Node, int Line, Request Request);

        public static IEnumerable<ContentProblem> Problems(string file, string source) =>
            Each(file, source, (node, line, _, problem) => problem == null ? null : new ContentProblem(file, node, problem, line));

        public static IEnumerable<Found> Requests(string file, string source) =>
            Each(file, source, (node, line, request, _) => request == null ? null : new Found(file, node, line, request));

        static IEnumerable<T> Each<T>(string file, string source, Func<string, int, Request, string, T> made) where T : class
        {
            string node = "";
            string[] lines = (source ?? "").Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("title:", StringComparison.Ordinal)) node = lines[i].Substring(6).Trim();

                foreach (Match command in Command.Matches(lines[i]))
                {
                    string text = (command.Groups[1].Value + " " + command.Groups[2].Value.Trim()).Trim();

                    if (!Request.Verbs.ContainsKey(command.Groups[1].Value) || text.Contains('{')) continue;

                    Request request = Request.Parse(text, out string problem);

                    if (made(node, i + 1, request, problem) is T one) yield return one;
                }
            }
        }
    }
}
