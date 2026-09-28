using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Species;
using Core.Characters;
using Core.Magic;

namespace Content.Creation
{
    public sealed partial class Creation
    {
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
    }
}
