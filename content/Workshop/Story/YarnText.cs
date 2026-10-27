using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Content.Workshop.Story
{
    // A .YARN FILE IN PIECES, BYTE FOR BYTE (cc_task_o Part 3): its nodes, each from its first header line to its "==="
    // line, and whatever lies between them (blank lines, a comment), so the pieces joined are the file. Saving puts a
    // changed node's new text where the old one was and leaves every other piece as it is
    public sealed class YarnText
    {
        public sealed record Piece(string Text, YarnNode Node)
        {
            public bool IsNode => Node != null;
        }

        public IReadOnlyList<Piece> Pieces { get; }

        public string Newline { get; }

        YarnText(List<Piece> pieces, string newline)
        {
            Pieces = pieces;
            Newline = newline;
        }

        public IEnumerable<YarnNode> Nodes => Pieces.Where(p => p.IsNode).Select(p => p.Node);

        public string Text => string.Concat(Pieces.Select(p => p.Text));

        public static YarnText Of(string text)
        {
            text ??= "";
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            var pieces = new List<Piece>();
            var between = new StringBuilder();
            List<string> node = null;
            int line = 0, startLine = 0;

            foreach (string raw in Lines(text))
            {
                line++;
                string bare = raw.TrimEnd('\r', '\n');

                if (node == null)
                {
                    // a node starts at its first header ("title: x"); anything else between nodes is kept as it is
                    if (bare.Trim().Length > 0 && !bare.TrimStart().StartsWith("//") && bare.Contains(':'))
                    {
                        if (between.Length > 0) pieces.Add(new Piece(between.ToString(), null));
                        between.Clear();
                        node = new List<string> { raw };
                        startLine = line;
                    }
                    else between.Append(raw);

                    continue;
                }

                node.Add(raw);

                if (bare.Trim() == "===")
                {
                    pieces.Add(new Piece(string.Concat(node), YarnNode.Of(node, startLine, newline)));
                    node = null;
                }
            }

            // a node with no "===" (the file ends inside it) is kept as text: the compiler will say so
            if (node != null) between.Append(string.Concat(node));
            if (between.Length > 0) pieces.Add(new Piece(between.ToString(), null));

            return new YarnText(pieces, newline);
        }

        // every line with its own line break on it, so they join back into the text
        public static IEnumerable<string> Lines(string text)
        {
            int at = 0;

            while (at < text.Length)
            {
                int end = text.IndexOf('\n', at);
                end = end < 0 ? text.Length : end + 1;
                yield return text.Substring(at, end - at);
                at = end;
            }
        }
    }

    // ONE NODE: its header lines as written, and its body - the lines between "---" and "===" - as written
    public sealed class YarnNode
    {
        public IReadOnlyList<string> Headers { get; init; }

        public IReadOnlyList<string> Body { get; init; }

        // where its first line is in the file, 1-based; and where its body starts
        public int Line { get; init; }

        public int BodyLine => Line + Headers.Count + 1;

        // the whole node, as written
        public string Raw { get; init; }

        // the "---" line was there (a node without one is the compiler's to complain about)
        public bool Sound { get; init; }

        public string Title => Header(Headers, "title");

        public static string Header(IEnumerable<string> headers, string key) =>
            headers.Select(h => h.TrimEnd('\r', '\n'))
                   .Where(h => h.StartsWith(key + ":", StringComparison.Ordinal))
                   .Select(h => h.Substring(key.Length + 1).Trim())
                   .FirstOrDefault() ?? "";

        public static YarnNode Of(List<string> lines, int line, string newline)
        {
            int dashes = lines.FindIndex(l => l.TrimEnd('\r', '\n').Trim() == "---");

            return new YarnNode
            {
                Headers = (dashes < 0 ? lines.Take(lines.Count - 1) : lines.Take(dashes)).Select(l => l.TrimEnd('\r', '\n')).ToList(),
                Body = dashes < 0 ? new List<string>() : lines.Skip(dashes + 1).Take(lines.Count - dashes - 2).Select(l => l.TrimEnd('\r', '\n')).ToList(),
                Line = line,
                Raw = string.Concat(lines),
                Sound = dashes >= 0 && lines[dashes].TrimEnd('\r', '\n') == "---" && lines[^1].TrimEnd('\r', '\n') == "===",
            };
        }

        public override string ToString() => $"{Title}: {Body.Count} lines";
    }
}
