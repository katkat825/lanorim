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
    public sealed partial class Creation
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
            whyNotKey = ImprovementRefusals.WhyNot(ImprovementPicksLeft, choice, ScoreAfter);

            if (whyNotKey != null) return false;

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

        // what the screen pre-fills: the suggestion (AbilityImprovement.Suggested) on the scores as
        // they will stand
        public AbilityImprovement SuggestedImprovement() =>
            AbilityImprovement.Suggested(Class?.Priority, ScoreAfter);

        public IReadOnlyDictionary<Ability, int> BackgroundSpend => _backgroundSpend;

        public IReadOnlyList<Skill> Skills => _skills;

        public IReadOnlyList<Skill> Expertise => _expertise;

        public IReadOnlyList<Spell> Spells => _spells;

        public override string ToString() =>
            $"{(Name.Length == 0 ? "unnamed" : Name)}: " +
            $"{Species?.Id ?? "?"} {Class?.Id ?? "?"} level {Level}, next {Next}";
    }
}
