using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Sheet;
using Content.Species;
using Core.Characters;
using Core.Magic;

namespace Content.Creation
{
    // the character creator, as logic. guided, in the order the sheet reads
    // (decisions_checklist.md section 3 settles it as "whatever's easiest"), and every step can be
    // asked what it will accept - so the UI is a list of buttons over this and holds no rules.
    public enum Step
    {
        Class,
        Species,
        Lineage,
        Background,
        Abilities,
        Skills,
        Spells,
        Name,
        Done,
    }

    public sealed class Creation
    {
        public Creation(Library library, IReadOnlyList<Background> backgrounds)
        {
            Library = library ?? throw new ArgumentNullException(nameof(library));
            Backgrounds = backgrounds ?? Array.Empty<Background>();
            Scores = new AbilityScores(Abilities.PointBuyFloor);
        }

        public Library Library { get; }

        public IReadOnlyList<Background> Backgrounds { get; }

        public CharacterClass Class { get; private set; }

        public Kind Species { get; private set; }

        public Kind Lineage { get; private set; }

        public Background Background { get; private set; }

        public AbilityScores Scores { get; private set; }

        public string Name { get; private set; } = "";

        readonly Dictionary<Ability, int> _backgroundSpend = new Dictionary<Ability, int>();
        readonly List<Skill> _skills = new List<Skill>();
        readonly List<Skill> _expertise = new List<Skill>();
        readonly List<Spell> _spells = new List<Spell>();

        public IReadOnlyDictionary<Ability, int> BackgroundSpend => _backgroundSpend;

        public IReadOnlyList<Skill> Skills => _skills;

        public IReadOnlyList<Skill> Expertise => _expertise;

        public IReadOnlyList<Spell> Spells => _spells;


        // --- the steps --------------------------------------------------------------------------

        public IEnumerable<CharacterClass> ClassChoices =>
            Library.Classes.OrderBy(c => Roster.Classes.ToList().IndexOf(c.Id));

        public IEnumerable<Kind> SpeciesChoices =>
            Library.Playable.OrderBy(s => Roster.Species.ToList().IndexOf(s.Id));

        public IEnumerable<Kind> LineageChoices =>
            Species == null ? Enumerable.Empty<Kind>() : Library.LineagesOf(Species.Id);

        public bool NeedsLineage => LineageChoices.Any();

        public IEnumerable<Skill> SkillChoices =>
            Class?.SkillChoices.Where(s => !_skills.Contains(s)) ?? Enumerable.Empty<Skill>();

        public int SkillPicksLeft => Math.Max(0, (Class?.SkillPicks ?? 0) - _skills.Count);

        public int ExpertisePicks =>
            Class?.Features.Where(f => f.Trait == Trait.Expertise && f.Level <= Level)
                           .Sum(f => f.Count) ?? 0;

        public int ExpertisePicksLeft => Math.Max(0, ExpertisePicks - _expertise.Count);

        // the spells a fresh caster may put on the sheet: its class's list, cantrips and level 1
        public IEnumerable<Spell> SpellChoices =>
            Class == null || !Class.Casts
                ? Enumerable.Empty<Spell>()
                : Library.Spells.For(Class.Id)
                         .Where(s => s.Level <= HighestSpellLevel)
                         .Where(s => !_spells.Any(k => k.Id == s.Id));

        public int HighestSpellLevel => Math.Clamp((Level + 1) / 2, 1, 9);

        // a Paladin casts and has no cantrips in SRD, so the number is read off the class's own
        // list rather than assumed from "does it cast"
        public int CantripPicks =>
            Class != null && Class.Casts && Library.Spells.For(Class.Id).Any(s => s.IsCantrip)
                ? 2 : 0;

        public int SpellPicks => Class != null && Class.Casts ? 2 + Level : 0;

        public int CantripPicksLeft =>
            Math.Max(0, CantripPicks - _spells.Count(s => s.IsCantrip));

        public int SpellPicksLeft =>
            Math.Max(0, SpellPicks - _spells.Count(s => !s.IsCantrip));

        public int Level { get; private set; } = 1;

        public void StartAt(int level) => Level = Proficiency.Clamp(level);


        // --- picking ----------------------------------------------------------------------------

        public bool Pick(CharacterClass cls)
        {
            if (cls == null || !Library.Classes.Contains(cls)) return false;

            Class = cls;

            _skills.Clear();
            _expertise.Clear();
            _spells.Clear();

            // the point-buy array is dealt into the class's priority order, which is the "guided"
            // half of the guided creator - the player can still move it
            Scores = Standard(cls);

            return true;
        }

        public bool Pick(Kind species)
        {
            if (species == null || species.IsLineage) return false;

            Species = species;
            Lineage = null;

            return true;
        }

        public bool PickLineage(Kind lineage)
        {
            if (lineage == null || Species == null || lineage.LineageOf != Species.Id) return false;

            Lineage = lineage;
            return true;
        }

