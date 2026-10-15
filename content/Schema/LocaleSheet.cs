using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Content.Schema
{
    // A CAMPAIGN'S LOCALE CSV, OPENED TO BE EDITED (cc_task_m: the Workshop writes a campaign's name, its description,
    // its chapters' titles and its characters' names). Locale reads one language and writes a fresh file; this keeps
    // the whole sheet - every row, every language column, in the file's order - and changes only the English of the
    // rows it's told to, so a campaign written by hand loses nothing to the Workshop. A new row goes in key order
    // (the hand-written packs are sorted); one sheet a file
    public sealed class LocaleSheet
    {
        readonly List<string> _header;
        readonly List<List<string>> _rows;

        LocaleSheet(List<string> header, List<List<string>> rows)
        {
            _header = header;
            _rows = rows;
        }

        public static LocaleSheet Empty() => new(new List<string> { Locale.KeyColumn, Locale.English }, new List<List<string>>());

        // a file's text; one without a "keys" column and an English one is started again with them
        public static LocaleSheet Of(string csv)
        {
            List<string[]> rows = Locale.Rows(csv ?? "");

            if (rows.Count == 0 || rows[0].Length == 0 || rows[0][0].Trim() != Locale.KeyColumn) return Empty();

            var header = rows[0].Select(h => h.Trim()).ToList();

            if (!header.Contains(Locale.English, StringComparer.OrdinalIgnoreCase)) header.Add(Locale.English);

            return new LocaleSheet(header,
                rows.Skip(1).Where(r => r.Length > 0 && !string.IsNullOrWhiteSpace(r[0]))
                    .Select(r => r.Select((cell, i) => i == 0 ? cell.Trim() : cell).ToList()).ToList());
        }

        int English => _header.FindIndex(h => string.Equals(h, Locale.English, StringComparison.OrdinalIgnoreCase));

        List<string> Row(string key) => _rows.FirstOrDefault(r => r[0] == key);

        public IEnumerable<string> Keys => _rows.Select(r => r[0]);

        public bool Has(string key) => Row(key) != null;

        // the English of a key, or "" when it has none
        public string Get(string key)
        {
            List<string> row = Row(key);
            int column = English;

            return row != null && column < row.Count ? row[column] : "";
        }

        // a key's English set, the row added in key order when there's none
        public void Set(string key, string english)
        {
            List<string> row = Row(key);

            if (row == null)
            {
                row = new List<string> { key };
                int at = _rows.FindIndex(r => string.CompareOrdinal(r[0], key) > 0);
                _rows.Insert(at < 0 ? _rows.Count : at, row);
            }

            while (row.Count < _header.Count) row.Add("");

            row[English] = english ?? "";
        }

        public bool Remove(string key) => _rows.RemoveAll(r => r[0] == key) > 0;

        // every key spelled anew (a campaign given another id): its other cells kept
        public void RenameKeys(Func<string, string> rename)
        {
            foreach (List<string> row in _rows) row[0] = rename(row[0]);
        }

        public string Text()
        {
            var csv = new StringBuilder();

            csv.Append(string.Join(",", _header.Select(Locale.Quote))).Append('\n');

            foreach (List<string> row in _rows)
            {
                // a short row is a hand-written file's own; it's written back as short as it was
                int last = row.Count - 1;
                while (last > 1 && row[last].Length == 0) last--;

                csv.Append(string.Join(",", row.Take(last + 1).Select(Locale.Quote))).Append('\n');
            }

            return csv.ToString();
        }
    }
}
