namespace Core.Combat
{
    // WHEN THE ENEMIES BREAK (cc_task_h 3.4; Kathleen: "as decided per battle per campaign"). Not a monster's tag: a
    // campaign's fight says it, in its encounter entry ("morale": "leader"). None is the default, and the fight is to
    // the end as it always was
    public enum Morale
    {
        // they fight to the end
        None,

        // the others break when the leader falls: the monster entry marked "leader", or the first
        Leader,

        // they break when half of them, rounded up, are down
        Half,
    }
}