        public bool Pick(Background background)
        {
            if (background == null || !Backgrounds.Contains(background)) return false;

            Background = background;
            _backgroundSpend.Clear();

            // the default spend is +2 to the class's first priority and +1 to the second, if the
            // background raises them; otherwise the first two it does raise
            foreach (Ability ability in (Class?.Priority ?? Array.Empty<Ability>())
                                        .Concat(background.Abilities)
                                        .Where(background.Abilities.Contains)
                                        .Distinct()
                                        .Take(2))
                _backgroundSpend[ability] = _backgroundSpend.Count == 0 ? 2 : 1;

            return _backgroundSpend.Values.Sum() == 3;
        }

        public bool Spend(IReadOnlyDictionary<Ability, int> spend)
        {
            if (Background == null || !Background.IsLegalSpend(spend, out _)) return false;

            _backgroundSpend.Clear();

            foreach (KeyValuePair<Ability, int> one in spend) _backgroundSpend[one.Key] = one.Value;

            return true;
        }

        public bool Train(Skill skill)
        {
            if (Class == null || SkillPicksLeft <= 0) return false;

            if (!Class.SkillChoices.Contains(skill) || _skills.Contains(skill)) return false;

            _skills.Add(skill);
            return true;
        }

        public bool Master(Skill skill)
        {
            if (ExpertisePicksLeft <= 0 || _expertise.Contains(skill)) return false;

            // SRD: Expertise doubles a proficiency you already have
            if (!_skills.Contains(skill) && !(Background?.Skills.Contains(skill) ?? false))
                return false;

            _expertise.Add(skill);
            return true;
        }

        public bool Learn(Spell spell)
        {
            if (spell == null || Class == null || !Class.Casts) return false;

            if (_spells.Any(s => s.Id == spell.Id)) return false;

            if (!spell.Classes.Contains(Class.Id, StringComparer.OrdinalIgnoreCase)) return false;

            if (spell.IsCantrip)
            {
                if (CantripPicksLeft <= 0) return false;
            }
            else
            {
                if (SpellPicksLeft <= 0 || spell.Level > HighestSpellLevel) return false;
            }

            _spells.Add(spell);
            return true;
        }

        public bool Call(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            Name = name.Trim();
            return true;
        }


        // --- where it is up to --------------------------------------------------------------------

        public Step Next =>
            Class == null ? Step.Class
          : Species == null ? Step.Species
          : NeedsLineage && Lineage == null ? Step.Lineage
          : Background == null ? Step.Background
          : !Scores.IsLegalPointBuy(out _) ? Step.Abilities
          : SkillPicksLeft > 0 || ExpertisePicksLeft > 0 ? Step.Skills
          : CantripPicksLeft > 0 || SpellPicksLeft > 0 ? Step.Spells
          : Name.Length == 0 ? Step.Name
          : Step.Done;

        public bool Ready => Next == Step.Done;

        public IReadOnlyList<string> Problems
        {
            get
            {
                var problems = new List<string>();

                if (Class == null) problems.Add("no class picked");
                if (Species == null) problems.Add("no species picked");
                if (NeedsLineage && Lineage == null) problems.Add($"{Species.Id} needs a lineage");
                if (Background == null) problems.Add("no background picked");

                if (!Scores.IsLegalPointBuy(out string spend)) problems.Add(spend);

                if (Background != null && !Background.IsLegalSpend(_backgroundSpend, out string bg))
                    problems.Add(bg);

                if (SkillPicksLeft > 0) problems.Add($"{SkillPicksLeft} skills still to pick");

                if (ExpertisePicksLeft > 0)
                    problems.Add($"{ExpertisePicksLeft} expertises still to pick");

                if (CantripPicksLeft > 0) problems.Add($"{CantripPicksLeft} cantrips still to pick");

                if (SpellPicksLeft > 0) problems.Add($"{SpellPicksLeft} spells still to pick");

                if (Name.Length == 0) problems.Add("no name");

                return problems;
            }
        }

        public Hero Finish()
        {
            if (!Ready) return null;

            var hero = new Hero(Name, Class, Species, Background, Scores.Copy(), Level, Lineage);

            hero.Build(_backgroundSpend, _skills, _expertise, Library.Items, _spells);

            return hero;
        }

        // the standard array, dealt into the class's priority order. 15 14 13 12 10 8 is exactly
        // the 27-point budget, so a guided creation is always legal
        public static AbilityScores Standard(CharacterClass cls)
        {
            int[] array = { 15, 14, 13, 12, 10, 8 };

            var order = new List<Ability>(cls?.Priority ?? Array.Empty<Ability>());

            foreach (Ability ability in Abilities.All)
                if (!order.Contains(ability))
                    order.Add(ability);

            var scores = new AbilityScores();

            for (int i = 0; i < order.Count && i < array.Length; i++)
                scores.SetBase(order[i], array[i]);

            return scores;
        }

        public override string ToString() =>
            $"{(Name.Length == 0 ? "unnamed" : Name)}: " +
            $"{Species?.Id ?? "?"} {Class?.Id ?? "?"} level {Level}, next {Next}";
    }
}
