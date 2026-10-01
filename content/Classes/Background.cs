using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Localization;
using Core.Words;

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

        // the gold instead of the gear (SRD 5.2.1 p.83: 50 GP for every background)
        public int GoldInstead { get; init; }

        // Lanorim's own, not the SRD's (Recluse): the word a spell that isn't the SRD's carries
        public bool NotInSrd { get; init; }

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
        // every key a background takes
        public static readonly IReadOnlyList<string> Keys = new[] { "id", "skills", "abilities", "gear", "gold", "gold_instead", "not_in_srd" };

        // the file: { "backgrounds": [ ... ] }, each entry read by ReadOne (EntryList)
        public static readonly EntryList<Background> Entries =
            new EntryList<Background>("backgrounds", "background", Keys, ReadOne);

        public static bool TryRead(string text, out IReadOnlyList<Background> backgrounds,
                                   out IReadOnlyList<string> problems) =>
            Entries.TryRead(text, out backgrounds, out problems);

        static Background ReadOne(JsonElement entry, string id, List<string> problems)
        {
            var skills = entry.SkillList("skills", problems, id).ToList();

            if (skills.Count != 2)
                problems.Add($"{id}: {skills.Count} skills - SRD gives a background two");

            var abilities = entry.AbilityList("abilities", problems, id).ToList();

            if (abilities.Count != 3)
                problems.Add($"{id}: {abilities.Count} abilities - SRD names three to " +
                             "spend the +2 and +1 on");

            return new Background(id, skills, abilities, entry.Strings("gear"), entry.Number("gold"))
            {
                NotInSrd = entry.Flag("not_in_srd"),
                GoldInstead = entry.Number("gold_instead"),
            };
        }
    }
}
