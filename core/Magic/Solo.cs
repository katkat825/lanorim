namespace Core.Magic
{
    // what a spell is to a party of one (cc_task_ui-issues-10-01.md 2.2). Kathleen: "if it can't be cast on
    // yourself, it isn't an option". Spare the Dying is the one: its only target is a creature at 0 hit
    // points, a hero at 0 is Incapacitated and can't cast, and monsters here die at 0. An unavailable spell
    // stays in the data and is never offered to a solo hero (creation, the Spells menu); a companion on the
    // board, or a "spare a foe" rule, brings it back by reading this one word
    public enum Solo
    {
        Usable,

        Unavailable,
    }
}
