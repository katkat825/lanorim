namespace Core.Tables
{
    // A HAND-WRITTEN FIGHT THAT IS ALSO A CHALLENGER (cc_task_cg 3.4): an encounter entry with "challenger": { ... }
    // goes on the arena's roster beside the generated ones, from a reputation, and each time the roster is drawn its
    // trigger is rolled (the encounter table's own Trigger: no trigger is always). Otherwise it is an ordinary fight
    public sealed class ChallengerTerms
    {
        public ChallengerTerms(int reputation, Trigger trigger)
        {
            Reputation = reputation < 0 ? 0 : reputation;
            Trigger = trigger;
        }

        // the reputation it appears from
        public int Reputation { get; }

        public Trigger Trigger { get; }

        public override string ToString() => $"a challenger from reputation {Reputation}, {Trigger}";
    }
}
