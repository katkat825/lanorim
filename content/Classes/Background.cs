using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Localization;

namespace Content.Classes
{
    // SRD 5.2.1 moved the ability increases onto the background, which is why the seven species
    // carry none. light, as decisions_checklist.md section 2 asks: two skills, a piece of gear,
    // and the +2/+1 (or +1/+1/+1) to spend.
    public sealed class Background
    {
        public Background(string id, IReadOnlyList<Skill> skills = null,
                          IReadOnlyList<Ability> abilities = null,
                          IReadOnlyList<string> gear = null, int gold = 0)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Skills = skills ?? Array.Empty<Skill>();
            Abilities = abilities ?? Array.Empty<Ability>();
            Gear = gear ?? Array.Empty<string>();
            Gold = Math.Max(0, gold);
        }

        public string Id { get; }

        public IReadOnlyList<Skill> Skills { get; }

        // the three the +2/+1 or +1/+1/+1 may be spent on
        public IReadOnlyList<Ability> Abilities { get; }

        public IReadOnlyList<string> Gear { get; }

        public int Gold { get; }

        public string NameKey => KeyConventions.BackgroundName(Id);

        public string DescriptionKey =>
            KeyConventions.Key(KeyConventions.BackgroundNs, Id, "description");

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;
        }

        // SRD: +2 to one and +1 to another of the three, or +1 to all three. the choice is the
        // player's, so this only checks it is legal
        public bool IsLegalSpend(IReadOnlyDictionary<Ability, int> spend, out string problem)
        {
            problem = null;

            if (spend == null || spend.Count == 0)
            {
                problem = "nothing spent - a background gives +2 and +1, or +1 to all three";
                return false;
            }

            foreach (Ability ability in spend.Keys)
                if (!Abilities.Contains(ability))
                {
                    problem = $"{Id} does not raise {ability.Id()}";
                    return false;
                }

            int total = spend.Values.Sum();

            if (total != 3)
            {
                problem = $"{total} points spent; a background gives three";
                return false;
            }

            var sorted = spend.Values.OrderByDescending(v => v).ToList();

            bool twoAndOne = sorted.Count == 2 && sorted[0] == 2 && sorted[1] == 1;
            bool oneEach = sorted.Count == 3 && sorted.All(v => v == 1);

            if (!twoAndOne && !oneEach)
            {
                problem = "a background gives +2 and +1, or +1 to all three";
                return false;
            }

            return true;
        }

        public void Outfit(Actor actor, IReadOnlyDictionary<Ability, int> spend)
        {
            if (actor == null) return;

            foreach (Skill skill in Skills) actor.Train(skill);

            foreach (KeyValuePair<Ability, int> raise in spend ??
                                                         new Dictionary<Ability, int>())
                actor.Scores.Raise(raise.Key, raise.Value);
        }

        public override string ToString() =>
            $"{Id}: {string.Join(", ", Skills.Select(s => s.Id()))}, " +
            $"raises {string.Join("/", Abilities.Select(a => a.Id()))}";
    }

    public static class BackgroundReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Background> backgrounds,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<Background>();
            var trouble = new List<string>();

            backgrounds = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                foreach (JsonElement entry in document.RootElement.Items("backgrounds"))
                {
                    string id = entry.Text("id");

                    if (!Json.IsId(id))
                    {
                        trouble.Add($"'{id}' is not a background id");
                        continue;
                    }

                    var skills = new List<Skill>();

                    foreach (string skill in entry.Strings("skills"))
                    {
                        if (Core.Characters.Skills.TryParse(skill, out Skill read)) skills.Add(read);
                        else trouble.Add($"{id}: '{skill}' is not a skill");
                    }

                    if (skills.Count != 2)
                        trouble.Add($"{id}: {skills.Count} skills - SRD gives a background two");

                    var abilities = new List<Ability>();

                    foreach (string ability in entry.Strings("abilities"))
                    {
                        if (Core.Characters.Abilities.TryParse(ability, out Ability read))
                            abilities.Add(read);
                        else trouble.Add($"{id}: '{ability}' is not an ability");
                    }

                    if (abilities.Count != 3)
                        trouble.Add($"{id}: {abilities.Count} abilities - SRD names three to " +
                                    "spend the +2 and +1 on");

                    found.Add(new Background(id, skills, abilities,
                                             entry.Strings("gear"), entry.Number("gold")));
                }

                if (found.Count == 0 && trouble.Count == 0)
                    trouble.Add("no backgrounds in it");
            }

            return trouble.Count == 0;
        }
    }
}
