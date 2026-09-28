namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- modifiers ------------------------------------------------------------------------

        public int AbilityModifier(Ability ability) => Scores.Modifier(ability);

        // every timed modifier riding on this actor - a Bless, a potion, a class stance
        public Boons Boons { get; } = new Boons();

        public int CheckModifier(Skill skill) =>
            skill == Skill.None
                ? Boons.FlatOnCheck(skill)
                : Scores.Modifier(skill.Governs()) +
                  Proficiency.Applied(Level, TrainingIn(skill)) +
                  Boons.FlatOnCheck(skill);

        // a raw ability check - no skill named, so no proficiency
        public int CheckModifier(Ability ability) =>
            Scores.Modifier(ability) + Boons.FlatOnCheck(Skill.None);

        public int SaveModifier(Ability ability) =>
            Scores.Modifier(ability) +
            (SavesWith(ability) ? ProficiencyBonus : 0) +
            Boons.FlatOnSave(ability) +
            AuraBonus;
    }
}
