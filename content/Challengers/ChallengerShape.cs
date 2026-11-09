namespace Content.Challengers
{
    // WHAT A CHALLENGER TYPE PUTS IN THE ARENA (cc_task_cg Part 1), from the `stats:` line of Kathleen's lists:
    // "a single goblin warrior", "two guards", "team of goblin warrior, goblin archer"
    public enum ChallengerShape
    {
        // one monster: its name in the fight is the challenger's own ("Grik the Knife")
        Single,

        // a fixed number of the same, or a fixed mix: the monsters keep their statblock names
        Group,

        // drawn from its monsters to a budget for the hero's level (ChallengerFit.Team): the name is the team's
        Team,
    }
}
