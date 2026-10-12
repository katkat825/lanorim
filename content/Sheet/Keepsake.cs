using Core.Characters;

namespace Content.Sheet
{
    // THE KEEPSAKE'S ONCE A REST (cc_task_h 3.2): spent or ready, and the catch itself - the moment the hero drops to 0,
    // they stay up instead, at what it heals. Was kept in Hero's per-rest ledger under its item id; its own type since
    // cc_task_j Part 1. The hero holds one (Hero.Keepsake.cs says whether it's carried and what it heals), and a save
    // writes it in the ledger as before, so a save's file is unchanged
    internal sealed class Keepsake
    {
        // the quest item every hero is given (content/srd/items/gear.json), and its key in a save's ledger
        public const string Id = "keepsake";

        public bool Spent { get; private set; }

        // dropped to 0 just now: held up at `heals` if it's carried and not spent. true when it held
        public bool Catch(Actor actor, bool carried, int heals)
        {
            if (!carried || Spent || !actor.IsDown || actor.IsDead) return false;

            Spent = true;
            actor.StaysUp(heals);

            return true;
        }

        // a save putting it back spent
        public void Spend() => Spent = true;

        // a long rest gives it back (Hero.LongRest; a short rest doesn't, Kathleen 2026-10-12)
        public void Refresh() => Spent = false;
    }
}
