using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Magic;
using Core.Dice;
using Core.Localization;

namespace Content.Classes
{
    // one of the seven (v1_class_roster.md). the subclass is folded in as features with a
    // subclass id on them, because one subclass each is the whole of v1's subclass system and a
    // separate type for it would be a second description of the same list.
    public sealed class CharacterClass
    {
        public CharacterClass(string id, Die hitDie,
                              IReadOnlyList<Ability> saves = null,
                              IReadOnlyList<Skill> skillChoices = null,
                              int skillPicks = 2,
                              IReadOnlyList<ArmorWeight> armorTraining = null,
                              bool shields = false,
                              IReadOnlyList<string> startingGear = null,
                              IReadOnlyList<Feature> features = null,
                              string subclass = null,
                              string companion = null,
                              IReadOnlyList<Ability> priority = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            HitDie = hitDie;
            Saves = saves ?? Array.Empty<Ability>();
            SkillChoices = skillChoices ?? Array.Empty<Skill>();
            SkillPicks = Math.Max(0, skillPicks);
            ArmorTraining = armorTraining ?? Array.Empty<ArmorWeight>();
            Shields = shields;
            StartingGear = startingGear ?? Array.Empty<string>();
            Features = features ?? Array.Empty<Feature>();
            Subclass = subclass ?? "";
            Companion = companion ?? "";
            Priority = priority ?? Array.Empty<Ability>();
        }

        public string Id { get; }

        public Die HitDie { get; }

        public IReadOnlyList<Ability> Saves { get; }

        public IReadOnlyList<Skill> SkillChoices { get; }

        public int SkillPicks { get; }

        public IReadOnlyList<ArmorWeight> ArmorTraining { get; }

        public bool Shields { get; }

        public IReadOnlyList<string> StartingGear { get; }

        public IReadOnlyList<Feature> Features { get; }

        public string Subclass { get; }

        // which companion voice it shares. Fighter shares Barbarian's, Paladin shares Cleric's -
        // the trick that makes the sixth and seventh class cheap (v1_class_roster.md)
        public string Companion { get; }

        // which abilities character creation should push the high numbers into
        public IReadOnlyList<Ability> Priority { get; }

        public IEnumerable<Feature> By(int level) => Features.Where(f => f.Level <= level);

        // THE LEVELS THAT BRING AN ABILITY SCORE IMPROVEMENT. SRD 5.2.1: 4, 8, 12, 16 and 19 (Epic
        // Boon - a feat, and feats are deferred, so +2 like the rest); the Fighter adds 6 and 14 and
        // the Rogue 10. Data, so the class says it
        public static readonly IReadOnlyList<int> UsualImprovementLevels = new[] { 4, 8, 12, 16, 19 };

        public IReadOnlyList<int> ImprovementLevels { get; init; } = UsualImprovementLevels;

        public int ImprovementsBy(int level) => ImprovementLevels.Count(l => level >= l);

        public Feature Spellcasting => Features.FirstOrDefault(f => f.Trait == Trait.Spellcasting);

        public bool Casts => Spellcasting != null;

        public Ability? CastingAbility => Spellcasting?.Ability;

        // SRD hit points: the first die is its maximum, every level after is the average rounded
        // up, and Constitution is added every time
        public int HitPointsAt(int level, int constitutionModifier)
        {
            int sides = HitDie.Sides();

            return sides + constitutionModifier +
                   Math.Max(0, level - 1) * (sides / 2 + 1 + constitutionModifier);
        }

        // how fast this class climbs the spell levels; None for a class that does not cast
        public CasterProgression Progression => Spellcasting?.Progression ?? CasterProgression.None;

        // THE RESOURCE THIS CLASS HANDS A CHARACTER OF THIS LEVEL, in the mode they chose at
        // creation. One progression feeds both: the slot table reads it directly, and the point
        // pool derives its size from it.
        public ISpellResource ResourceAt(int level, SpellResourceMode mode)
        {
            if (!Casts) return null;

            return mode == SpellResourceMode.Points
                ? SpellPoints.For(Progression, level)
                : SpellSlots.For(Progression, level);
        }

        public string NameKey => KeyConventions.ClassName(Id);

        public string DescriptionKey => KeyConventions.ClassDescription(Id);

        public string SubclassNameKey =>
            Subclass.Length == 0 ? "" : KeyConventions.ClassName(Subclass);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;

            if (Subclass.Length > 0) yield return SubclassNameKey;

            foreach (string key in Features.SelectMany(f => f.Keys())) yield return key;
        }

        // everything a class does to a fresh actor: hit points, proficiencies, features
        public void Outfit(Actor actor, int level, IEnumerable<Skill> chosenSkills = null)
        {
            if (actor == null) return;

            actor.SetLevel(level);

            int constitution = actor.AbilityModifier(Ability.Constitution);

            actor.SetHealth(new Health(HitPointsAt(level, constitution), HitDie, level));

            foreach (Ability save in Saves) actor.TrainSave(save);

            foreach (Skill skill in chosenSkills ?? Enumerable.Empty<Skill>()) actor.Train(skill);

            foreach (Feature feature in By(level)) feature.Grant(actor, level);

            // the spell resource is NOT set here. Outfit is handed an Actor, and an Actor no
            // longer holds one - it belongs to the Caster, which is Hero's to build because only
            // Hero knows which mode the player picked.
        }

        public override string ToString() =>
            $"{Id} ({HitDie.Label()})" +
            (Subclass.Length > 0 ? $", {Subclass}" : "") +
            $", {Features.Count} features" +
            (Casts ? $", casts with {CastingAbility.Value.Id()}" : "");
    }
}
