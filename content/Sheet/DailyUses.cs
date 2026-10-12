using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;

namespace Content.Sheet
{
    // WHAT A HERO HAS SPENT SINCE ITS LAST REST: uses by feature id (Rage, Channel Divinity, Wild Shape, a Relentless
    // Endurance), and what a rest gives back. Was Hero's private ledger (cc_task_d proposed it; cc_task_j Part 1).
    // Which feature a use comes out of, and how many it has, are the hero's to say; this only counts
    internal sealed class DailyUses
    {
        readonly Dictionary<string, int> _spent = new Dictionary<string, int>();

        public int Used(string id) => _spent.TryGetValue(id, out int used) ? used : 0;

        public void Spend(string id) => _spent[id] = Used(id) + 1;

        // a save putting one back (already clamped to what the feature has)
        public void Set(string id, int used)
        {
            if (used == 0) _spent.Remove(id);
            else _spent[id] = used;
        }

        // the whole ledger: only a save reads it whole
        public IReadOnlyDictionary<string, int> All => _spent;

        public void LongRest() => _spent.Clear();

        // SRD 5.2.1: what a short rest gives back - every use, one use (Rage, Second Wind, Channel Divinity, Wild
        // Shape), or nothing (Lay on Hands, Indomitable)
        public void ShortRest(Func<string, Recharge> rechargeOf)
        {
            foreach (string id in _spent.Keys.ToList())
            {
                Recharge recharge = rechargeOf(id);

                if (recharge == Recharge.Short) _spent.Remove(id);
                else if (recharge == Recharge.ShortOne)
                {
                    if (_spent[id] > 1) _spent[id]--;
                    else _spent.Remove(id);
                }
            }
        }
    }
}
