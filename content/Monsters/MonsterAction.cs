using System;
using Core.Magic;

namespace Content.Monsters
{
    // one special action on a statblock: what it does (a spell of primitives), and how often
    public sealed class MonsterAction
    {
        public MonsterAction(Spell spell, int rechargeOn = 0, int uses = 0, int dc = 0)
        {
            Spell = spell ?? throw new ArgumentNullException(nameof(spell));
            RechargeOn = Math.Clamp(rechargeOn, 0, 6);
            Uses = Math.Max(0, uses);
            Dc = Math.Max(0, dc);
        }

        // its own printed DC; 0 is the statblock's spellcasting DC
        public int Dc { get; }

        public Spell Spell { get; }

        // "recharge": {"d6": 5} is "Recharge 5-6"
        public int RechargeOn { get; }

        // so many uses, back on a long rest - a feature's 'uses', the same word
        public int Uses { get; }

        public override string ToString() =>
            Spell.Id + (RechargeOn > 0 ? $" (recharge {RechargeOn}-6)" : "") +
            (Uses > 0 ? $" ({Uses}/day)" : "");
    }
}
