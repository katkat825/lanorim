using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Rules;

namespace Content.Classes
{
    // what a form card is for. the four jobs v1_class_roster.md names, and the reason the list is
    // curated at all: a druid picks a card by what it needs doing, not by leafing through a
    // bestiary
    public enum FormRole
    {
        Scout,
        Travel,
        Combat,
        Utility,
    }

    // one Wild Shape card: an SRD 5.2.1 beast's statblock, cut down to what a borrowed body
    // changes. no hit points, because SRD 5.2.1 keeps the druid's own and hands over temporary
    // ones instead; no Intelligence, Wisdom or Charisma, because the mind stays the druid's.
    public sealed class Form
    {
        public Form(string id, FormRole role, int challengeTimesTen, int armorClass,
                    int strength, int dexterity, int constitution,
                    IReadOnlyList<Attack> attacks,
                    int speed = 30, int climb = 0, int swim = 0, int fly = 0,
                    IReadOnlyList<Skill> skills = null,
                    IReadOnlyDictionary<string, Rider> riders = null,
                    string mini = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Role = role;
            ChallengeTimesTen = Math.Max(0, challengeTimesTen);
            ArmorClass = armorClass;
            Strength = strength;
            Dexterity = dexterity;
            Constitution = constitution;
            Attacks = attacks ?? Array.Empty<Attack>();
            Speed = Math.Max(0, speed);
            Climb = Math.Max(0, climb);
            Swim = Math.Max(0, swim);
            Fly = Math.Max(0, fly);
            Skills = skills ?? Array.Empty<Skill>();
            Riders = riders ?? new Dictionary<string, Rider>();
            Mini = mini ?? "";
        }

        public string Id { get; }

        public FormRole Role { get; }

        // the same fixed point the bestiary uses: CR 1/4 is 2, CR 1/2 is 5, CR 1 is 10
        public int ChallengeTimesTen { get; }

        public int ArmorClass { get; }

        public int Strength { get; }

        public int Dexterity { get; }

        public int Constitution { get; }

        // what the druid swings with while it wears this. the druid keeps the HERO'S turn - the
        // two base actions stand in for the beast statblock's Multiattack, which a form does not
        // carry (a monster's own turn is ActionBudget.Statblock)
        public IReadOnlyList<Attack> Attacks { get; }

        // feet, like Actor.Speed
        public int Speed { get; }

        // the other ways it moves. the grid walks; these are for the campaign to read off the
        // card - a cat up a wall, a bear across a river
        public int Climb { get; }

        public int Swim { get; }

        public int Fly { get; }

        public IReadOnlyList<Skill> Skills { get; }

        // what an attack carries besides its own damage: the spider's venom. keyed by attack id
        public IReadOnlyDictionary<string, Rider> Riders { get; }

        // which model stands for it; Kathleen's to fill
        public string Mini { get; }

        // THE DRUID LEVEL THIS CARD OPENS AT, derived from the statblock and never written in the
        // data: SRD 5.2.1's Wild Shape table caps the challenge at 1/4 from level 2, 1/2 from 4
        // and 1 from 8, and allows a fly speed from 8. a card that says its own level is a second
        // copy of that table, and free to disagree with it.
        public int MinimumLevel => LevelFor(ChallengeTimesTen, Fly);

        public const int FirstLevel = 2;
        public const int FlyingLevel = 8;

        // past this no druid of the Land ever reaches, so the reader refuses it
        public const int HighestChallengeTimesTen = 10;

        public static int LevelFor(int challengeTimesTen, int fly)
        {
            int level = challengeTimesTen <= 2 ? FirstLevel
                      : challengeTimesTen <= 5 ? 4
                      : FlyingLevel;

            return fly > 0 ? Math.Max(level, FlyingLevel) : level;
        }

        public bool OpenAt(int level) => level >= MinimumLevel;

        public Rider RiderFor(Attack attack) =>
            attack != null && Riders.TryGetValue(attack.Id, out Rider rider) ? rider : null;

        public bool Owns(Attack attack) => attack != null && Attacks.Contains(attack);

        // what core is handed: the body, and nothing about druids
        public Shape ToShape(string source) =>
            new Shape(Id, source, Strength, Dexterity, Constitution, ArmorClass, Speed, Skills);

