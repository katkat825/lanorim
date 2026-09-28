using System.Collections.Generic;
using System.Linq;

namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- training -------------------------------------------------------------------------

        readonly Dictionary<Skill, Training> _skills = new();
        readonly HashSet<Ability> _saves = new();

        public Training TrainingIn(Skill skill) =>
            _skills.TryGetValue(skill, out Training t) ? t : Training.Untrained;

        public void Train(Skill skill, Training training = Training.Proficient)
        {
            if (skill == Skill.None) return;

            // never demotes: a Rogue's Expertise landing after the background's proficiency is the
            // normal order, and the reverse order must not undo it
            if (TrainingIn(skill) < training) _skills[skill] = training;
        }

        public IEnumerable<Skill> TrainedSkills =>
            Skills.All.Where(s => TrainingIn(s) != Training.Untrained);

        public bool SavesWith(Ability ability) => _saves.Contains(ability);

        public void TrainSave(Ability ability) => _saves.Add(ability);

        public IEnumerable<Ability> SaveProficiencies => Abilities.All.Where(SavesWith);
    }
}
