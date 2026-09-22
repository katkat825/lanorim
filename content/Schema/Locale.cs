using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Localization;

namespace Content.Schema
{
    // reads and writes the Godot translation CSV: first column "keys", one column per language.
    // English is a locale file like any other language - core emits keys and never text, and this
    // is the only place that knows what a key says.
    public static class Locale
    {
        public const string KeyColumn = "keys";

        public const string English = "en";

        public static IReadOnlyDictionary<string, string> Read(string csv, string language = English)
        {
            var strings = new Dictionary<string, string>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(csv)) return strings;

            List<string[]> rows = Rows(csv);

            if (rows.Count == 0) return strings;

            string[] header = rows[0];

            int column = Array.FindIndex(header,
                h => string.Equals(h.Trim(), language, StringComparison.OrdinalIgnoreCase));

            if (column < 0) return strings;

            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];

                if (row.Length == 0 || string.IsNullOrWhiteSpace(row[0])) continue;

                strings[row[0].Trim()] = column < row.Length ? row[column] : "";
            }

            return strings;
        }

        public static ILocalizer Localizer(string csv, string language = English) =>
            new DictionaryLocalizer(Read(csv, language));

        public static string Write(IEnumerable<(string Key, string Text)> lines,
                                   string language = English)
        {
            var csv = new StringBuilder();

            csv.Append(KeyColumn).Append(',').Append(language).Append('\n');

            foreach ((string key, string text) in lines)
                csv.Append(Quote(key)).Append(',').Append(Quote(text)).Append('\n');

            return csv.ToString();
        }

        // RFC 4180 enough for Godot: quote when the value holds a comma, a quote or a newline,
        // and double an inner quote
        static string Quote(string value)
        {
            value ??= "";

            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        static List<string[]> Rows(string csv)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new StringBuilder();

            bool quoted = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];

                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;

                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;

                    case '\r':
                        break;

                    case '\n':
                        row.Add(field.ToString());
                        field.Clear();
                        rows.Add(row.ToArray());
                        row.Clear();
                        break;

                    default:
                        field.Append(c);
                        break;
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }

            return rows;
        }

        // the audit check-locale used to run, moved inside dotnet test so it cannot be forgotten:
        // every key the engine emits has English, and no key is malformed
        public static IReadOnlyList<string> Audit(string csv, IEnumerable<string> keys,
                                                  string language = English)
        {
            var problems = new List<string>();

            IReadOnlyDictionary<string, string> strings = Read(csv, language);

            foreach (string key in keys.Distinct().OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!KeyConventions.IsWellFormed(key))
                {
                    problems.Add(KeyConventions.Explain(key));
                    continue;
                }

                if (!strings.TryGetValue(key, out string text))
                {
                    problems.Add($"'{key}' has no {language}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(text)) problems.Add($"'{key}' is blank in {language}");
            }

            return problems;
        }

        // the other direction: text nobody asks for any more
        public static IReadOnlyList<string> Orphans(string csv, IEnumerable<string> keys,
                                                    string language = English)
        {
            var wanted = new HashSet<string>(keys, StringComparer.Ordinal);

            return Read(csv, language).Keys.Where(k => !wanted.Contains(k))
                                           .OrderBy(k => k, StringComparer.Ordinal)
                                           .ToList();
        }
    }
}
