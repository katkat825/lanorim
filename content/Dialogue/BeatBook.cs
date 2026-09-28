using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Core.Words;

namespace Content.Dialogue
{
    // a campaign's beats/ folder. A file holds a LIST of beats rather than one apiece: the spine is
    // read and edited as a whole, and a hundred two-line files is a worse writing surface than one
    // hundred-line file, which is the thing W is meant to get right.
    public sealed class BeatBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Beat> _beats = new Dictionary<string, Beat>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        BeatBook(string campaign) => Campaign = campaign ?? "";

        public string Campaign { get; }

        public IReadOnlyCollection<string> Ids => _beats.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _beats.ContainsKey(id);

        public Beat Of(string id) => id != null && _beats.TryGetValue(id, out Beat beat) ? beat : null;

        public IEnumerable<Beat> All =>
            _beats.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _beats[i]);

        public int Count => _beats.Count;

        // Every phrasing the spine CAN use: the DM's floor, and one per beat per voice.
        //
        // None of these is demanded of a campaign - a campaign cannot write for a companion that
        // was published after it. What is demanded is that each beat is deliverable, which is
        // Reaches() below and which the dialogue check enforces. This list exists so the locale
        // audit knows a beat key in a CSV is a real key and not an orphan.
        public IEnumerable<string> KeysFor(IEnumerable<string> speakers)
        {
            foreach (Beat beat in All)
            {
                yield return beat.DmKey(Campaign);

                foreach (string speaker in speakers ?? Array.Empty<string>())
                    yield return beat.KeyFor(speaker, Campaign);
            }
        }

        // Is this beat deliverable to EVERY player? The DM can always say it, so a DM phrasing
        // settles it on its own; otherwise every voice on the shelf needs its own.
        public bool Reaches(Beat beat, IEnumerable<string> voices, Func<string, bool> written) =>
            beat != null && written != null
            && (written(beat.DmKey(Campaign))
                || (voices ?? Array.Empty<string>()).All(v => written(beat.KeyFor(v, Campaign))));


        public static BeatBook Read(string folder, string campaign)
        {
            var book = new BeatBook(campaign);

            ListFile.ReadFolder(folder, Extension, "beats",
                                "a beats file is a list of intents - " +
                                "{ \"beats\": [ { \"id\": \"warn_bridge_trapped\", \"kind\": \"plot\" } ] }",
                                "a beat", Fields, book._problems, book.One);

            return book;
        }

        static readonly string[] Fields = { "id", "kind", "note" };

        void One(JsonElement entry, string file, string where)
        {
            if (!entry.TryGetProperty("id", out JsonElement id) ||
                id.ValueKind != JsonValueKind.String || !ContentId.IsLocal(id.GetString()))
            {
                _problems.Add(new ContentProblem(
                    file, $"{where}.id",
                    "a beat needs an id - lowercase a-z, 0-9 and underscore. Every companion is " +
                    "keyed off it, so it is the name of the intent and not of a line"));
                return;
            }

            BeatKind kind = BeatKind.Plot;

            if (entry.TryGetProperty("kind", out JsonElement word))
            {
                if (word.ValueKind != JsonValueKind.String || !EnumWords.TryParse(word.GetString(), out kind))
                {
                    _problems.Add(new ContentProblem(
                        file, $"{where}.kind",
                        $"'{PackJson.Shown(word)}' is not a kind of beat - it is one of " +
                        $"{Vocabulary.Offer(EnumWords.Ids<BeatKind>())}"));
                    return;
                }
            }

            string note = entry.TryGetProperty("note", out JsonElement said) &&
                          said.ValueKind == JsonValueKind.String
                ? said.GetString()
                : "";

            if (_beats.ContainsKey(id.GetString()))
            {
                _problems.Add(new ContentProblem(
                    file, $"{where}.id",
                    $"'{id.GetString()}' is already a beat in this campaign - one intent, written " +
                    "once, is the whole idea"));
                return;
            }

            _beats[id.GetString()] = new Beat(id.GetString(), kind, note);
        }

        public override string ToString() =>
            $"{_beats.Count} beats" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
