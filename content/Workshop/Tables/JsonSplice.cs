using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Content.Workshop.Tables
{
    // A HAND-WRITTEN JSON FILE, CHANGED WHERE IT CHANGED AND NOWHERE ELSE (cc_task_n: "keep the files' existing shape, so
    // Kathleen's hand-written ones stay readable by her"). Three things go in: the file as written, what the editor
    // read out of it (`before`, the draft's own JSON), and what the editor holds now (`after`). A value `before` and
    // `after` agree on keeps the file's own text, spelling and layout and all - a "count": 2 the draft reads as "2", a key
    // the editor doesn't know, a one-line entry. Only what the author changed is written fresh, in the style of what's
    // around it: an object whose first key sat on its brace's line is written on one line, any other indented.
    // Objects in a list are matched by their "id", so one entry edited is one entry rewritten
    public static class JsonSplice
    {
        public static string Write(string original, JsonNode before, JsonNode after)
        {
            if (string.IsNullOrWhiteSpace(original)) return Fresh(after, "", Newline(original), inline: false) + "\n";

            string newline = Newline(original);
            var scan = new Scanner(original);
            Span root = scan.Value();

            string body = Value(original, root, before, after, newline);

            return original.Substring(0, root.Start) + body + original.Substring(root.End);
        }

        static string Newline(string text) => text != null && text.Contains("\r\n") ? "\r\n" : "\n";

        static string Value(string text, Span span, JsonNode before, JsonNode after, string newline)
        {
            if (JsonNode.DeepEquals(before, after)) return text.Substring(span.Start, span.End - span.Start);

            if (span.Kind == '{' && before is JsonObject b && after is JsonObject a) return Object(text, span, b, a, newline);

            if (span.Kind == '[' && before is JsonArray bl && after is JsonArray al && ById(bl) && ById(al) && ById(text, span))
                return List(text, span, bl, al, newline);

            return Fresh(after, Indent(text, span.Start), newline, Inline(text, span));
        }

        // --- an object: its keys in the file's order, new ones where the editor's order puts them -------------------------

        static string Object(string text, Span span, JsonObject before, JsonObject after, string newline)
        {
            var parts = new List<(string Name, string Text, int Was)>();
            var written = new HashSet<string>(StringComparer.Ordinal);
            string inner = span.Items.Count > 0 ? Indent(text, span.Items[0].NameStart) : Indent(text, span.Start) + "  ";
            bool inline = Inline(text, span);

            for (int at = 0; at < span.Items.Count; at++)
            {
                Span property = span.Items[at];
                string name = property.Name;
                written.Add(name);

                JsonNode was = before[name];
                JsonNode now = after[name];

                bool same = JsonNode.DeepEquals(was, now);

                if (same)
                {
                    parts.Add((name, text.Substring(property.NameStart, property.End - property.NameStart), at));
                    continue;
                }

                if (now == null && !after.ContainsKey(name)) continue;

                string head = text.Substring(property.NameStart, property.Start - property.NameStart);
                parts.Add((name, head + Value(text, property, was, now, newline), at));
            }

            // keys the file didn't have: after the nearest key before them in the editor's order
            List<string> order = after.Select(p => p.Key).ToList();

            foreach (string name in order.Where(n => !written.Contains(n)))
            {
                if (JsonNode.DeepEquals(before[name], after[name]) && before.ContainsKey(name)) continue;

                string fresh = Quote(name) + ": " + Fresh(after[name], inner, newline, inline);
                int at = order.IndexOf(name);
                string previous = order.Take(at).LastOrDefault(n => parts.Any(p => p.Name == n));
                int index = previous == null ? 0 : parts.FindIndex(p => p.Name == previous) + 1;

                parts.Insert(index, (name, fresh, -1));
            }

            return Join(text, span, parts.Select(p => (p.Text, p.Was)).ToList(), inner, newline, inline);
        }

        // --- a list of objects with ids: in the editor's order, each matched to the file's by id ----------------------------

        static bool ById(JsonArray list) => list.All(i => i is JsonObject o && o["id"] is JsonValue) &&
                                            list.Select(i => i["id"].ToString()).Distinct().Count() == list.Count;

        static bool ById(string text, Span span) => span.Items.All(i => i.Kind == '{' && i.Items.Any(p => p.Name == "id"));

        static string IdOf(string text, Span item)
        {
            Span id = item.Items.First(p => p.Name == "id");
            string raw = text.Substring(id.Start, id.End - id.Start);

            try
            {
                return JsonNode.Parse(raw)?.ToString() ?? "";
            }
            catch (JsonException)
            {
                return raw;
            }
        }

        static string List(string text, Span span, JsonArray before, JsonArray after, string newline)
        {
            var parts = new List<(string Text, int Was)>();
            string inner = span.Items.Count > 0 ? Indent(text, span.Items[0].Start) : Indent(text, span.Start) + "  ";
            bool inline = Inline(text, span);

            // a new entry takes the shape its neighbours have: one line if the file's first entry is one line
            bool entriesInline = span.Items.Count > 0 && Inline(text, span.Items[0]);

            foreach (JsonNode now in after)
            {
                string id = now["id"].ToString();
                int was = span.Items.FindIndex(i => IdOf(text, i) == id);
                JsonNode read = before.FirstOrDefault(b => b["id"].ToString() == id);

                parts.Add(was >= 0 && read != null
                    ? (Value(text, span.Items[was], read, now, newline), was)
                    : (Fresh(now, inner, newline, entriesInline || inline), -1));
            }

            return Join(text, span, parts, inner, newline, inline);
        }

        // the container's own brackets and the space just inside them kept, and what's in it: between two neighbours
        // that were neighbours in the file, the file's own separator (a blank line between two groups stays), else one
        // in the container's shape. `Was` is where a part was in the file, -1 for a new one
        static string Join(string text, Span span, List<(string Text, int Was)> parts, string inner, string newline, bool inline)
        {
            char open = span.Kind, close = open == '{' ? '}' : ']';

            if (parts.Count == 0) return $"{open}{close}";

            string between = inline ? ", " : "," + newline + inner;

            if (span.Items.Count == 0)
                return inline
                    ? $"{open} {string.Join(between, parts.Select(p => p.Text))} {close}"
                    : open + newline + inner + string.Join(between, parts.Select(p => p.Text)) + newline + Indent(text, span.Start) + close;

            int StartOf(Span item) => open == '{' ? item.NameStart : item.Start;

            string lead = text.Substring(span.Start + 1, StartOf(span.Items[0]) - span.Start - 1);
            string trail = text.Substring(span.Items[^1].End, span.End - 1 - span.Items[^1].End);

            var joined = new System.Text.StringBuilder(parts[0].Text);

            for (int i = 1; i < parts.Count; i++)
            {
                int was = parts[i - 1].Was;

                joined.Append(was >= 0 && parts[i].Was == was + 1
                    ? text.Substring(span.Items[was].End, StartOf(span.Items[was + 1]) - span.Items[was].End)
                    : between);
                joined.Append(parts[i].Text);
            }

            return open + lead + joined + trail + close;
        }

        // --- writing a value fresh --------------------------------------------------------------------------------------

        static readonly JsonSerializerOptions Indented = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public static string Quote(string words) => JsonSerializer.Serialize(words, Compact);

        static string Fresh(JsonNode node, string indent, string newline, bool inline)
        {
            if (node == null) return "null";

            if (inline) return OneLine(node);

            string written = node.ToJsonString(Indented).Replace("\r\n", "\n");

            return string.Join(newline + indent, written.Split('\n'));
        }

        // { "id": "x", "kind": "flavour" }, the shape of the game's own one-line entries
        static string OneLine(JsonNode node) => node switch
        {
            JsonObject o when o.Count == 0 => "{}",
            JsonObject o => "{ " + string.Join(", ", o.Select(p => Quote(p.Key) + ": " + OneLine(p.Value))) + " }",
            JsonArray a => "[" + string.Join(", ", a.Select(OneLine)) + "]",
            null => "null",
            _ => node.ToJsonString(Compact),
        };

        // the spaces before a position on its line
        static string Indent(string text, int at)
        {
            int start = text.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
            int end = start;

            while (end < text.Length && (text[end] == ' ' || text[end] == '\t')) end++;

            return text.Substring(start, end - start);
        }

        // an object or list written on its opening bracket's line
        static bool Inline(string text, Span span)
        {
            if (span.Kind != '{' && span.Kind != '[') return true;

            int firstAt = span.Items.Count == 0 ? span.End - 1 : span.Kind == '{' ? span.Items[0].NameStart : span.Items[0].Start;

            return text.IndexOf('\n', span.Start, firstAt - span.Start) < 0;
        }

        // --- where everything is in the file -------------------------------------------------------------------------

        // a value: where it starts and ends; for an object its properties (each with where its name starts), for a list
        // its items
        sealed class Span
        {
            public char Kind;
            public int Start, End, NameStart;
            public string Name;
            public readonly List<Span> Items = new();
        }

        // just enough of a JSON reader to know where each value is. the text was read by the real reader first, so it's
        // JSON; this only finds the places
        sealed class Scanner
        {
            readonly string _text;
            int _at;

            public Scanner(string text) => _text = text;

            // white space, and the comments the game's reader allows (Json.Options): a comment in a part the author
            // changes goes with it
            void Skip()
            {
                while (_at < _text.Length)
                {
                    if (char.IsWhiteSpace(_text[_at])) _at++;
                    else if (_text.AsSpan(_at).StartsWith("//")) _at = _text.IndexOf('\n', _at) is int end and >= 0 ? end : _text.Length;
                    else if (_text.AsSpan(_at).StartsWith("/*")) _at = _text.IndexOf("*/", _at + 2, StringComparison.Ordinal) is int close and >= 0 ? close + 2 : _text.Length;
                    else break;
                }
            }

            public Span Value()
            {
                Skip();
                var span = new Span { Start = _at, Kind = _at < _text.Length ? _text[_at] : ' ' };

                switch (span.Kind)
                {
                    case '{':
                        _at++;
                        Skip();
                        while (_at < _text.Length && _text[_at] != '}')
                        {
                            int nameStart = _at;
                            string name = JsonNode.Parse(_text.Substring(nameStart, String() - nameStart))?.ToString();
                            Skip();
                            _at++; // the colon
                            Span value = Value();
                            value.Name = name;
                            value.NameStart = nameStart;
                            span.Items.Add(value);
                            Skip();
                            if (_at < _text.Length && _text[_at] == ',') _at++;
                            Skip();
                        }
                        _at++;
                        break;

                    case '[':
                        _at++;
                        Skip();
                        while (_at < _text.Length && _text[_at] != ']')
                        {
                            span.Items.Add(Value());
                            Skip();
                            if (_at < _text.Length && _text[_at] == ',') _at++;
                            Skip();
                        }
                        _at++;
                        break;

                    case '"':
                        String();
                        break;

                    default:
                        while (_at < _text.Length && ",}] \t\r\n/".IndexOf(_text[_at]) < 0) _at++;
                        break;
                }

                span.End = _at;
                return span;
            }

            // past a string, escapes and all; where it ends
            int String()
            {
                _at++;
                while (_at < _text.Length && _text[_at] != '"') _at += _text[_at] == '\\' ? 2 : 1;
                return ++_at;
            }
        }
    }
}
