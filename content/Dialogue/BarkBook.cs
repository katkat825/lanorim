using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;

namespace Content.Dialogue
{
    // a pack's barks/ folder: one file per speaker, the file name irrelevant, the 'speaker' field
    // the identity. Speakers are NOT pack-scoped - the wolf ships in the base game and belongs to
    // no campaign (CONVENTIONS section 7), so a campaign writing more of the wolf's barks adds to
    // the same bank rather than starting a second one nobody would hear.
    public sealed class BarkBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, BarkBank> _banks =
            new Dictionary<string, BarkBank>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        public IReadOnlyCollection<string> Speakers => _banks.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string speaker) => speaker != null && _banks.ContainsKey(speaker);

        public BarkBank Of(string speaker) =>
            speaker != null && _banks.TryGetValue(speaker, out BarkBank bank) ? bank : null;

        public IEnumerable<BarkBank> All =>
            _banks.Keys.OrderBy(s => s, StringComparer.Ordinal).Select(s => _banks[s]);

        public int Lines => _banks.Values.Sum(b => b.Lines);

        public IEnumerable<string> Keys() => All.SelectMany(bank => bank.Keys());

        public bool TakesAnArgument(string key) => All.Any(bank => bank.TakesAnArgument(key));


        public static BarkBook Read(string folder)
        {
            var book = new BarkBook();

            foreach ((string name, string _, string text) in PackFolder.Read(folder, Extension, null, book._problems))
            {
                Read<BarkBank> read = BarkReader.Parse(text, name);

                if (!read.Ok)
                {
                    book._problems.AddRange(read.Problems);
                    continue;
                }

                book.Add(read.Value, name);
            }

            return book;
        }

        void Add(BarkBank bank, string file)
        {
            if (_banks.ContainsKey(bank.Speaker))
            {
                _problems.Add(new ContentProblem(
                    file, "speaker",
                    $"'{bank.Speaker}' already has a bark bank in this folder - one file per " +
                    "voice, or two of them silently disagree about how many lines there are"));
                return;
            }

            _banks[bank.Speaker] = bank;
        }

        public override string ToString() =>
            $"{_banks.Count} voices, {Lines} barks" +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
