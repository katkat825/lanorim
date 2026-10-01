using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;

namespace Content.Dialogue
{
    // a campaign's hints/ folder: one problem, three rungs, each rung naming a BEAT rather than a
    // line. That indirection is the point - a hint phrased by only one companion is a hint gated on
    // which class you picked, and the spine check catches that the moment a voice is missing one.
    public sealed class HintBook : IdBook<Hint>
    {
        public const string Extension = ".json";

        HintBook(string campaign) : base(campaign, "hints")
        {
        }

        public static HintBook Read(string folder, string campaign)
        {
            var book = new HintBook(campaign);

            ListFile.ReadFolder(folder, Extension, "hints",
                                "a hints file is a list of problems - { \"hints\": [ { \"id\": " +
                                "\"the_stair_door\", \"rungs\": [ \"hinges_are_new\", " +
                                "\"somebody_replaced_it\", \"the_pins_lift_out\" ] } ] }",
                                "a hint", Fields, book.Said, book.One);

            return book;
        }

        static readonly string[] Fields = { "id", "rungs" };

        void One(JsonElement entry, string file, string where)
        {
            string id = IdOf(entry, file, where,
                             "a hint needs the problem it is about - lowercase a-z, 0-9 and underscore. " +
                             "The ladder counts asks per problem, so this is what it counts");

            if (id == null) return;

            if (!entry.TryGetProperty("rungs", out JsonElement rungs) ||
                rungs.ValueKind != JsonValueKind.Array)
            {
                Said.Add(new ContentProblem(
                    file, $"{where}.rungs",
                    $"a hint needs its {HintLadder.Rungs} rungs, as beat ids - an observation, a " +
                    "nudge, then something close to the answer"));
                return;
            }

            var named = new List<string>();
            int at = 0;

            foreach (JsonElement rung in rungs.EnumerateArray())
            {
                if (rung.ValueKind != JsonValueKind.String || !ContentId.IsLocal(rung.GetString()))
                {
                    Said.Add(new ContentProblem(
                        file, $"{where}.rungs[{at}]",
                        $"'{PackJson.Shown(rung)}' is not a beat id - a rung names a beat in this " +
                        "campaign's beats/ folder, so every companion has to phrase it"));
                    return;
                }

                named.Add(rung.GetString());
                at++;
            }

            if (named.Count != HintLadder.Rungs)
            {
                Said.Add(new ContentProblem(
                    file, $"{where}.rungs",
                    $"this hint has {named.Count} rungs and a ladder has {HintLadder.Rungs} - an " +
                    "observation, a nudge, then something close to the answer. Fewer and asking " +
                    "twice repeats itself; more and the third ask is not the last one"));
                return;
            }

            if (Has(id))
            {
                Said.Add(new ContentProblem(
                    file, $"{where}.id",
                    $"'{id}' already has a ladder in this campaign"));
                return;
            }

            Add(id, new Hint(id, named));
        }
    }

    public sealed class Hint
    {
        public Hint(string id, IReadOnlyList<string> rungs)
        {
            Id = id;
            Rungs = rungs ?? Array.Empty<string>();
        }

        // the problem asked about, which is what HintLadder counts
        public string Id { get; }

        // beat ids, in order: observation, nudge, answer
        public IReadOnlyList<string> Rungs { get; }

        // the beat for a rung, 1-based as the ladder counts them; null past the top
        public string Beat(int rung) =>
            rung >= 1 && rung <= Rungs.Count ? Rungs[rung - 1] : null;

        public override string ToString() => $"{Id}: {string.Join(" -> ", Rungs)}";
    }
}
