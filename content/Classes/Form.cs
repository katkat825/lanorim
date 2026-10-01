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
    // what a form card is for. the four jobs v1_class_roster.md names, and the reason the list is
    // curated at all: a druid picks a card by what it needs doing, not by leafing through a
    // bestiary
    [Fallback(FormRole.Utility)]
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
            $"{Id} [{EnumWords.Name(Role)}] cr {ChallengeTimesTen / 10.0}, " +
            $"level {MinimumLevel}: ac {ArmorClass}, speed {Speed}" +
            (Climb > 0 ? $" climb {Climb}" : "") + (Swim > 0 ? $" swim {Swim}" : "") +
            (Fly > 0 ? $" fly {Fly}" : "") + $", {Attacks.Count} attacks";
    }

    public static class FormRoles
    {
        public static readonly IReadOnlyList<FormRole> All =
            Enum.GetValues(typeof(FormRole)).Cast<FormRole>().ToList();
    }

    public static class FormReader
    {
        public static bool TryRead(string text, out IReadOnlyList<Form> forms,
                                   out IReadOnlyList<string> problems) =>
            Entries.TryRead(text, out forms, out problems);

        // every key a Wild Shape card takes
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "id", "role", "challenge_times_ten", "armor_class", "speed", "climb", "swim", "fly", "scores",
            "skills", "attacks", "mini",
        };

        // the file: { "forms": [ ... ] }, each entry read by ReadOne (EntryList)
        public static readonly EntryList<Form> Entries =
            new EntryList<Form>("forms", "form", Keys, ReadOne);

        // a form's attack: its name; the rest is what every attack takes (AttackReader.Keys)
        public static readonly IReadOnlyList<string> AttackKeys = new[] { "id" };

        static Form ReadOne(JsonElement entry, string id, List<string> problems)
        {
            if (!EnumWords.TryParse(entry.Text("role"), out FormRole role))
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
            Dictionary<Ability, int> body = entry.AbilityRecord("scores", problems, id);

            foreach (Ability mind in body.Keys.Where(a => !Shape.Physical.Contains(a)).ToList())
            {
                problems.Add($"{id}: '{mind.Id()}' is the druid's own - a form sets str, dex and con " +
                             "and nothing else");
                body.Remove(mind);
            }

            foreach (Ability ability in Shape.Physical)
                if (!body.ContainsKey(ability))
                    problems.Add($"{id}: no {ability.Id()} score");

            var attacks = new List<Attack>();

            foreach (JsonElement raw in entry.Items("attacks"))
            {
                string name = raw.Text("id");

                if (!Json.IsId(name))
                {
                    problems.Add($"{id}: '{name}' is not an attack id");
                    continue;
                }

                Keyed.OnlyKnown(raw, AttackKeys.Concat(AttackReader.Keys), $"{id}/{name}", problems);

                // a borrowed body's claws and teeth: nothing to drop
                attacks.Add(AttackReader.Read(raw, name, Hand.None, $"{id}/{name}", problems));
            }

            if (attacks.Count == 0) problems.Add($"{id}: a form with nothing to attack with");

            var skills = entry.SkillList("skills", problems, id).ToList();

            return new Form(id, role, Math.Max(0, challenge),
                            entry.Number("armor_class", 10),
                            body.TryGetValue(Ability.Strength, out int str) ? str : 10,
                            body.TryGetValue(Ability.Dexterity, out int dex) ? dex : 10,
                            body.TryGetValue(Ability.Constitution, out int con) ? con : 10,
                            attacks,
                            entry.WalkingSpeed(id, problems),
                            entry.Number("climb"),
                            entry.Number("swim"),
                            entry.Number("fly"),
                            skills,
                            entry.Text("mini"));
        }
    }

    // every Wild Shape card, by id
    public sealed class FormShelf : Catalogue<Form>
    {
        public FormShelf(IEnumerable<Form> forms, IEnumerable<string> problems = null)
            : base(forms, f => f.Id, f => f.Keys(), problems)
        {
        }

        // the declared product constraint of v1_class_roster.md: three to five cards, and a
        // sixth is a beast-catalogue converter by the back door
        public const int Fewest = 3;
        public const int Most = 5;

        public override IEnumerable<Form> All =>
            Stock.OrderBy(f => f.MinimumLevel).ThenBy(f => f.Role)
                 .ThenBy(f => f.Id, StringComparer.Ordinal);

        // the cards a druid of this level may pick up
        public IEnumerable<Form> OpenAt(int level) => All.Where(f => f.OpenAt(level));

        public IEnumerable<Form> For(FormRole role) => All.Where(f => f.Role == role);

        // read once and shared: a catalogue is read-only (Catalogue)
        static readonly Lazy<FormShelf> TheSrd = new(Read);

        public static FormShelf Srd() => TheSrd.Value;

        static FormShelf Read()
        {
            List<Form> forms = ReadSrd("forms", FormReader.TryRead, out List<string> problems);

            if (forms.Count < Fewest || forms.Count > Most)
                problems.Add($"{forms.Count} Wild Shape forms - v1 ships {Fewest} to {Most}");

            foreach (FormRole role in FormRoles.All)
                if (!forms.Any(f => f.Role == role))
                    problems.Add($"no {role.Id()} form - v1_class_roster.md names all four jobs");

            return new FormShelf(forms, problems);
        }

        public override string ToString() => Counted("forms");
    }
}
