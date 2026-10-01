using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Core.Characters;

namespace Content.Creation
{
    // THE SPECIES' OWN CHOICES (cc_task_e-shop-species-and-ui-notes.md 1.3, SRD 5.2.1 pp.84-86): the Human's Skillful
    // (any one skill), the Elf's Keen Senses (Insight, Perception or Survival), the spellcasting ability a lineage's or a
    // legacy's spells use (Intelligence, Wisdom or Charisma), and Medium or Small for the Human and the Tiefling. All
    // read off the species' and lineage's features and sizes, so a campaign's own species asks the same way. One step,
    // Traits, after the background; the size is pre-answered (the species' first) the way the spell resource is
    public sealed partial class Creation
    {
        readonly List<Skill> _traitSkills = new List<Skill>();

        Size? _size;

        // the species' and the lineage's features this level has
        IEnumerable<Feature> OriginFeatures =>
            new[] { Species, Lineage }.Where(k => k != null)
                                      .SelectMany(k => k.Features)
                                      .Where(f => f.Level <= Level);

        IEnumerable<Feature> SkillPickers => OriginFeatures.Where(f => f.SkillPicks > 0);

        public int TraitSkillPicks => SkillPickers.Sum(f => f.SkillPicks);

        public int TraitSkillPicksLeft => Math.Max(0, TraitSkillPicks - _traitSkills.Count);

        public IReadOnlyList<Skill> TraitSkills => _traitSkills;

        // what the trait offers (every skill when it names none), less what the background or another species trait
        // already trains (the Human's Versatile, Perception) and what has been picked: a second proficiency in one
        // skill would be a wasted pick
        public IEnumerable<Skill> TraitSkillChoices =>
            SkillPickers.SelectMany(f => f.Skills.Count > 0 ? f.Skills : Core.Characters.Skills.All)
                        .Distinct()
                        .Where(s => !(Background?.Skills.Contains(s) ?? false) && !_traitSkills.Contains(s) &&
                                    !OriginFeatures.Any(f => f.Trait == Trait.Training && f.SkillPicks == 0 &&
                                                             f.Skills.Contains(s)))
                        .OrderBy(s => (int)s);

        public bool PickTraitSkill(Skill skill)
        {
            if (TraitSkillPicksLeft <= 0 || !TraitSkillChoices.Contains(skill)) return false;

            _traitSkills.Add(skill);
            _skills.Remove(skill);
            return true;
        }

        public bool UnpickTraitSkill(Skill skill) => _traitSkills.Remove(skill);

        // "Intelligence, Wisdom, or Charisma is your spellcasting ability for the spells you cast with this trait"
        public IReadOnlyList<Ability> SpellAbilityChoices =>
            OriginFeatures.SelectMany(f => f.SpellAbilities).Distinct().ToList();

        public Ability? SpellAbility { get; private set; }

        public bool PickSpellAbility(Ability ability)
        {
            if (!SpellAbilityChoices.Contains(ability)) return false;

            SpellAbility = ability;
            return true;
        }

        public IReadOnlyList<Size> SizeChoices => Species?.Sizes ?? Array.Empty<Size>();

        public bool ChoosesSize => Species?.ChoosesSize ?? false;

        public Size Size => _size ?? (SizeChoices.Count > 0 ? SizeChoices[0] : Size.Medium);

        public bool PickSize(Size size)
        {
            if (!SizeChoices.Contains(size)) return false;

            _size = size;
            return true;
        }

        // FOR A HERO NOBODY IS MAKING BY HAND - the sim's, a probe's `--begin`, a test's: the first skill offered and
        // the best of the abilities allowed. never called by the creator's screen: there the player picks
        public void SuggestTraits()
        {
            while (TraitSkillPicksLeft > 0 && TraitSkillChoices.FirstOrDefault() is var skill && PickTraitSkill(skill)) { }

            if (SpellAbility == null && SpellAbilityChoices.Count > 0)
                SpellAbility = SpellAbilityChoices.OrderByDescending(ScoreAfter).First();
        }

        // the traits the page is about: the skill pickers and the features whose spells take the chosen ability
        public IEnumerable<Feature> TraitFeatures =>
            OriginFeatures.Where(f => f.SkillPicks > 0 || f.SpellAbilities.Count > 0);

        // whether the Traits step has anything on it at all
        public bool HasTraits => TraitSkillPicks > 0 || SpellAbilityChoices.Count > 0 || ChoosesSize;

        // what the step still waits on: a skill to pick, or an ability (the size never waits)
        bool TraitsOpen => TraitSkillPicksLeft > 0 || SpellAbilityChoices.Count > 0 && SpellAbility == null;

        // a new species or lineage starts its traits over
        void ForgetTraits(bool sizeToo)
        {
            _traitSkills.Clear();
            SpellAbility = null;

            if (sizeToo) _size = null;
        }

        IEnumerable<string> TraitProblems()
        {
            if (TraitSkillPicksLeft > 0) yield return $"{TraitSkillPicksLeft} species skills still to pick";

            if (SpellAbilityChoices.Count > 0 && SpellAbility == null)
                yield return "no spellcasting ability picked for the species' spells";
        }
    }
}