        public string NameKey => KeyConventions.FormName(Id);

        public string DescriptionKey => KeyConventions.FormDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;

            // a claw is named where a sword is, because the attack card asks every Attack the
            // same question
            foreach (Attack attack in Attacks) yield return attack.NameKey;
        }

        public override string ToString() =>
            $"{Id} [{Role.ToString().ToLowerInvariant()}] cr {ChallengeTimesTen / 10.0}, " +
            $"level {MinimumLevel}: ac {ArmorClass}, speed {Speed}" +
            (Climb > 0 ? $" climb {Climb}" : "") + (Swim > 0 ? $" swim {Swim}" : "") +
            (Fly > 0 ? $" fly {Fly}" : "") + $", {Attacks.Count} attacks";
    }

    public static class FormRoles
    {
        public static readonly IReadOnlyList<FormRole> All =
            Enum.GetValues(typeof(FormRole)).Cast<FormRole>().ToList();

        public static string Id(this FormRole role) => role.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out FormRole role)
        {
            foreach (FormRole r in All)
            {
                if (!string.Equals(r.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                role = r;
                return true;
            }

            role = FormRole.Utility;
            return false;
        }
    }

    public static class FormReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Form> forms,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<Form>();
            var trouble = new List<string>();

            forms = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                foreach (JsonElement entry in document.RootElement.Items("forms"))
                {
                    Form form = ReadOne(entry, trouble);

                    if (form != null) found.Add(form);
                }

                if (found.Count == 0 && trouble.Count == 0)
                    trouble.Add("no forms in it - the file is an object with a 'forms' array");
            }

            return trouble.Count == 0;
        }

        static Form ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a form id");
                return null;
            }

            if (!FormRoles.TryParse(entry.Text("role"), out FormRole role))
                problems.Add($"{id}: '{entry.Text("role")}' is not a role " +
                             $"({string.Join(", ", FormRoles.All.Select(r => r.Id()))})");

            int challenge = entry.Number("challenge_times_ten", -1);

            if (challenge < 0)
                problems.Add($"{id}: no challenge_times_ten - the level the card opens at is " +
                             "worked out from it");
            else if (challenge > Form.HighestChallengeTimesTen)
                problems.Add($"{id}: challenge {challenge / 10.0} - Wild Shape never reaches " +
                             $"past {Form.HighestChallengeTimesTen / 10.0}");

            if (!entry.Has("armor_class")) problems.Add($"{id}: no armor_class");

            // the body and only the body. a card that set Wisdom would be setting the druid's
            var body = new Dictionary<Ability, int>();

            if (entry.Has("scores"))
                foreach (JsonProperty score in entry.GetProperty("scores").EnumerateObject())
                {
                    if (!Abilities.TryParse(score.Name, out Ability ability) ||
                        !score.Value.TryGetInt32(out int value))
                    {
                        problems.Add($"{id}: '{score.Name}' is not an ability score");
                        continue;
                    }

                    if (!Shape.Physical.Contains(ability))
                    {
                        problems.Add($"{id}: '{score.Name}' is the druid's own - a form " +
                                     "sets str, dex and con and nothing else");
                        continue;
                    }

                    body[ability] = value;
                }

            foreach (Ability ability in Shape.Physical)
                if (!body.ContainsKey(ability))
                    problems.Add($"{id}: no {ability.Id()} score");

            var attacks = new List<Attack>();
            var riders = new Dictionary<string, Rider>(StringComparer.Ordinal);

            foreach (JsonElement raw in entry.Items("attacks"))
            {
                string name = raw.Text("id");

                if (!Json.IsId(name))
                {
                    problems.Add($"{id}: '{name}' is not an attack id");
                    continue;
                }

                DiceRoll damage = raw.Dice("damage", problems, id);

                if (damage.IsNothing) problems.Add($"{id}/{name}: an attack with no damage");

                DamageType type = raw.Damage("damage_type", problems, id);

                if (type == DamageType.None)
                    problems.Add($"{id}/{name}: an attack with no damage_type");

                Ability? ability = raw.Ability("ability", problems, id);

                attacks.Add(new Attack(name, damage, type,
                                       ability ?? Ability.Strength,
                                       true,
                                       raw.Number("reach", 1),
                                       raw.Number("range"),
                                       raw.Number("long_range"),
                                       Hand.None,
                                       raw.Flag("finesse"),
                                       0, 0,
                                       raw.Flag("adds_ability", true)));

                if (!raw.Has("rider")) continue;

                JsonElement extra = raw.GetProperty("rider");

                DiceRoll more = extra.Dice("damage", problems, id);
                DamageType moreType = extra.Damage("damage_type", problems, id);
                Condition condition = extra.Condition("condition", problems, id);

                if (more.IsNothing && condition == Condition.None)
                    problems.Add($"{id}/{name}: a rider that adds neither damage nor a condition");

                riders[name] = new Rider(name, more, moreType, condition);
            }

            if (attacks.Count == 0) problems.Add($"{id}: a form with nothing to attack with");

            var skills = new List<Skill>();

            foreach (string skill in entry.Strings("skills"))
            {
                if (Core.Characters.Skills.TryParse(skill, out Skill read)) skills.Add(read);
                else problems.Add($"{id}: '{skill}' is not a skill");
            }

            return new Form(id, role, Math.Max(0, challenge),
                            entry.Number("armor_class", 10),
                            body.TryGetValue(Ability.Strength, out int str) ? str : 10,
                            body.TryGetValue(Ability.Dexterity, out int dex) ? dex : 10,
                            body.TryGetValue(Ability.Constitution, out int con) ? con : 10,
                            attacks,
                            entry.Number("speed", 30),
                            entry.Number("climb"),
                            entry.Number("swim"),
                            entry.Number("fly"),
                            skills, riders,
                            entry.Text("mini"));
        }
    }

    // every Wild Shape card, by id
    public sealed class FormShelf
    {
        readonly Dictionary<string, Form> _byId;

        public FormShelf(IEnumerable<Form> forms, IEnumerable<string> problems = null)
        {
            _byId = new Dictionary<string, Form>(StringComparer.Ordinal);

            foreach (Form form in forms ?? Enumerable.Empty<Form>())
                if (form != null)
                    _byId[form.Id] = form;

            Problems = (problems ?? Enumerable.Empty<string>()).ToList();
        }

        // the declared product constraint of v1_class_roster.md: three to five cards, and a
        // sixth is a beast-catalogue converter by the back door
        public const int Fewest = 3;
        public const int Most = 5;

        public IReadOnlyList<string> Problems { get; }

        public bool Sound => Problems.Count == 0;

        public int Count => _byId.Count;

        public IEnumerable<Form> All =>
            _byId.Values.OrderBy(f => f.MinimumLevel).ThenBy(f => f.Role)
                 .ThenBy(f => f.Id, StringComparer.Ordinal);

        public Form Find(string id) =>
            id != null && _byId.TryGetValue(id, out Form form) ? form : null;

        public bool Has(string id) => Find(id) != null;

        // the cards a druid of this level may pick up
        public IEnumerable<Form> OpenAt(int level) => All.Where(f => f.OpenAt(level));

        public IEnumerable<Form> For(FormRole role) => All.Where(f => f.Role == role);

        public IEnumerable<string> Keys() => All.SelectMany(f => f.Keys());

        public static FormShelf Srd()
        {
            var forms = new List<Form>();
            var problems = new List<string>();

            foreach ((string path, string text) in Schema.Srd.ReadFolder("forms"))
            {
                FormReader.TryRead(text, out IReadOnlyList<Form> read,
                                   out IReadOnlyList<string> trouble);

                forms.AddRange(read);
                problems.AddRange(trouble.Select(t => $"{path}: {t}"));
            }

            if (forms.Count < Fewest || forms.Count > Most)
                problems.Add($"{forms.Count} Wild Shape forms - v1 ships {Fewest} to {Most}");

            foreach (FormRole role in FormRoles.All)
                if (!forms.Any(f => f.Role == role))
                    problems.Add($"no {role.Id()} form - v1_class_roster.md names all four jobs");

            return new FormShelf(forms, problems);
        }

        public override string ToString() =>
            $"{Count} forms" + (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
    }
}
