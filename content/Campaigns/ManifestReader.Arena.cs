using System.Collections.Generic;
using System.Text.Json;
using Content.Challengers;
using Content.Schema;

namespace Content.Campaigns
{
    public static partial class ManifestReader
    {
        // THE ARENA'S NUMBERS (cc_task_cg Parts 2 and 4), pack.json's "challengers": each one optional, each left out
        // the game's (ArenaRules.Default)
        //   "challengers": { "win": 5, "loss": -2,
        //                    "roster": { "least": 2, "every": 10, "most": 6 },
        //                    "bout_xp": { "share": 0.25, "low": 0.75, "moderate": 1, "high": 1.5 } }
        public const string ArenaField = "challengers";

        public static readonly IReadOnlyList<string> ArenaKeys = new[] { "win", "loss", "roster", "bout_xp" };

        public static readonly IReadOnlyList<string> RosterKeys = new[] { "least", "every", "most" };

        public static readonly IReadOnlyList<string> BoutXpKeys = new[] { "share", "low", "moderate", "high" };

        static ArenaRules Arena(JsonElement root, string file, List<ContentProblem> problems)
        {
            ArenaRules game = ArenaRules.Default;

            if (!root.TryGetProperty(ArenaField, out JsonElement arena)) return game;

            if (arena.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(file, ArenaField, "'challengers' is an object of the arena's numbers"));
                return game;
            }

            var trouble = new List<string>();
            Keyed.OnlyKnown(arena, ArenaKeys, ArenaField, trouble);

            JsonElement roster = Part(arena, "roster", RosterKeys, trouble);
            JsonElement xp = Part(arena, "bout_xp", BoutXpKeys, trouble);

            var rules = new ArenaRules
            {
                Win = Whole(arena, "win", game.Win, 0, trouble),
                Loss = Whole(arena, "loss", game.Loss, int.MinValue, trouble, most: 0),
                Least = Whole(roster, "least", game.Least, 1, trouble),
                Every = Whole(roster, "every", game.Every, 1, trouble),
                Most = Whole(roster, "most", game.Most, 1, trouble),
                Share = Real(xp, "share", game.Share, trouble),
                Low = Real(xp, "low", game.Low, trouble),
                Moderate = Real(xp, "moderate", game.Moderate, trouble),
                High = Real(xp, "high", game.High, trouble),
            };

            if (rules.Most < rules.Least)
                trouble.Add($"the roster's 'most' ({rules.Most}) is under its 'least' ({rules.Least})");

            foreach (string one in trouble) problems.Add(new ContentProblem(file, ArenaField, one));

            return rules;
        }

        static JsonElement Part(JsonElement arena, string name, IReadOnlyList<string> keys, List<string> trouble)
        {
            if (!arena.TryGetProperty(name, out JsonElement part)) return default;

            if (part.ValueKind != JsonValueKind.Object)
            {
                trouble.Add($"'{name}' is an object");
                return default;
            }

            Keyed.OnlyKnown(part, keys, name, trouble);
            return part;
        }

        static int Whole(JsonElement part, string name, int otherwise, int least, List<string> trouble, int most = int.MaxValue)
        {
            if (part.ValueKind != JsonValueKind.Object || !part.TryGetProperty(name, out JsonElement value)) return otherwise;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int read) && read >= least && read <= most)
                return read;

            trouble.Add($"'{name}' is '{PackJson.Shown(value)}', and it is a whole number" +
                        (most == 0 ? ", 0 or less" : least > int.MinValue ? $", {least} or more" : ""));
            return otherwise;
        }

        static double Real(JsonElement part, string name, double otherwise, List<string> trouble)
        {
            if (part.ValueKind != JsonValueKind.Object || !part.TryGetProperty(name, out JsonElement value)) return otherwise;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double read) && read >= 0) return read;

            trouble.Add($"'{name}' is '{PackJson.Shown(value)}', and it is a number, 0 or more");
            return otherwise;
        }
    }
}
