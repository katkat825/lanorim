using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Sheet;
using Content.Species;
using Core.Characters;
using Core.Localization;
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

        // a character made above level 4 spends its ability score improvements here, one by one.
        // it is a step Next STOPS on while any is unspent: the screen pre-fills each with the
        // class's suggestion (SuggestedImprovement), and the player says yes or changes it - never
        // spent on the player's behalf (decisions_checklist.md section 1, 2026-09-24)
        Improvements,

        Skills,
        Spells,

        // A STEP `Next` NEVER STOPS ON, and that is not an oversight. The choice is pre-answered
        // with the SRD's own mode, so there is nothing creation has to wait for - a player who
        // never opens this step gets slots, which is the right default. It is a Step so the UI has
        // somewhere to put it; ChoosesResource says whether to show it at all.
        SpellResource,

        // ALSO NEVER STOPPED ON, for the same reason: pre-answered (true neutral), so a player who
        // skips it still has the field the sheet requires. make Next stop here if it should be
        // asked every time - that is a one-line change and the call is Kathleen's
        Alignment,

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
        readonly List<AbilityImprovement> _improvements = new List<AbilityImprovement>();

        public IReadOnlyList<AbilityImprovement> Improvements => _improvements;

        // how many the starting level gives, and how many are still to choose
        public int ImprovementPicks => Hero.AbilityScoreImprovements(Level, Class);

        public int ImprovementPicksLeft => Math.Max(0, ImprovementPicks - _improvements.Count);

        // a score as it will stand once the species, the background's spend and the improvements
        // chosen so far are on it - what the cap of 20 is measured against
        public int ScoreAfter(Ability ability) =>
            Scores.Base(ability) +
            (Species?.Bumps.TryGetValue(ability, out int bump) == true ? bump : 0) +
            (Lineage?.Bumps.TryGetValue(ability, out int lineageBump) == true ? lineageBump : 0) +
            (_backgroundSpend.TryGetValue(ability, out int spend) ? spend : 0) +
            _improvements.SelectMany(i => i.Points).Where(p => p.ability == ability)
                         .Sum(p => p.points);

        public bool Improve(AbilityImprovement choice, out string whyNotKey)
        {
            whyNotKey = null;

            if (ImprovementPicksLeft <= 0)
            {
                whyNotKey = ImprovementRefusals.Key(ImprovementRefusals.NonePending);
                return false;
            }

            string why = ImprovementRefusals.Check(choice, ScoreAfter);

            if (why != null)
            {
                whyNotKey = ImprovementRefusals.Key(why);
                return false;
            }

            _improvements.Add(choice);
            return true;
        }

        public bool Improve(AbilityImprovement choice) => Improve(choice, out _);

        // back a step on the improvements screen
        public bool Unimprove()
        {
            if (_improvements.Count == 0) return false;

            _improvements.RemoveAt(_improvements.Count - 1);
            return true;
        }

        // what the screen pre-fills: +2 on the class's first priority that has room for it
        public AbilityImprovement SuggestedImprovement()
        {
            IEnumerable<Ability> order = (Class?.Priority ?? Array.Empty<Ability>())
                                         .Concat(Abilities.All).Distinct();

            foreach (Ability ability in order)
                if (ScoreAfter(ability) <= Abilities.Ceiling - 2) return AbilityImprovement.Two(ability);

            List<Ability> room = order.Where(a => ScoreAfter(a) < Abilities.Ceiling).ToList();

            return room.Count >= 2
                ? AbilityImprovement.OneEach(room[0], room[1])
                : AbilityImprovement.Two(Ability.Strength);
        }

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

        // the class's picks, and one more for a feature that grants it by this level (SRD 5.2.1
        // Primal Knowledge, p.29)
        public int SkillPicks =>
            (Class?.SkillPicks ?? 0) +
            (Class?.Features.Where(f => f.Level <= Level).Sum(f => f.SkillPicks) ?? 0);

        public int SkillPicksLeft => Math.Max(0, SkillPicks - _skills.Count);

        public int ExpertisePicks =>
            Class?.Features.Where(f => f.Trait == Trait.Expertise && f.Level <= Level)
                           .Sum(f => f.Count) ?? 0;

        public int ExpertisePicksLeft => Math.Max(0, ExpertisePicks - _expertise.Count);

        // the spells a fresh caster may put on the sheet: its class's list, cantrips and level 1
        // WHICH WAY THIS CHARACTER WILL PAY FOR LEVELED SPELLS. Slots is pre-selected because it
        // is the SRD's own answer, and a player who does not care which they have should end up
        // holding the faithful one rather than the variant.
        //
        // Both labels are keys, not words: the screen shows "Spell slots (classic D&D)" against
        // "Spell points (simpler bookkeeping)" in whatever language it is being read in.
        public SpellResourceMode Resource { get; private set; } = SpellResourceMode.Slots;

        public Alignment Alignment { get; private set; } = Alignment.Neutral;

        public void Pick(Alignment alignment) => Alignment = alignment;

        // a non-caster is never asked, and answering for one is refused rather than ignored
        public bool ChoosesResource => Class != null && Class.Casts;

        public bool Pick(SpellResourceMode mode)
        {
            if (!ChoosesResource) return false;

            Resource = mode;
            return true;
        }

        public static string LabelKey(SpellResourceMode mode) =>
            KeyConventions.Key(KeyConventions.UiNs, "spell_resource",
                               mode.ToString().ToLowerInvariant(), "name");

        public static string BlurbKey(SpellResourceMode mode) =>
            KeyConventions.Key(KeyConventions.UiNs, "spell_resource",
                               mode.ToString().ToLowerInvariant(), "description");

        public static IEnumerable<string> ResourceKeys() =>
            System.Enum.GetValues<SpellResourceMode>()
                       .SelectMany(m => new[] { LabelKey(m), BlurbKey(m) });

        public IEnumerable<Spell> SpellChoices =>
            Class == null || !Class.Casts
                ? Enumerable.Empty<Spell>()
                : Library.Spells.For(Class.Id)
                         .Where(s => s.Level <= HighestSpellLevel)
                         .Where(s => !_spells.Any(k => k.Id == s.Id));

        // the top of the class's own slot table at this level: a level 5 Paladin has 2nd-level
        // slots, not 3rd (SRD 5.2.1 p.53)
        public int HighestSpellLevel =>
            Class == null || !Class.Casts
                ? 1
                : Math.Clamp(SpellPoints.HighestLevelFor(Class.Progression, Level), 1, 9);

        // SRD 5.2.1's Cantrips column: a Wizard or Cleric 3, 4 at 4th level, 5 at 10th; a Druid
        // one fewer; a Paladin none. read off the spellcasting feature, and off the class's own
        // list for a class that says nothing
        public int CantripPicks
        {
            get
            {
                if (Class == null || !Class.Casts || !Library.Spells.For(Class.Id).Any(s => s.IsCantrip))
                    return 0;

                int table = Class.Spellcasting.CantripsAt(Level);

                return table >= 0 ? table : 2;
            }
        }

        // SRD 5.2.1's Prepared Spells column - the flat known/equipped model spends it as the
        // spells on the sheet. always-prepared spells come on top (Caster.Prepare)
        public int SpellPicks
        {
            get
            {
                if (Class == null || !Class.Casts) return 0;

                int table = Class.Spellcasting.KnownAt(Level);

                return table >= 0 ? table : 2 + Level;
            }
        }

        public int CantripPicksLeft =>
            Math.Max(0, CantripPicks - _spells.Count(s => s.IsCantrip));

        public int SpellPicksLeft =>
            Math.Max(0, SpellPicks - _spells.Count(s => !s.IsCantrip));

        public int Level { get; private set; } = 1;

        public void StartAt(int level)
        {
            Level = Proficiency.Clamp(level);

            // a lower starting level has fewer to spend
            while (_improvements.Count > ImprovementPicks) _improvements.RemoveAt(_improvements.Count - 1);
        }


        // --- picking ----------------------------------------------------------------------------

        public bool Pick(CharacterClass cls)
        {
            if (cls == null || !Library.Classes.Contains(cls)) return false;

            Class = cls;

            _skills.Clear();
            _expertise.Clear();
            _spells.Clear();
            _improvements.Clear();

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

            // a Scholar's Expertise is one of six skills of learning (SRD 5.2.1 p.78)
            List<Feature> expertise = Class?.Features.Where(f => f.Trait == Trait.Expertise &&
                                                                 f.Level <= Level).ToList()
                                      ?? new List<Feature>();

            if (expertise.Count > 0 && expertise.All(f => f.ExpertiseFrom.Count > 0) &&
                !expertise.Any(f => f.ExpertiseFrom.Contains(skill)))
                return false;

            _expertise.Add(skill);
            return true;
        }

        // THE SCREEN'S UNDO: a pick taken back. A skill that has an expertise on it takes the
        // expertise with it, since expertise doubles a proficiency the character no longer has
        public bool Untrain(Skill skill)
        {
            if (!_skills.Remove(skill)) return false;

            if (!(Background?.Skills.Contains(skill) ?? false)) _expertise.Remove(skill);

            return true;
        }

        public bool Unmaster(Skill skill) => _expertise.Remove(skill);

        public bool Unlearn(Spell spell) => spell != null && _spells.RemoveAll(s => s.Id == spell.Id) > 0;

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
          : ImprovementPicksLeft > 0 ? Step.Improvements
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

                if (ImprovementPicksLeft > 0)
                    problems.Add($"{ImprovementPicksLeft} ability score improvements still to spend");

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

            var hero = new Hero(Name, Class, Species, Background, Scores.Copy(), Level, Lineage,
                                Resource)
            {
                Alignment = Alignment,
            };

            hero.Build(_backgroundSpend, _skills, _expertise, Library.Items, _spells, _improvements);

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
