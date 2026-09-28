using Core.Characters;

namespace Core.Magic
{
    // the check a creature may spend its action on to break free: Web's Strength (Athletics)
    // against the caster's DC, Maze's Intelligence (Investigation) against 20. Skill None is the
    // usual one for the ability; Dc 0 is the caster's. escape + escape_skill + escape_dc, as one
    // record (cc_task_dedupe-effects.md, 3b)
    public sealed record Escape(Ability Ability, Skill Skill = Skill.None, int Dc = 0)
    {
        public Skill Check => Skill != Skill.None ? Skill
                            : Ability == Ability.Dexterity ? Skill.Acrobatics : Skill.Athletics;
    }
}
